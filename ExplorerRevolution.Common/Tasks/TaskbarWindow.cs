using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.UI.Xaml.Media.Imaging;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common.Tasks
{
    /// <summary>
    /// 任务栏按钮对应的顶层窗口模型。
    /// 移植自 ManagedShell.WindowsTasks.ApplicationWindow(替换 WPF 类型为 UWP 类型)。
    /// 原文件:https://github.com/cairoshell/ManagedShell (Apache-2.0)
    /// </summary>
    [DebuggerDisplay("Title: {Title}, Handle: {Handle}")]
    public class TaskbarWindow : IEquatable<TaskbarWindow>, INotifyPropertyChanged
    {
        private const int TITLE_LENGTH = 1024;

        private readonly WindowsTasksService _tasksService;
        private readonly StringBuilder titleBuilder = new StringBuilder(TITLE_LENGTH);

        public delegate void GetButtonRectEventHandler(ref ShortRect rect);

        /// <summary>
        /// UI 层注册后,可响应 HSHELL_GETMINRECT(最小化动画的落点矩形)。
        /// </summary>
        public event GetButtonRectEventHandler GetButtonRect;

        public TaskbarWindow(WindowsTasksService tasksService, IntPtr handle)
        {
            _tasksService = tasksService;
            Handle = handle;
            State = WindowState.Inactive;
        }

        public IntPtr Handle { get; set; }

        private string _appUserModelId;

        public string AppUserModelID
        {
            get
            {
                if (string.IsNullOrEmpty(_appUserModelId))
                {
                    _appUserModelId = GetAppUserModelId(Handle);
                }

                return _appUserModelId;
            }
        }

        private bool? _isUWP;

        public bool IsUWP
        {
            get
            {
                if (_isUWP == null)
                {
                    _isUWP = WinFileName.ToLower().Contains("applicationframehost.exe");
                }

                return _isUWP.Value;
            }
        }

        private string _winFileName = "";

        public string WinFileName
        {
            get
            {
                if (string.IsNullOrEmpty(_winFileName))
                {
                    _winFileName = GetPathForWindowHandle(Handle);
                }

                return _winFileName;
            }
        }

        private string _winFileDescription;

        public string WinFileDescription
        {
            get
            {
                if (string.IsNullOrEmpty(_winFileDescription))
                {
                    _winFileDescription = getFileDescription();
                }

                return _winFileDescription;
            }
        }

        private uint? _procId;

        public uint? ProcId => _procId ?? (_procId = GetProcIdForHandle(Handle));

        private string _className;

        public string ClassName
        {
            get
            {
                if (_className == null)
                {
                    _className = GetWindowClassName(Handle);
                    OnPropertyChanged(nameof(ClassName));
                }

                return _className;
            }
        }

        private string _title;

        public string Title
        {
            get
            {
                if (_title == null)
                {
                    setTitle();
                }

                return _title;
            }
        }

        private void setTitle()
        {
            string title = "";
            try
            {
                titleBuilder.Clear();
                GetWindowText(Handle, titleBuilder, TITLE_LENGTH + 1);
                title = titleBuilder.ToString();
            }
            catch { }

            if (_title != title)
            {
                _title = title;
                OnPropertyChanged(nameof(Title));
            }
        }

        private BitmapImage _icon;

        /// <summary>
        /// 窗口图标(UWP BitmapImage)。由 UI 层负责从 HICON/AUMID 转换后赋值
        /// (UWP 的 BitmapImage 需在 UI 线程上创建,故不在服务层加载)。
        /// </summary>
        public BitmapImage Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }

        private TBPFLAG _progressState;

        public TBPFLAG ProgressState
        {
            get => _progressState;
            set
            {
                _progressState = value;
                if (value == TBPFLAG.TBPF_NOPROGRESS)
                {
                    ProgressValue = 0;
                }
                OnPropertyChanged(nameof(ProgressState));
            }
        }

        private int _progressValue;

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                if (_progressValue != value)
                {
                    _progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        private WindowState _state;

        public WindowState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        private IntPtr _hMonitor;

        public IntPtr HMonitor
        {
            get
            {
                SetMonitor();
                return _hMonitor;
            }
        }

        public bool IsMinimized => IsIconic(Handle);

        public WindowShowStyle ShowStyle => GetWindowShowStyle(Handle);

        public int WindowStyles => GetWindowLong(Handle, GWL_STYLE);

        public int ExtendedWindowStyles => GetWindowLong(Handle, GWL_EXSTYLE);

        /// <summary>
        /// 窗口是否具备出现在任务栏的基础条件(可见、非工具窗、非 ITaskList_Deleted 等)。
        /// </summary>
        public bool CanAddToTaskbar
        {
            get
            {
                int extendedWindowStyles = ExtendedWindowStyles;
                bool isWindow = IsWindow(Handle);
                bool isVisible = IsWindowVisible(Handle);
                bool isToolWindow = (extendedWindowStyles & WS_EX_TOOLWINDOW) != 0;
                bool isAppWindow = (extendedWindowStyles & (int)WS_EX_APPWINDOW) != 0;
                bool isNoActivate = (extendedWindowStyles & WS_EX_NOACTIVATE) != 0;
                bool isDeleted = GetProp(Handle, "ITaskList_Deleted") != IntPtr.Zero;
                IntPtr ownerWin = GetWindow(Handle, GW_OWNER);

                return isWindow
                    && isVisible
                    && (ownerWin == IntPtr.Zero || isAppWindow)
                    && (!isNoActivate || isAppWindow)
                    && !isToolWindow
                    && !isDeleted;
            }
        }

        public bool CanMinimize => (WindowStyles & WS_MINIMIZEBOX) != 0 && IsWindowEnabled(Handle);

        private bool? _showInTaskbar;

        /// <summary>
        /// 窗口是否应显示在任务栏(在 CanAddToTaskbar 基础上额外排除 cloaked / 隐式壳窗口)。
        /// </summary>
        public bool ShowInTaskbar
        {
            get
            {
                if (_showInTaskbar == null)
                {
                    SetShowInTaskbar();
                }

                return _showInTaskbar.Value;
            }
        }

        public void SetShowInTaskbar()
        {
            bool showInTaskbar = getShowInTaskbar();

            if (_showInTaskbar != showInTaskbar)
            {
                _showInTaskbar = showInTaskbar;
                OnPropertyChanged(nameof(ShowInTaskbar));
            }
        }

        private bool getShowInTaskbar()
        {
            // EnumWindows 和 ShellHook 会返回被 'cloaked' 的 UWP 窗口,它们不应出现在任务栏。
            int cloaked = 0;
            DwmGetWindowAttribute(Handle, DWMWA_CLOAKED, out cloaked, sizeof(int));
            if (cloaked > 0)
            {
                ShellLog.Debug($"TaskbarWindow: Cloaked window {Handle} ({Title}) hidden from taskbar");
                return false;
            }

            // 未被 cloaked 的 UWP 壳窗口同样需要从任务栏隐藏。
            if (IsImmersiveShellWindow())
            {
                ShellLog.Debug($"TaskbarWindow: Hiding immersive shell window {Handle} ({Title}) from taskbar");
                return false;
            }

            return CanAddToTaskbar;
        }

        public bool IsImmersiveShellWindow()
        {
            string className = ClassName;
            if (className == "ApplicationFrameWindow"
                || className == "Windows.UI.Core.CoreWindow"
                || className == "StartMenuSizingFrame"
                || className == "Shell_LightDismissOverlay")
            {
                if ((ExtendedWindowStyles & WS_EX_WINDOWEDGE) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private string getFileDescription()
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(WinFileName).FileDescription;
            }
            catch (Exception e)
            {
                ShellLog.Warning($"TaskbarWindow: Unable to get file description for {WinFileName} ({Title}): {e.Message}");
                return Title;
            }
        }

        internal void UpdateProperties()
        {
            setTitle();
            SetShowInTaskbar();
        }

        internal void SetMonitor()
        {
            _hMonitor = MonitorFromWindow(Handle, MONITOR_DEFAULTTONEAREST);
        }

        internal ShortRect GetButtonRectFromShell()
        {
            ShortRect rect = new ShortRect();
            GetButtonRect?.Invoke(ref rect);
            return rect;
        }

        public void BringToFront()
        {
            // 最小化时先还原
            if (IsMinimized)
            {
                Restore();
            }
            else
            {
                // 最大化窗口用 ShowMaximized,避免先还原再最大化
                if (GetWindowShowStyle(Handle) != WindowShowStyle.ShowMaximized
                    || !ShowWindow(Handle, (int)WindowShowStyle.ShowMaximized))
                {
                    ShowWindow(Handle, (int)WindowShowStyle.Show);
                }

                makeForeground();

                if (State == WindowState.Flashing)
                {
                    State = WindowState.Active; // 部分顽固窗口(如 Outlook)在已激活时仍闪烁,借此停止
                }
            }
        }

        public void Minimize()
        {
            if (!CanMinimize)
            {
                return;
            }

            uint procId = GetProcIdForHandle(Handle);
            AllowSetForegroundWindow(procId);
            PostMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
        }

        public void Restore()
        {
            IntPtr retval = IntPtr.Zero;
            SendMessageTimeout(Handle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 200, out retval);

            makeForeground();
        }

        public void Maximize()
        {
            if (!ShowWindow(Handle, (int)WindowShowStyle.Maximize))
            {
                // 对提权窗口没有可靠回退,退化为还原
                IntPtr retval = IntPtr.Zero;
                SendMessageTimeout(Handle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero,
                    SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 200, out retval);
            }

            makeForeground();
        }

        private void makeForeground()
        {
            SetForegroundWindow(GetLastActivePopup(Handle));
        }

        internal IntPtr DoClose()
        {
            makeForeground();
            IntPtr retval = IntPtr.Zero;
            SendMessageTimeout(Handle, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 200, out retval);

            return retval;
        }

        public void Close()
        {
            _tasksService.CloseWindow(this);
        }

        public void Move()
        {
            // 通过方向键移动窗口;需先激活
            BringToFront();
            IntPtr retval = IntPtr.Zero;
            SendMessageTimeout(Handle, WM_SYSCOMMAND, (IntPtr)SC_MOVE, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 200, out retval);
        }

        public void Size()
        {
            // 通过方向键调整窗口大小;需先激活
            BringToFront();
            IntPtr retval = IntPtr.Zero;
            SendMessageTimeout(Handle, WM_SYSCOMMAND, (IntPtr)SC_SIZE, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 200, out retval);
        }

        /// <summary>
        /// 返回窗口显示状态:normal(1)、minimized(2)、maximized(3)。
        /// </summary>
        private WindowShowStyle GetWindowShowStyle(IntPtr hWnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT { Length = (uint)Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
            GetWindowPlacement(hWnd, ref placement);
            return placement.showCmd;
        }

        public bool Equals(TaskbarWindow other)
        {
            return other != null && Handle.Equals(other.Handle);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public enum WindowState
        {
            Active,
            Inactive,
            Hidden,
            Flashing,
            Unknown = 999
        }
    }
}
