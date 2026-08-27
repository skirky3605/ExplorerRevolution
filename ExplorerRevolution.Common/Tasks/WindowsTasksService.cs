using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common.Tasks
{
    /// <summary>
    /// 任务栏窗口跟踪服务:通过 SHELLHOOK + cloak 事件维护当前应显示在任务栏的窗口集合。
    /// 移植自 ManagedShell.WindowsTasks.TasksService(去掉 WPF DependencyObject 依赖)。
    /// 原文件:https://github.com/cairoshell/ManagedShell (Apache-2.0)
    /// </summary>
    public class WindowsTasksService : IDisposable
    {
        public event EventHandler<WindowEventArgs> WindowActivated;
        public event EventHandler<EventArgs> DesktopActivated;
        public event EventHandler<FullScreenEventArgs> FullScreenChanged;
        public event EventHandler<WindowEventArgs> MonitorChanged;

        private ShellHookWindow _hookWin;
        private readonly object _windowsLock = new object();
        private bool _isInitialized;

        private static int _wmShellHook = -1;
        private static int _wmTaskbarCreated = -1;
        private static int _wmTaskbarButtonCreated = -1;

        private static IntPtr _cloakEventHook = IntPtr.Zero;
        private WinEventDelegate _cloakEventProc;

        /// <summary>
        /// 是否以"替换 explorer"的模式运行。为 false 时(explorer 仍在运行)不主动发送
        /// TaskbarButtonCreated 消息,避免与 explorer 重复通知。
        /// </summary>
        public bool IsAppRunningAsShell { get; set; }

        /// <summary>
        /// 当前被跟踪的顶层窗口集合(与 UI 绑定的数据源)。
        /// </summary>
        public ObservableCollection<TaskbarWindow> Windows { get; } = new ObservableCollection<TaskbarWindow>();

        public void Initialize(bool withMultiMonTracking = false)
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                ShellLog.Debug("WindowsTasksService: Starting");

                // 创建隐藏消息窗口,用于接收 SHELLHOOK 与 ITaskbarList 消息
                _hookWin = new ShellHookWindow();
                _hookWin.CreateHandle(new CreateParams());

                // 向系统声明本窗口为任务管理器窗口
                SetTaskmanWindow(_hookWin.Handle);

                // 注册 SHELLHOOK
                RegisterShellHookWindow(_hookWin.Handle);
                if (_wmShellHook == -1) _wmShellHook = RegisterWindowMessage("SHELLHOOK");
                if (_wmTaskbarCreated == -1) _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");
                if (_wmTaskbarButtonCreated == -1) _wmTaskbarButtonCreated = RegisterWindowMessage("TaskbarButtonCreated");
                _hookWin.MessageReceived += ShellWinProc;

                // cloak/uncloak 事件(Windows 8+)
                _cloakEventProc = CloakEventCallback;
                if (_cloakEventHook == IntPtr.Zero)
                {
                    _cloakEventHook = SetWinEventHook(
                        EVENT_OBJECT_CLOAKED,
                        EVENT_OBJECT_UNCLOAKED,
                        IntPtr.Zero,
                        _cloakEventProc,
                        0,
                        0,
                        WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                }

                // 接管 ITaskbarList 消息归属
                setTaskbarListHwnd(_hookWin.Handle);

                // 枚举当前已打开窗口并设置活动窗口
                getInitialWindows();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                ShellLog.Error("WindowsTasksService: Unable to start: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                ShellLog.Debug("WindowsTasksService: Deregistering hooks");

                DeregisterShellHookWindow(_hookWin.Handle);

                if (_cloakEventHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_cloakEventHook);
                    _cloakEventHook = IntPtr.Zero;
                }

                _hookWin.MessageReceived -= ShellWinProc;
                _hookWin.DestroyHandle();

                // 归还 ITaskbarList 消息归属给 explorer
                setTaskbarListHwnd(IntPtr.Zero);

                _isInitialized = false;
                Windows.Clear();
            }
        }

        public void CloseWindow(TaskbarWindow window)
        {
            if (window.DoClose() != IntPtr.Zero)
            {
                ShellLog.Debug($"WindowsTasksService: Removing window {window.Title} from collection due to no response");
                Windows.Remove(window);
            }
        }

        private void getInitialWindows()
        {
            EnumWindows((hwnd, lParam) =>
            {
                TaskbarWindow win = new TaskbarWindow(this, hwnd);
                if (win.CanAddToTaskbar && win.ShowInTaskbar && !Windows.Contains(win))
                {
                    Windows.Add(win);
                    sendTaskbarButtonCreatedMessage(win.Handle);
                }

                return true;
            }, IntPtr.Zero);

            IntPtr hWndForeground = GetForegroundWindow();
            TaskbarWindow active = Windows.FirstOrDefault(w => w.Handle == hWndForeground && w.ShowInTaskbar);
            if (active != null)
            {
                active.State = TaskbarWindow.WindowState.Active;
                active.SetShowInTaskbar();
            }
        }

        private void sendTaskbarButtonCreatedMessage(IntPtr hWnd)
        {
            // explorer 运行时会自行发送该消息,只有替换模式才需要主动发
            if (IsAppRunningAsShell)
            {
                SendNotifyMessage(hWnd, (uint)_wmTaskbarButtonCreated, UIntPtr.Zero, IntPtr.Zero);
            }
        }

        private TaskbarWindow addWindow(IntPtr hWnd, TaskbarWindow.WindowState initialState = TaskbarWindow.WindowState.Inactive, bool sanityCheck = false)
        {
            TaskbarWindow win = new TaskbarWindow(this, hWnd);

            if (initialState != TaskbarWindow.WindowState.Inactive)
            {
                win.State = initialState;
            }

            // 需要校验时,只有满足基础条件才加入集合
            if (!sanityCheck || win.CanAddToTaskbar)
            {
                Windows.Add(win);
                ShellLog.Debug($"WindowsTasksService: Added window {hWnd} ({win.Title})");
            }

            return win;
        }

        private void removeWindow(IntPtr hWnd)
        {
            TaskbarWindow win;
            while ((win = Windows.FirstOrDefault(w => w.Handle == hWnd)) != null)
            {
                Windows.Remove(win);
                ShellLog.Debug($"WindowsTasksService: Removed window {hWnd} ({win.Title})");
            }
        }

        private void redrawWindow(TaskbarWindow win)
        {
            win.UpdateProperties();
            ShellLog.Debug($"WindowsTasksService: Updated window {win.Handle} ({win.Title})");

            // 同一进程的其他窗口也同步刷新(如多窗口应用)
            foreach (TaskbarWindow wind in Windows)
            {
                if (wind.WinFileName == win.WinFileName && wind.Handle != win.Handle)
                {
                    wind.UpdateProperties();
                }
            }
        }

        private void ShellWinProc(ref Message msg, ref bool handled)
        {
            Message msgCopy = msg;
            handled = true;

            if (msg.Msg == _wmShellHook)
            {
                try
                {
                    lock (_windowsLock)
                    {
                        switch (msg.WParam.ToInt32())
                        {
                            case HSHELL_WINDOWCREATED:
                                TaskbarWindow created = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                if (created == null)
                                {
                                    addWindow(msg.LParam);
                                }
                                else
                                {
                                    created.UpdateProperties();
                                }
                                break;

                            case HSHELL_WINDOWDESTROYED:
                                removeWindow(msg.LParam);
                                break;

                            case HSHELL_WINDOWREPLACING:
                                TaskbarWindow replacing = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                if (replacing != null)
                                {
                                    replacing.State = TaskbarWindow.WindowState.Inactive;
                                    replacing.SetShowInTaskbar();
                                }
                                else
                                {
                                    addWindow(msg.LParam);
                                }
                                break;

                            case HSHELL_WINDOWREPLACED:
                                // 注意:窗口被替换时,应用级状态(如 overlay 图标)会丢失
                                removeWindow(msg.LParam);
                                break;

                            case HSHELL_WINDOWACTIVATED:
                            case HSHELL_RUDEAPPACTIVATED:
                                // 壳自身窗口(任务栏/桌面)激活时,不改变活动窗口跟踪:
                                // 原生任务栏点击同样会夺取焦点,但不应因此清掉"当前前台窗口"
                                if (IsOwnShellWindow(msgCopy.LParam))
                                {
                                    break;
                                }

                                foreach (TaskbarWindow aWin in Windows.Where(w => w.State == TaskbarWindow.WindowState.Active))
                                {
                                    aWin.State = TaskbarWindow.WindowState.Inactive;
                                }

                                if (msg.LParam != IntPtr.Zero)
                                {
                                    TaskbarWindow win = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                    if (win == null)
                                    {
                                        win = addWindow(msg.LParam, TaskbarWindow.WindowState.Active);
                                    }
                                    else
                                    {
                                        win.State = TaskbarWindow.WindowState.Active;
                                        win.SetShowInTaskbar();
                                        ShellLog.Debug($"WindowsTasksService: Activated window {win.Handle} ({win.Title})");
                                    }

                                    if (win != null)
                                    {
                                        foreach (TaskbarWindow wind in Windows)
                                        {
                                            if (wind.WinFileName == win.WinFileName && wind.Handle != win.Handle)
                                            {
                                                wind.SetShowInTaskbar();
                                            }
                                        }

                                        WindowActivated?.Invoke(this, new WindowEventArgs { Window = win });
                                    }
                                }
                                else
                                {
                                    DesktopActivated?.Invoke(this, EventArgs.Empty);
                                }
                                break;

                            case HSHELL_FLASH:
                                TaskbarWindow flashing = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                if (flashing != null)
                                {
                                    if (flashing.State != TaskbarWindow.WindowState.Active)
                                    {
                                        flashing.State = TaskbarWindow.WindowState.Flashing;
                                    }
                                    redrawWindow(flashing);
                                }
                                else
                                {
                                    addWindow(msg.LParam, TaskbarWindow.WindowState.Flashing, true);
                                }
                                break;

                            case HSHELL_ACTIVATESHELLWINDOW:
                                ShellLog.Debug("WindowsTasksService: Activate shell window called.");
                                break;

                            case HSHELL_ENDTASK:
                                removeWindow(msg.LParam);
                                break;

                            case HSHELL_REDRAW:
                                TaskbarWindow redraw = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                if (redraw != null)
                                {
                                    if (redraw.State == TaskbarWindow.WindowState.Flashing)
                                    {
                                        redraw.State = TaskbarWindow.WindowState.Inactive;
                                    }
                                    redrawWindow(redraw);
                                }
                                else
                                {
                                    addWindow(msg.LParam, TaskbarWindow.WindowState.Inactive, true);
                                }
                                break;

                            case HSHELL_MONITORCHANGED:
                                TaskbarWindow monitored = Windows.FirstOrDefault(w => w.Handle == msgCopy.LParam);
                                if (monitored != null)
                                {
                                    monitored.SetMonitor();
                                    MonitorChanged?.Invoke(this, new WindowEventArgs { Window = monitored });
                                }
                                break;

                            case HSHELL_FULLSCREENENTER:
                                FullScreenChanged?.Invoke(this, new FullScreenEventArgs { Handle = msgCopy.LParam, IsEntering = true });
                                ShellLog.Debug($"WindowsTasksService: Full screen entered by window {msgCopy.LParam}");
                                break;

                            case HSHELL_FULLSCREENEXIT:
                                FullScreenChanged?.Invoke(this, new FullScreenEventArgs { Handle = msgCopy.LParam, IsEntering = false });
                                ShellLog.Debug($"WindowsTasksService: Full screen exited by window {msgCopy.LParam}");
                                break;

                            case HSHELL_GETMINRECT:
                                SHELLHOOKINFO minRectInfo = Marshal.PtrToStructure<SHELLHOOKINFO>(msg.LParam);
                                TaskbarWindow minWin = Windows.FirstOrDefault(w => w.Handle == minRectInfo.hwnd);
                                if (minWin != null)
                                {
                                    minRectInfo.rc = minWin.GetButtonRectFromShell();

                                    if (minRectInfo.rc.Width > 0 && minRectInfo.rc.Height > 0)
                                    {
                                        Marshal.StructureToPtr(minRectInfo, msg.LParam, false);
                                        msg.Result = (IntPtr)1;
                                        ShellLog.Debug($"WindowsTasksService: MinRect {minRectInfo.rc.Width}x{minRectInfo.rc.Height} provided for {minWin.Handle} ({minWin.Title})");
                                        return; // 保持 handled=true,避免走 DefWindowProc
                                    }
                                }
                                break;

                            default:
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShellLog.Error("WindowsTasksService: Error in ShellWinProc.", ex);
                    Debugger.Break();
                }
            }
            else if (msg.Msg == _wmTaskbarCreated)
            {
                ShellLog.Debug("WindowsTasksService: TaskbarCreated received, setting ITaskbarList window");
                setTaskbarListHwnd(_hookWin.Handle);
            }
            else if (msg.Msg >= (int)WM_USER)
            {
                // ITaskbarList 消息(由其他窗口通过 TaskbandHWND 属性转发而来)
                switch (msg.Msg)
                {
                    case (int)WM_USER + 64:
                        // SetProgressValue
                        TaskbarWindow pvWin = Windows.FirstOrDefault(w => w.Handle == msgCopy.WParam);
                        if (pvWin != null)
                        {
                            pvWin.ProgressValue = msgCopy.LParam.ToInt32();
                        }
                        msg.Result = IntPtr.Zero;
                        return;

                    case (int)WM_USER + 65:
                        // SetProgressState
                        TaskbarWindow psWin = Windows.FirstOrDefault(w => w.Handle == msgCopy.WParam);
                        if (psWin != null)
                        {
                            psWin.ProgressState = (TBPFLAG)msgCopy.LParam.ToInt32();
                        }
                        msg.Result = IntPtr.Zero;
                        return;

                    default:
                        ShellLog.Debug($"WindowsTasksService: Unhandled ITaskbarList message {msg.Msg}");
                        break;
                }
            }

            handled = false;
        }

        private static bool IsOwnShellWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(hWnd, out uint pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }

        private void CloakEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hWnd != IntPtr.Zero && idObject == 0 && idChild == 0)
            {
                TaskbarWindow win = Windows.FirstOrDefault(w => w.Handle == hWnd);
                if (win != null)
                {
                    ShellLog.Debug($"WindowsTasksService: {(eventType == EVENT_OBJECT_CLOAKED ? "Cloak" : "Uncloak")} event received for {win.Title}");
                    win.SetShowInTaskbar();
                }
            }
        }

        // 在任务栏窗口上设置 TaskbandHWND 属性,使 ITaskbarList 消息转发到我们的钩子窗口
        private void setTaskbarListHwnd(IntPtr hwndHook)
        {
            bool resetProp = true;

            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero)
            {
                return;
            }

            // 若我们的托盘在运行,可能同时存在第二个任务栏(explorer 的)
            IntPtr systemTaskbarHwnd = FindWindowEx(IntPtr.Zero, taskbarHwnd, "Shell_TrayWnd", null);

            if (hwndHook == IntPtr.Zero)
            {
                // 无目标钩子窗口时,回退到 explorer 任务栏的 MSTaskSwWClass
                resetProp = false;
                hwndHook = getChildHwndByClass(systemTaskbarHwnd == IntPtr.Zero ? taskbarHwnd : systemTaskbarHwnd, "MSTaskSwWClass");
            }

            if (hwndHook == IntPtr.Zero)
            {
                return;
            }

            ShellLog.Debug("WindowsTasksService: Adding TaskbandHWND prop to hwnd: " + taskbarHwnd);
            SetProp(taskbarHwnd, "TaskbandHWND", hwndHook);

            if (resetProp && systemTaskbarHwnd != IntPtr.Zero)
            {
                ShellLog.Debug("WindowsTasksService: Removing TaskbandHWND prop from hwnd: " + systemTaskbarHwnd);
                RemoveProp(systemTaskbarHwnd, "TaskbandHWND");
            }
        }

        private IntPtr getChildHwndByClass(IntPtr parentHwnd, string wndClass)
        {
            IntPtr childHwnd = IntPtr.Zero;

            EnumChildWindows(parentHwnd, (hwnd, lParam) =>
            {
                StringBuilder cName = new StringBuilder(256);
                GetClassName(hwnd, cName, cName.Capacity);
                if (cName.ToString() == wndClass)
                {
                    childHwnd = hwnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return childHwnd;
        }

        /// <summary>
        /// 接收 SHELLHOOK / TaskbarCreated / ITaskbarList 消息的隐藏窗口。
        /// </summary>
        private sealed class ShellHookWindow : NativeWindow
        {
            public delegate void MessageReceivedEventHandler(ref Message m, ref bool handled);

            public event MessageReceivedEventHandler MessageReceived;

            protected override void WndProc(ref Message m)
            {
                bool handled = false;
                MessageReceived?.Invoke(ref m, ref handled);

                if (!handled)
                {
                    base.WndProc(ref m);
                }
            }
        }
    }

    public class WindowEventArgs : EventArgs
    {
        public TaskbarWindow Window;
    }

    public class FullScreenEventArgs : EventArgs
    {
        public IntPtr Handle;
        public bool IsEntering;
    }
}
