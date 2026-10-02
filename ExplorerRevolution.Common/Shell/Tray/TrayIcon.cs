using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using ExplorerRevolution.Common;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.Shell.Tray.TrayInterop;

namespace ExplorerRevolution.Common.Shell.Tray
{
    public enum TrayMouseButton
    {
        Left,
        Middle,
        Right
    }

    /// <summary>
    /// 通知区图标数据模型 + 鼠标协议(点击时按 NIN/NIM 约定把消息发回源窗口,
    /// 移植自 ManagedShell.WindowsTray.NotifyIcon,Apache-2.0)。
    /// </summary>
    public sealed class TrayIcon : INotifyPropertyChanged, IDisposable
    {
        private readonly Action<TrayIcon> _removeRequest;
        private DateTime _lastLClick = DateTime.Now;
        private DateTime _lastMClick = DateTime.Now;
        private DateTime _lastRClick = DateTime.Now;

        public TrayIcon(Action<TrayIcon> removeRequest)
        {
            _removeRequest = removeRequest;
        }

        public IntPtr HWnd { get; set; }
        public uint UID { get; set; }
        public Guid GUID { get; set; }
        public uint Version { get; set; }
        public uint CallbackMessage { get; set; }
        public string Path { get; set; }

        /// <summary>图标在屏幕上的实际位置(由 UI 层维护,供 Shell_NotifyIconGetRect 查询)。</summary>
        public Rectangle Placement { get; set; }

        private IntPtr _hIcon;
        public IntPtr HIcon
        {
            get => _hIcon;
            set
            {
                if (_hIcon != value)
                {
                    _hIcon = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPinned;
        /// <summary>是否固定在任务栏(常驻);false 时收进溢出面板。</summary>
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isHidden;
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    _isHidden = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Identifier
        {
            get
            {
                if (GUID != Guid.Empty)
                {
                    return GUID.ToString();
                }

                return (Path ?? "") + ":" + UID + ":" + (Title ?? "");
            }
        }

        /// <summary>用于固定集合匹配的稳定标识:优先 GUID,否则 路径+UID。</summary>
        public string IdentifierPath
        {
            get
            {
                if (GUID != Guid.Empty)
                {
                    return GUID.ToString();
                }

                string path = (Path ?? "").ToLowerInvariant();
                return path + ":" + UID;
            }
        }

        public bool IsEqualByIdentifier(string other)
        {
            if (string.IsNullOrEmpty(other))
            {
                return false;
            }

            if (string.Equals(other, GUID.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (GUID != Guid.Empty || string.IsNullOrEmpty(Path))
            {
                // GUID 图标只用 GUID 匹配;无路径时无法稳定匹配
                return false;
            }

            return string.Equals(other, IdentifierPath, StringComparison.OrdinalIgnoreCase);
        }

        public void SetPinValues(System.Collections.Generic.HashSet<string> pinnedIdentifiers)
        {
            if (pinnedIdentifiers == null)
            {
                return;
            }

            bool pinned = false;
            foreach (string item in pinnedIdentifiers)
            {
                if (IsEqualByIdentifier(item))
                {
                    pinned = true;
                    break;
                }
            }

            IsPinned = pinned;
        }

        public bool Equals(TrayIcon other)
        {
            if (other == null)
            {
                return false;
            }

            return (HWnd.Equals(other.HWnd) && UID.Equals(other.UID)) ||
                   (other.GUID != Guid.Empty && GUID.Equals(other.GUID));
        }

        public bool Equals(SafeNotifyIconData other)
        {
            return (HWnd.Equals(other.hWnd) && UID.Equals(other.uID)) ||
                   (other.guidItem != Guid.Empty && GUID.Equals(other.guidItem));
        }

        #region 鼠标协议

        public void IconMouseEnter()
        {
            if (RemoveIfInvalid())
            {
                return;
            }

            SendMessage(WM_MOUSEHOVER);
            if (Version > 3)
            {
                SendMessage((uint)NIN.POPUPOPEN);
            }
        }

        public void IconMouseLeave()
        {
            if (RemoveIfInvalid())
            {
                return;
            }

            SendMessage(WM_MOUSELEAVE);
            if (Version > 3)
            {
                SendMessage((uint)NIN.POPUPCLOSE);
            }
        }

        public void IconMouseMove()
        {
            if (RemoveIfInvalid())
            {
                return;
            }

            SendMessage(WM_MOUSEMOVE);
        }

        public void IconMouseDown(TrayMouseButton button)
        {
            // 允许图标源窗口在后续点击处理中夺取前台(与原生任务栏一致)
            GetWindowThreadProcessId(HWnd, out uint procId);
            AllowSetForegroundWindow(procId);

            uint doubleClickTime = GetDoubleClickTime();

            if (button == TrayMouseButton.Left)
            {
                SendMessage(DateTime.Now.Subtract(_lastLClick).TotalMilliseconds <= doubleClickTime
                    ? WM_LBUTTONDBLCLK
                    : WM_LBUTTONDOWN);
                _lastLClick = DateTime.Now;
            }
            else if (button == TrayMouseButton.Middle)
            {
                SendMessage(DateTime.Now.Subtract(_lastMClick).TotalMilliseconds <= doubleClickTime
                    ? WM_MBUTTONDBLCLK
                    : WM_MBUTTONDOWN);
                _lastMClick = DateTime.Now;
            }
            else
            {
                SendMessage(DateTime.Now.Subtract(_lastRClick).TotalMilliseconds <= doubleClickTime
                    ? WM_RBUTTONDBLCLK
                    : WM_RBUTTONDOWN);
                _lastRClick = DateTime.Now;
            }
        }

        public void IconMouseUp(TrayMouseButton button)
        {
            if (button == TrayMouseButton.Left)
            {
                SendMessage(WM_LBUTTONUP);
                // 文档中为 v4,但 explorer 对 v3 也发送 SELECT
                if (Version >= 3)
                {
                    SendMessage((uint)NIN.SELECT);
                }
            }
            else if (button == TrayMouseButton.Middle)
            {
                SendMessage(WM_MBUTTONUP);
            }
            else
            {
                SendMessage(WM_RBUTTONUP);
                if (Version >= 3)
                {
                    SendMessage(WM_CONTEXTMENU);
                }
            }
        }

        /// <summary>通知源窗口取消当前菜单/捕获状态,等价于 Explorer 切换托盘项时的清理。</summary>
        public void CancelInteraction()
        {
            if (!RemoveIfInvalid())
            {
                // Match Explorer's leave/close sequence before cancelling the owner.
                IconMouseLeave();

                // Also notify the owner window directly. Some Win32 applications keep
                // their context menu on a separate modal loop and do not close it until
                // the owner receives WM_CANCELMODE.
                NativeMethods.SendMessage(HWnd, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);
                IntPtr popup = GetLastActivePopup(HWnd);
                if (popup != IntPtr.Zero && popup != HWnd && IsWindow(popup))
                {
                    NativeMethods.SendMessage(popup, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);
                    NativeMethods.SendMessage(popup, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
                    NativeMethods.SendMessage(popup, WM_KILLFOCUS, IntPtr.Zero, IntPtr.Zero);
                }

                // Custom tray menus (Steam, Lyricify, QQ) may not be returned by
                // GetLastActivePopup. If the foreground window still belongs to
                // this icon's process, cancel that popup as well.
                IntPtr foreground = GetForegroundWindow();
                GetWindowThreadProcessId(HWnd, out uint ownerPid);
                GetWindowThreadProcessId(foreground, out uint foregroundPid);
                if (foreground != IntPtr.Zero && foreground != HWnd &&
                    foregroundPid != 0 && foregroundPid == ownerPid &&
                    foreground != popup && IsWindow(foreground))
                {
                    NativeMethods.SendMessage(foreground, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);
                    NativeMethods.SendMessage(foreground, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
                    NativeMethods.SendMessage(foreground, WM_KILLFOCUS, IntPtr.Zero, IntPtr.Zero);
                }

                // Steam and a few Chromium/Qt tray clients render their context
                // menu from a helper process, so the foreground popup is not in
                // the tray owner's PID. WM_CANCELMODE is safe to send cross-process
                // and asks that popup to release capture without stealing focus from
                // the window the user just clicked.
                if (foreground != IntPtr.Zero && foreground != HWnd &&
                    foregroundPid != 0 && foregroundPid != ownerPid &&
                    foreground != popup && IsWindow(foreground))
                {
                    NativeMethods.SendMessage(foreground, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);

                    // Menu windows are WS_POPUP; deactivating only that window
                    // mirrors Explorer's dismissal while leaving a newly focused
                    // normal application window untouched.
                    if ((GetWindowLong(foreground, GWL_STYLE) & WS_POPUP) != 0)
                    {
                        NativeMethods.SendMessage(foreground, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
                        NativeMethods.SendMessage(foreground, WM_KILLFOCUS, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                SendMessage(WM_CANCELMODE);
                if (Version > 3)
                {
                    SendMessage((uint)NIN.POPUPCLOSE);
                }
            }
        }

        private bool SendMessage(uint message)
        {
            return SendNotifyMessage(HWnd, CallbackMessage, new UIntPtr(GetMessageWParam()), new IntPtr(unchecked((long)GetMessageLParam(message))));
        }

        private uint GetMessageLParam(uint message)
        {
            return message | (GetMessageHiWord() << 16);
        }

        private uint GetMessageHiWord()
        {
            return Version > 3 ? UID : 0;
        }

        private uint GetMessageWParam()
        {
            // NOTIFYICON_VERSION_4 下 wParam 必须是鼠标屏幕坐标(低字 X,高字 Y),
            // 应用用 GET_X_LPARAM/GET_Y_LPARAM 解析后定位右键菜单/提示框;
            // v3 及以下才是图标 UID。之前的实现把 MK_* 常量当坐标发过去,
            // 导致部分程序把 wParam=2 解析成 (2,0),菜单跑到屏幕左上角。
            if (Version <= 3)
            {
                return UID;
            }

            POINT point;
            if (!GetCursorPos(out point))
            {
                return 0;
            }

            return (uint)((point.Y << 16) | (point.X & 0xFFFF));
        }

        private bool RemoveIfInvalid()
        {
            if (!IsWindow(HWnd))
            {
                _removeRequest?.Invoke(this);
                return true;
            }

            return false;
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            // 图标句柄属于源进程,不由本类销毁
        }
    }
}
