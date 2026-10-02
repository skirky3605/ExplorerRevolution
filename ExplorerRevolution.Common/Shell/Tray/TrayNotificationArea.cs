using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.Shell.Tray.TrayInterop;

namespace ExplorerRevolution.Common.Shell.Tray
{
    public class TrayBalloonEventArgs : EventArgs
    {
        public TrayIcon Icon;
        public string Title;
        public string Info;
        public NIIF InfoFlags;
    }

    /// <summary>
    /// 自研通知区:枚举/接收托盘图标,维护图标集合,响应 Shell_NotifyIconGetRect 位置查询。
    /// 所有回调都发生在创建 TrayService 的 UI 线程上,集合可直接与 XAML/WinForms 绑定。
    /// </summary>
    public sealed class TrayNotificationArea : IDisposable
    {
        private readonly TrayService _trayService = new TrayService();
        private readonly ExplorerTrayService _explorerTrayService = new ExplorerTrayService();
        private readonly object _lock = new object();
        private Timer _cleanupTimer;
        private bool _initialized;
        private readonly HashSet<string> _pinnedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private const string PinRegistryPath = @"Software\ExplorerRevolution";
        private const string PinRegistryValue = "TrayPinnedIcons";

        public ObservableCollection<TrayIcon> Icons { get; } = new ObservableCollection<TrayIcon>();

        /// <summary>由 UI 层提供每个图标的屏幕矩形(供 Shell_NotifyIconGetRect 查询)。</summary>
        public Func<TrayIcon, Rectangle?> PlacementProvider { get; set; }

        /// <summary>应用发来 NIF_INFO 气泡通知时触发。</summary>
        public event EventHandler<TrayBalloonEventArgs> BalloonShown;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _trayService.SystrayMessage += OnSystrayMessage;
            _trayService.IconDataRequest += OnIconDataRequest;

            LoadPinnedIdentifiers();

            // 1) 先枚举 explorer 中已存在的图标
            _explorerTrayService.Run(OnSystrayMessage);

            // 2) 再创建假托盘窗口并广播 TaskbarCreated,让运行中的应用重新注册
            _trayService.Initialize();
            _trayService.Run();

            // 3) 周期性清理源窗口已销毁但未发 NIM_DELETE 的僵尸图标
            _cleanupTimer = new Timer { Interval = 10000 };
            _cleanupTimer.Tick += (s, e) => CleanupInvalidIcons();
            _cleanupTimer.Start();
        }

        public void Dispose()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized = false;

            if (_cleanupTimer != null)
            {
                _cleanupTimer.Stop();
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }

            _trayService.SystrayMessage -= OnSystrayMessage;
            _trayService.IconDataRequest -= OnIconDataRequest;
            _trayService.Dispose();

            Icons.Clear();
        }

        /// <summary>把图标固定/取消固定到任务栏,并持久化。</summary>
        public void SetPinned(TrayIcon icon, bool pinned)
        {
            if (icon == null || icon.IsPinned == pinned)
            {
                return;
            }

            if (pinned)
            {
                _pinnedIdentifiers.Add(icon.IdentifierPath);
            }
            else
            {
                _pinnedIdentifiers.Remove(icon.IdentifierPath);
            }

            SavePinnedIdentifiers();
            icon.IsPinned = pinned;
        }

        private void LoadPinnedIdentifiers()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PinRegistryPath))
                {
                    string[] values = key?.GetValue(PinRegistryValue) as string[];
                    if (values != null)
                    {
                        foreach (string value in values)
                        {
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                _pinnedIdentifiers.Add(value.Trim());
                            }
                        }
                    }
                }
            }
            catch
            {
                // 读取固定列表失败不影响托盘启动
            }
        }

        private void SavePinnedIdentifiers()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PinRegistryPath))
                {
                    string[] values = new string[_pinnedIdentifiers.Count];
                    _pinnedIdentifiers.CopyTo(values);
                    key?.SetValue(PinRegistryValue, values, RegistryValueKind.MultiString);
                }
            }
            catch
            {
                // 持久化失败不阻断
            }
        }

        private bool OnSystrayMessage(uint message, SafeNotifyIconData data)
        {
            // 新图标必须带有效 hWnd;无 hWnd 且带 GUID 的修改也要能被识别
            if (data.hWnd == IntPtr.Zero && (data.guidItem == Guid.Empty || (NIM)message == NIM.NIM_ADD))
            {
                return false;
            }

            lock (_lock)
            {
                switch ((NIM)message)
                {
                    case NIM.NIM_ADD:
                    case NIM.NIM_MODIFY:
                        return AddOrModifyIcon(message, data);

                    case NIM.NIM_DELETE:
                        return DeleteIcon(data);

                    case NIM.NIM_SETVERSION:
                        foreach (TrayIcon ti in Icons)
                        {
                            if (ti.Equals(data))
                            {
                                ti.Version = data.uVersion;
                                break;
                            }
                        }
                        return true;
                }
            }

            return true;
        }

        private bool AddOrModifyIcon(uint message, SafeNotifyIconData data)
        {
            TrayIcon trayIcon = null;
            bool exists = false;
            bool titleChanged = false;

            foreach (TrayIcon ti in Icons)
            {
                if (ti.Equals(data))
                {
                    exists = true;
                    trayIcon = ti;
                    break;
                }
            }

            if (trayIcon == null)
            {
                trayIcon = new TrayIcon(RemoveIcon);
                // Explorer sends the context-menu notification for version 3
                // clients as well. Enumeration of existing icons does not expose
                // NOTIFYICON_VERSION, so use v3 until NIM_SETVERSION arrives.
                trayIcon.Version = data.uVersion > 0 ? data.uVersion : 3;
            }

            if ((data.uFlags & NIF.STATE) != 0)
            {
                trayIcon.IsHidden = data.dwState == 1;
            }

            if ((data.uFlags & NIF.TIP) != 0 && !string.IsNullOrEmpty(data.szTip))
            {
                trayIcon.Title = data.szTip;
                titleChanged = true;
            }

            if ((data.uFlags & NIF.ICON) != 0)
            {
                trayIcon.HIcon = data.hIcon;
            }

            if (data.hWnd != IntPtr.Zero)
            {
                trayIcon.HWnd = data.hWnd;
                trayIcon.UID = data.uID;
            }

            if ((data.uFlags & NIF.GUID) != 0)
            {
                trayIcon.GUID = data.guidItem;
            }

            if (data.uVersion > 0 && data.uVersion <= 4)
            {
                trayIcon.Version = data.uVersion;
            }

            if ((data.uFlags & NIF.MESSAGE) != 0)
            {
                trayIcon.CallbackMessage = data.uCallbackMessage;
            }

            if (trayIcon.HIcon == IntPtr.Zero && trayIcon.HWnd != IntPtr.Zero)
            {
                // 部分应用不随 NIM_ADD 提供 hIcon(或枚举自 explorer 时句柄无效),用窗口图标兜底
                trayIcon.HIcon = WindowHelpers.GetWindowIcon(trayIcon.HWnd);
            }

            if (!exists)
            {
                if (data.hWnd == IntPtr.Zero)
                {
                    return false;
                }

                trayIcon.Path = GetPathForWindowHandle(trayIcon.HWnd);
                trayIcon.SetPinValues(_pinnedIdentifiers);
                Icons.Add(trayIcon);

                if ((data.uFlags & NIF.INFO) != 0)
                {
                    HandleBalloonData(data, trayIcon);
                }

                ShellLog.Debug($"TrayNotificationArea: Added: {trayIcon.Title} Path: {trayIcon.Path} Hidden: {trayIcon.IsHidden} GUID: {trayIcon.GUID} UID: {trayIcon.UID} Version: {trayIcon.Version}");

                return (NIM)message != NIM.NIM_MODIFY;
            }

            if ((data.uFlags & NIF.INFO) != 0)
            {
                HandleBalloonData(data, trayIcon);
            }

            if (titleChanged && trayIcon.GUID == Guid.Empty)
            {
                trayIcon.Path = GetPathForWindowHandle(trayIcon.HWnd);
                trayIcon.SetPinValues(_pinnedIdentifiers);
            }

            ShellLog.Debug($"TrayNotificationArea: Modified: {trayIcon.Title}");
            return true;
        }

        private bool DeleteIcon(SafeNotifyIconData data)
        {
            foreach (TrayIcon icon in Icons)
            {
                if (icon.Equals(data))
                {
                    Icons.Remove(icon);
                    ShellLog.Debug($"TrayNotificationArea: Removed: {icon.Title}");
                    return true;
                }
            }

            return false;
        }

        private void HandleBalloonData(SafeNotifyIconData data, TrayIcon trayIcon)
        {
            if (string.IsNullOrEmpty(data.szInfoTitle))
            {
                return;
            }

            ShellLog.Debug($"TrayNotificationArea: Received notification \"{data.szInfoTitle}\" for {trayIcon.Title}");
            BalloonShown?.Invoke(this, new TrayBalloonEventArgs
            {
                Icon = trayIcon,
                Title = data.szInfoTitle,
                Info = data.szInfo,
                InfoFlags = data.dwInfoFlags
            });
        }

        private IntPtr OnIconDataRequest(int message, IntPtr hWnd, uint uID, Guid guidItem)
        {
            foreach (TrayIcon ti in Icons)
            {
                if ((guidItem != Guid.Empty && guidItem == ti.GUID) ||
                    (ti.HWnd == hWnd && ti.UID == uID))
                {
                    Rectangle rect = ti.Placement;
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    if (message == 1)
                    {
                        return MakeLParamIntPtr(rect.Left, rect.Top);
                    }

                    if (message == 2)
                    {
                        return MakeLParamIntPtr(rect.Right, rect.Bottom);
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void RemoveIcon(TrayIcon icon)
        {
            if (Icons.Contains(icon))
            {
                Icons.Remove(icon);
            }
        }

        private void CleanupInvalidIcons()
        {
            lock (_lock)
            {
                for (int i = Icons.Count - 1; i >= 0; i--)
                {
                    TrayIcon icon = Icons[i];
                    if (icon.HWnd == IntPtr.Zero || !IsWindow(icon.HWnd))
                    {
                        Icons.RemoveAt(i);
                    }
                }
            }
        }

        private static IntPtr MakeLParamIntPtr(int x, int y)
        {
            return new IntPtr(unchecked((long)((ushort)x | ((long)(ushort)y << 16))));
        }
    }
}
