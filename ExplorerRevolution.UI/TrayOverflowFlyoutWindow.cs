using ExplorerRevolution.Common;
using ExplorerRevolution.Common.Shell.Tray;
using Mile.Xaml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Windows.UI.Xaml.Hosting;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.UI
{
    /// <summary>托盘溢出面板宿主统一接口:新 UWP Flyout 与旧 WinForms 浮窗都可挂接。</summary>
    public interface ITrayOverflowHost : IDisposable
    {
        bool Visible { get; }

        /// <summary>宿主是否初始化成功(失败时调用方应回退到其他实现)。</summary>
        bool IsAvailable { get; }

        void ShowOverflow(
            Rectangle anchorRect,
            float dpiScale,
            IEnumerable<TrayIcon> icons,
            Action<TrayIcon> openRequest,
            Action<TrayIcon> pinRequest);

        void RefreshIcons(IEnumerable<TrayIcon> icons);

        void Hide();
    }

    /// <summary>
    /// 托盘溢出面板的 UWP Flyout 宿主。
    /// 为了让 Flyout 拥有真正的 UWP XAML 渲染,同时不破坏任务栏自身的 acrylic,
    /// 这里把 Flyout 放到一条独立的 STA UI 线程上,用独立的 XAML Island 窗口承载
    /// (同一线程上创建多个 XAML Island 窗口会破坏渲染,见 microsoft-ui-xaml #3482)。
    /// 窗口只创建一次,隐藏/显示复用,不销毁,进一步规避该缺陷。
    /// </summary>
    public sealed class TrayOverflowFlyoutWindow : ITrayOverflowHost
    {
        private readonly SynchronizationContext _mainSyncContext;
        private readonly object _gate = new object();
        private readonly AutoResetEvent _ready = new AutoResetEvent(false);

        private Thread _thread;
        private Form _form;                    // 仅 Flyout 线程访问
        private WindowsXamlHost _host;         // 仅 Flyout 线程访问
        private TrayOverflowFlyoutPage _page;  // 仅 Flyout 线程访问
        private WindowsXamlManager _xamlManager; // 保持 XAML 运行时存活,仅 Flyout 线程访问
        private IntPtr _formHandle;
        private volatile bool _initFailed;

        private Action<TrayIcon> _openRequest;
        private Action<TrayIcon> _pinRequest;
        private Rectangle _anchorRect;
        private float _dpiScale = 1f;

        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHook;

        /// <summary>是否正在显示(Flyout 线程外读取)。</summary>
        public bool Visible { get; private set; }

        /// <summary>独立线程 + XAML Island 是否初始化成功。</summary>
        public bool IsAvailable => !_initFailed && _formHandle != IntPtr.Zero;

        public TrayOverflowFlyoutWindow()
        {
            _mainSyncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// 填充图标并显示在 anchorRect(chevron 屏幕矩形)上方的任务栏区域。
        /// 线程安全:从主线程调用,内部封送到 Flyout 线程。
        /// </summary>
        public void ShowOverflow(
            Rectangle anchorRect,
            float dpiScale,
            IEnumerable<TrayIcon> icons,
            Action<TrayIcon> openRequest,
            Action<TrayIcon> pinRequest)
        {
            EnsureThread();
            if (_initFailed)
            {
                return;
            }

            var snapshot = icons?.Where(i => i != null).ToList() ?? new List<TrayIcon>();
            if (snapshot.Count == 0)
            {
                return;
            }

            _openRequest = openRequest;
            _pinRequest = pinRequest;
            _anchorRect = anchorRect;
            _dpiScale = dpiScale;
            Visible = true;

            TryBeginInvoke(() =>
            {
                try
                {
                    Windows.Foundation.Size dipSize = _page.SetIcons(snapshot);
                    PositionAndShow(dipSize);
                }
                catch
                {
                    // 显示失败不影响任务栏主体
                }
            });

            InstallMouseHook();
        }

        /// <summary>面板打开时刷新图标(固定/取消固定、图标增删后调用)。</summary>
        public void RefreshIcons(IEnumerable<TrayIcon> icons)
        {
            if (!Visible || _initFailed)
            {
                return;
            }

            var snapshot = icons?.Where(i => i != null).ToList() ?? new List<TrayIcon>();
            if (snapshot.Count == 0)
            {
                Hide();
                return;
            }

            TryBeginInvoke(() =>
            {
                try
                {
                    Windows.Foundation.Size dipSize = _page.SetIcons(snapshot);
                    PositionAndShow(dipSize);
                }
                catch
                {
                    // 刷新失败忽略
                }
            });
        }

        public void Hide()
        {
            if (!Visible)
            {
                return;
            }

            Visible = false;
            UninstallMouseHook();
            TryBeginInvoke(() =>
            {
                try
                {
                    if (_form != null && !_form.IsDisposed)
                    {
                        _form.Hide();
                    }
                }
                catch
                {
                    // 忽略
                }
            });
        }

        /// <summary>
        /// 按 chevron 锚点定位到任务栏上方右对齐,并把底部箭头对准 chevron 中心。
        /// 只在 Flyout 线程调用。
        /// </summary>
        private void PositionAndShow(Windows.Foundation.Size dipSize)
        {
            Form form = _form;
            if (form == null || _page == null)
            {
                return;
            }

            double scale = _dpiScale <= 0 ? 1.0 : _dpiScale;
            int width = Math.Max(40, (int)Math.Round(dipSize.Width * scale));
            int height = Math.Max(40, (int)Math.Round(dipSize.Height * scale));
            form.Size = new Size(width, height);

            Rectangle work = Screen.GetWorkingArea(new Rectangle(_anchorRect.X, _anchorRect.Y, 1, 1));
            int left = _anchorRect.Right + 6 - width;
            left = Math.Max(work.Left + 4, left);
            int top = _anchorRect.Top - height - 2;
            top = Math.Max(work.Top + 4, top);
            form.Location = new Point(left, top);

            // 箭头对准 chevron 中心(相对卡片右缘的 DIP 偏移)
            int chevronCenterX = _anchorRect.X + _anchorRect.Width / 2;
            double offsetDips = Math.Max(4, (form.Right - chevronCenterX) / scale);
            _page.ArrowOffsetFromRight = offsetDips;

            form.Show();
            form.BringToFront();
        }

        #region 独立线程与生命周期

        private void EnsureThread()
        {
            if (_thread != null || _initFailed)
            {
                return;
            }

            lock (_gate)
            {
                if (_thread != null || _initFailed)
                {
                    return;
                }

                _thread = new Thread(ThreadMain)
                {
                    IsBackground = true,
                    Name = "TrayOverflowFlyout"
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                _ready.WaitOne(5000);
            }
        }

        private void ThreadMain()
        {
            try
            {
                // 新线程必须单独初始化 XAML 运行时,并把返回的 manager 保持存活
                _xamlManager = WindowsXamlManager.InitializeForCurrentThread();

                _form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    TopMost = true,
                    BackColor = Color.LimeGreen,
                    TransparencyKey = Color.LimeGreen,
                    Size = new Size(120, 80)
                };
                Helpers.SetNoActivate(_form.Handle);
                Helpers.HideFromAltTab(_form.Handle);

                _host = new WindowsXamlHost
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false
                };
                _form.Controls.Add(_host);

                _page = new TrayOverflowFlyoutPage();
                _page.OpenRequested += OnPageOpenRequested;
                _page.PinRequested += OnPagePinRequested;
                _host.Child = _page;

                _formHandle = _form.Handle;
                _ready.Set();

                Application.Run();
            }
            catch
            {
                _initFailed = true;
                _ready.Set();
            }
        }

        private void TryBeginInvoke(Action action)
        {
            Form form = _form;
            if (form == null || form.IsDisposed)
            {
                return;
            }

            try
            {
                form.BeginInvoke(action);
            }
            catch
            {
                // 线程正在退出时忽略
            }
        }

        public void Dispose()
        {
            UninstallMouseHook();

            Form form = _form;
            if (form != null && !form.IsDisposed)
            {
                try
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _form?.Hide();
                            _form?.Close();
                        }
                        catch
                        {
                            // 关闭失败也继续退出消息循环
                        }

                        try
                        {
                            Application.ExitThread();
                        }
                        catch
                        {
                            // 忽略
                        }
                    }));
                }
                catch
                {
                    // 线程已退出
                }

                if (_thread != null && !_thread.Join(2000))
                {
                    // 线程卡住时保持后台线程,不阻塞应用退出
                }
            }

            _page = null;
            _host = null;
            _form = null;
        }

        #endregion

        #region 事件转发回主线程

        private void OnPageOpenRequested(TrayIcon icon)
        {
            PostToMain(() => _openRequest?.Invoke(icon));
        }

        private void OnPagePinRequested(TrayIcon icon)
        {
            PostToMain(() => _pinRequest?.Invoke(icon));
        }

        private void PostToMain(Action action)
        {
            SynchronizationContext context = _mainSyncContext;
            if (context != null)
            {
                try
                {
                    context.Post(_ => action(), null);
                    return;
                }
                catch
                {
                    // 回退到直接调用
                }
            }

            action();
        }

        #endregion

        #region 点击外部关闭(不抢焦点,用低级鼠标钩子)

        private void InstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                return;
            }

            _mouseProc = MouseHookProc;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
        }

        private void UninstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Visible && lParam != IntPtr.Zero)
            {
                uint msg = (uint)wParam;
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    IntPtr handle = _formHandle;
                    if (handle != IntPtr.Zero && GetWindowRect(handle, out RECT rect))
                    {
                        bool insideFlyout = ms.pt.X >= rect.Left && ms.pt.X <= rect.Right &&
                                            ms.pt.Y >= rect.Top && ms.pt.Y <= rect.Bottom;
                        // 点击 chevron 本身时交给其 Click 切换(先关后开竞态),其余外部点击一律关闭
                        bool insideAnchor = _anchorRect.Contains(ms.pt.X, ms.pt.Y);
                        if (!insideFlyout && !insideAnchor)
                        {
                            PostToMain(Hide);
                        }
                    }
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        #endregion
    }
}
