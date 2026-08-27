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
                            SHELLTRAYDATA trayData = (SHELLTRAYDATA)Marshal.PtrToStructure(copyData.lpData, typeof(SHELLTRAYDATA));
                            bool handled = SystrayMessage?.Invoke(trayData.dwMessage, new SafeNotifyIconData(trayData.nid)) ?? false;
                            if (handled)
                            {
                                return (IntPtr)1;
                            }
                            break;

                        case 3:
                            // Shell_NotifyIconGetRect 位置查询
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
