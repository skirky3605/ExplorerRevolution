using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.Shell.Tray.TrayInterop;

namespace ExplorerRevolution.Common.Shell.Tray
{
    /// <summary>
    /// 自研托盘消息接收窗口:注册同名 Shell_TrayWnd / TrayNotifyWnd 窗口类并保持置顶,
    /// 使应用调用 Shell_NotifyIcon 时通过 WM_COPYDATA 把图标增删改发到本进程
    /// (移植自 ManagedShell.WindowsTray.TrayService,Apache-2.0)。
    /// </summary>
    public sealed class TrayService : IDisposable
    {
        private const string NotifyWndClass = "TrayNotifyWnd";
        private const string TrayWndClass = "Shell_TrayWnd";

        // 某些消息必须 PostMessage 而非 SendMessage,否则源进程可能因此挂起
        private static readonly int[] ForwardMessagesPost = { unchecked((int)WM_USER + 372) };

        private WndProcDelegate _wndProcDelegate;
        private IntPtr _hwndTray;
        private IntPtr _hwndNotify;
        private IntPtr _hwndFwd;
        private readonly IntPtr _hInstance;
        private readonly Timer _trayMonitor;

        public event SystrayMessageHandler SystrayMessage;
        public event IconDataRequestHandler IconDataRequest;

        public TrayService()
        {
            _hInstance = Marshal.GetHINSTANCE(typeof(TrayService).Module);
            _trayMonitor = new Timer { Interval = 100 };
            _trayMonitor.Tick += TrayMonitorTick;
        }

        public IntPtr TrayWindow => _hwndTray;

        public bool IsInitialized => _hwndTray != IntPtr.Zero;

        /// <summary>创建假托盘的 Shell_TrayWnd 与 TrayNotifyWnd 窗口。</summary>
        public void Initialize()
        {
            if (_hwndTray != IntPtr.Zero)
            {
                return;
            }

            DestroyWindows();

            _wndProcDelegate = WndProc;

            RegisterTrayWnd();
            RegisterNotifyWnd();
        }

        /// <summary>广播 TaskbarCreated,让已在运行的应用把通知区图标重新注册到本托盘。</summary>
        public void Run()
        {
            if (_hwndTray == IntPtr.Zero)
            {
                return;
            }

            Resume();
            SendTaskbarCreated();
        }

        public void Dispose()
        {
            _trayMonitor.Stop();
            DestroyWindows();

            // 通知应用重新注册(恢复后的 explorer 会重新成为托盘)
            SendTaskbarCreated();
        }

        private void SendTaskbarCreated()
        {
            int msg = RegisterWindowMessage("TaskbarCreated");
            if (msg > 0)
            {
                SendNotifyMessage(HWND_BROADCAST, (uint)msg, UIntPtr.Zero, IntPtr.Zero);
            }
        }

        private void Resume()
        {
            if (_hwndTray == IntPtr.Zero)
            {
                return;
            }

            SetWindowsTrayBottommost();
            MakeTrayTopmost();
            _trayMonitor.Start();
        }

        private void DestroyWindows()
        {
            if (_hwndNotify != IntPtr.Zero)
            {
                DestroyWindow(_hwndNotify);
                UnregisterClass(NotifyWndClass, _hInstance);
                _hwndNotify = IntPtr.Zero;
            }

            if (_hwndTray != IntPtr.Zero)
            {
                DestroyWindow(_hwndTray);
                UnregisterClass(TrayWndClass, _hInstance);
                _hwndTray = IntPtr.Zero;
            }

            _hwndFwd = IntPtr.Zero;
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((uint)msg)
            {
                case WM_COPYDATA:
                    if (lParam == IntPtr.Zero)
                    {
                        break;
                    }

                    COPYDATASTRUCT copyData = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));

                    switch (copyData.dwData.ToInt32())
                    {
                        case 1:
                            // Shell_NotifyIcon 增删改
                            if (copyData.lpData == IntPtr.Zero || copyData.cbData < 8)
                            {
                                break;
                            }

                            uint trayMessage = unchecked((uint)Marshal.ReadInt32(copyData.lpData, 4));
                            SafeNotifyIconData trayIconData = ReadNotifyIconData(
                                IntPtr.Add(copyData.lpData, 8), copyData.cbData - 8);

                            bool handled = SystrayMessage?.Invoke(trayMessage, trayIconData) ?? false;
                            if (handled)
                            {
                                return (IntPtr)1;
                            }
                            break;

                        case 3:
                            // Shell_NotifyIconGetRect 位置查询
                            if (copyData.lpData == IntPtr.Zero || copyData.cbData < 16)
                            {
                                break;
                            }

                            int identifierSize = Marshal.ReadInt32(copyData.lpData, 8);
                            if (identifierSize > 0 && identifierSize <= copyData.cbData &&
                                copyData.cbData < Marshal.SizeOf(typeof(WINNOTIFYICONIDENTIFIER)))
                            {
                                WINNOTIFYICONIDENTIFIER32 icon32 =
                                    (WINNOTIFYICONIDENTIFIER32)Marshal.PtrToStructure(copyData.lpData, typeof(WINNOTIFYICONIDENTIFIER32));
                                return IconDataRequest?.Invoke(icon32.dwMessage, new IntPtr(unchecked((int)icon32.hWnd)), icon32.uID, icon32.guidItem) ?? IntPtr.Zero;
                            }

                            WINNOTIFYICONIDENTIFIER iconData =
                                (WINNOTIFYICONIDENTIFIER)Marshal.PtrToStructure(copyData.lpData, typeof(WINNOTIFYICONIDENTIFIER));
                            return IconDataRequest?.Invoke(iconData.dwMessage, iconData.hWnd, iconData.uID, iconData.guidItem) ?? IntPtr.Zero;
                    }
                    break;

                case WM_WINDOWPOSCHANGED:
                    WINDOWPOS wndPos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                    if ((wndPos.flags & SWP_SHOWWINDOW) != 0 && _hwndTray != IntPtr.Zero)
                    {
                        // 托盘窗口只需存在并接收消息,不允许可见
                        SetWindowLong(_hwndTray, GWL_STYLE, GetWindowLong(_hwndTray, GWL_STYLE) & ~WS_VISIBLE);
                    }
                    break;
            }

            if (msg == (int)WM_COPYDATA ||
                msg == (int)WM_ACTIVATEAPP ||
                msg == (int)WM_COMMAND ||
                msg >= (int)WM_USER)
            {
                return ForwardMsg(hWnd, msg, wParam, lParam);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static SafeNotifyIconData ReadNotifyIconData(IntPtr p, int bytes)
        {
            // cbSize is version-dependent and is not a reliable indicator of
            // pointer width. Probe both native pointer locations and choose the
            // one that names a live window. This also handles 64-bit clients
            // sending a legacy (shorter) cbSize.
            IntPtr hwnd32 = bytes >= 8 ? new IntPtr(unchecked((int)(uint)Marshal.ReadInt32(p, 4))) : IntPtr.Zero;
            IntPtr hwnd64 = bytes >= 16 ? Marshal.ReadIntPtr(p, 8) : IntPtr.Zero;
            bool valid32 = hwnd32 != IntPtr.Zero && IsWindow(hwnd32);
            bool valid64 = hwnd64 != IntPtr.Zero && IsWindow(hwnd64);

            bool is32 = (valid32 && !valid64) ||
                        (!valid32 && !valid64 && bytes < Marshal.SizeOf(typeof(NOTIFYICONDATA)));
            int cb = Marshal.ReadInt32(p, 0);
            int uidOffset = is32 ? 8 : 16;
            int flagsOffset = is32 ? 12 : 20;
            int callbackOffset = is32 ? 16 : 24;
            int iconOffset = is32 ? 20 : 32;
            int tipOffset = is32 ? 24 : 40;
            int stateOffset = is32 ? 280 : 296;
            int stateMaskOffset = is32 ? 284 : 300;
            int infoOffset = is32 ? 288 : 304;
            int versionOffset = is32 ? 800 : 816;
            int infoTitleOffset = is32 ? 804 : 820;
            int infoFlagsOffset = is32 ? 932 : 948;
            int guidOffset = is32 ? 936 : 952;
            int balloonOffset = is32 ? 952 : 968;

            SafeNotifyIconData data = new SafeNotifyIconData
            {
                cbSize = cb,
                hWnd = is32 ? hwnd32 : hwnd64,
                uID = ReadUInt32(p, uidOffset),
                uFlags = (NIF)ReadUInt32(p, flagsOffset),
                uCallbackMessage = ReadUInt32(p, callbackOffset),
                hIcon = is32 ? new IntPtr(unchecked((int)ReadUInt32(p, iconOffset))) : Marshal.ReadIntPtr(p, iconOffset),
                szTip = ReadString(p, tipOffset, 128, bytes),
                dwState = Marshal.ReadInt32(p, stateOffset),
                dwStateMask = Marshal.ReadInt32(p, stateMaskOffset),
                szInfo = ReadString(p, infoOffset, 256, bytes),
                uVersion = ReadUInt32(p, versionOffset),
                szInfoTitle = ReadString(p, infoTitleOffset, 64, bytes),
                dwInfoFlags = (NIIF)ReadUInt32(p, infoFlagsOffset),
                guidItem = bytes >= guidOffset + 16 ? Marshal.PtrToStructure<Guid>(IntPtr.Add(p, guidOffset)) : Guid.Empty,
                hBalloonIcon = is32 ? new IntPtr(unchecked((int)ReadUInt32(p, balloonOffset))) : Marshal.ReadIntPtr(p, balloonOffset)
            };

            return data;
        }

        private static uint ReadUInt32(IntPtr p, int offset)
        {
            return unchecked((uint)Marshal.ReadInt32(p, offset));
        }

        private static string ReadString(IntPtr p, int offset, int chars, int bytes)
        {
            int available = Math.Min(chars, Math.Max(0, (bytes - offset) / 2));
            return available > 0 ? Marshal.PtrToStringUni(IntPtr.Add(p, offset), available)?.TrimEnd('\0') : null;
        }

        /// <summary>把未处理的消息转发给真正的 explorer 托盘(例如 AppBar 查询)。</summary>
        private IntPtr ForwardMsg(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (_hwndFwd == IntPtr.Zero || !IsWindow(_hwndFwd))
            {
                _hwndFwd = FindWindowsTray(_hwndTray);
            }

            if (_hwndFwd != IntPtr.Zero)
            {
                if (msg >= (int)WM_USER && Array.IndexOf(ForwardMessagesPost, msg) >= 0)
                {
                    PostMessage(_hwndFwd, (uint)msg, wParam, lParam);
                    return DefWindowProc(hWnd, msg, wParam, lParam);
                }

                return SendMessage(_hwndFwd, (uint)msg, wParam, lParam);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private ushort RegisterWndClass(string name)
        {
            WNDCLASS newClass = new WNDCLASS
            {
                lpszClassName = name,
                hInstance = _hInstance,
                style = CS_DBLCLKS,
                lpfnWndProc = _wndProcDelegate
            };

            return RegisterClass(ref newClass);
        }

        private void RegisterTrayWnd()
        {
            ushort trayClassReg = RegisterWndClass(TrayWndClass);
            if (trayClassReg == 0)
            {
                ShellLog.Info($"TrayService: Error registering {TrayWndClass} class ({Marshal.GetLastWin32Error()})");
            }

            _hwndTray = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                TrayWndClass,
                "",
                WS_POPUP | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
                0,
                0,
                GetSystemMetrics(SM_CXSCREEN),
                32,
                IntPtr.Zero,
                IntPtr.Zero,
                _hInstance,
                IntPtr.Zero);

            if (_hwndTray == IntPtr.Zero)
            {
                ShellLog.Info($"TrayService: Error creating {TrayWndClass} window ({Marshal.GetLastWin32Error()})");
            }
        }

        private void RegisterNotifyWnd()
        {
            ushort trayNotifyClassReg = RegisterWndClass(NotifyWndClass);
            if (trayNotifyClassReg == 0)
            {
                ShellLog.Info($"TrayService: Error registering {NotifyWndClass} class ({Marshal.GetLastWin32Error()})");
            }

            _hwndNotify = CreateWindowEx(
                0,
                NotifyWndClass,
                null,
                WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
                0,
                0,
                GetSystemMetrics(SM_CXSCREEN),
                32,
                _hwndTray,
                IntPtr.Zero,
                _hInstance,
                IntPtr.Zero);

            if (_hwndNotify == IntPtr.Zero)
            {
                ShellLog.Info($"TrayService: Error creating {NotifyWndClass} window ({Marshal.GetLastWin32Error()})");
            }
        }

        private void TrayMonitorTick(object sender, EventArgs e)
        {
            if (_hwndTray == IntPtr.Zero)
            {
                return;
            }

            IntPtr taskbarHwnd = FindWindow(TrayWndClass, "");
            if (taskbarHwnd == _hwndTray)
            {
                return;
            }

            // explorer(或其它托盘)抢占了 Shell_TrayWnd 顶层位置,重新把自己抬到最上
            MakeTrayTopmost();
        }

        private void SetWindowsTrayBottommost()
        {
            IntPtr taskbarHwnd = FindWindowsTray(_hwndTray);
            if (taskbarHwnd != IntPtr.Zero)
            {
                SetWindowPos(taskbarHwnd, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        private void MakeTrayTopmost()
        {
            if (_hwndTray != IntPtr.Zero)
            {
                SetWindowPos(_hwndTray, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        /// <summary>查找"另一个" Shell_TrayWnd(即 explorer 的真托盘,排除自身)。</summary>
        private static IntPtr FindWindowsTray(IntPtr hwndIgnore)
        {
            IntPtr taskbarHwnd = FindWindow(TrayWndClass, "");
            if (hwndIgnore != IntPtr.Zero)
            {
                while (taskbarHwnd == hwndIgnore)
                {
                    taskbarHwnd = FindWindowEx(IntPtr.Zero, taskbarHwnd, TrayWndClass, "");
                }
            }

            return taskbarHwnd;
        }
    }
}
