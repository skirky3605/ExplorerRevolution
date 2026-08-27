using ExplorerRevolution.Data;
using System;
using ExplorerRevolution.Common.Tasks;
using ExplorerRevolution.Common;
using ExplorerRevolution.Common.Shell.Tray;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel.Email.DataProvider;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using static ExplorerRevolution.Common.WindowHelpers;
using TaskBarIcon = ExplorerRevolution.Data.TaskBarIcon;
using static ExplorerRevolution.Common.NativeMethods;
using System.ComponentModel;

namespace ExplorerRevolution.UI
{
    public sealed partial class TaskBar : Page
    {
        public TaskBar()
        {
            this.InitializeComponent();
            this.Unloaded += TaskBar_Unloaded;

            _ = LoadPinnedAppsAsync();

            // 订阅 TaskBarItemsControl 中的 ScrollViewer.ViewChanged 事件
            // 在 GridView 的容器生成后（Loaded）查找 ScrollViewer 并订阅事件
            TaskBarItemsControl.Loaded += (s, e) =>
            {
                taskBarScrollViewer = FindDescendant<ScrollViewer>(TaskBarItemsControl);
                if (taskBarScrollViewer != null)
                {
                    taskBarScrollViewer.ViewChanged += TaskBarScrollViewer_ViewChanged;
                    // 初始保存 offset
                    TaskBarScrollHorizontalOffset = taskBarScrollViewer.HorizontalOffset;
                }
            };
        }

        private void TaskBarGrid_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void TaskBarCenterStack_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCenterAlignment();
        }

        private void TaskBarCenterHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCenterAlignment();
        }

        /// <summary>
        /// 居中簇的对齐策略:能放下时整体居中;图标过多放不下时右缘贴住系统区,
        /// 整体向左扩展,避免一直往右堆叠/压到系统托盘。
        /// </summary>
        private void UpdateCenterAlignment()
        {
            if (TaskBarCenterStack == null || TaskBarCenterHost == null)
            {
                return;
            }

            // 居中基准是整条任务栏(屏幕中心),托盘只是叠在右侧;
            // 图标过多放不下时右缘贴住托盘左边界,整体向左扩展。
            // 居中放得下的最大宽度:簇右缘不能越过托盘左边界
            double available = TaskBarCenterHost.ActualWidth - _trayAreaWidth * 2 - 16;
            if (TaskBarCenterStack.ActualWidth > available)
            {
                TaskBarCenterStack.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
                TaskBarCenterStack.Margin = new Thickness(0, 0, _trayAreaWidth + 8, 0);
            }
            else
            {
                TaskBarCenterStack.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
                TaskBarCenterStack.Margin = new Thickness(0);
            }
        }

        public void SetHighlightButton(int index)
        {
            //if (index < TaskBarItemsControl.Children.Count() && index >= 0)
            //{

            //    ((TaskBarItemsControl.Children[index] as Grid).Children[0] as Button).ClearValue(Button.BackgroundProperty);
            //    var hostSel = (TaskBarItemsControl.Children[index] as Grid).Children[1] as Grid;
            //    var sbSel = hostSel?.Resources["AppStatus_Default"] as Storyboard;
            //    sbSel?.Begin();
            //}

            //for (int i = 0; i < TaskBarItemsControl.Children.Count(); i++)
            //{
            //    if(i != index)
            //    {
            //        ((TaskBarItemsControl.Children[i] as Grid).Children[0] as Button).Background = new SolidColorBrush(Colors.Transparent);
                    
            //        if(true) //最小化
            //        {
            //            var host = (TaskBarItemsControl.Children[i] as Grid).Children[1] as Grid;
            //            var sb = host?.Resources["AppStatus_DisabledVisible"] as Storyboard;
            //            sb?.Begin();
            //        }
            //        else //固定且关闭
            //        {
            //            var host = (TaskBarItemsControl.Children[i] as Grid).Children[1] as Grid;
            //            var sb = host?.Resources["AppStatus_DisabledHidden"] as Storyboard;
            //            sb?.Begin();
            //        }
            //    }
            //}
        }

        public int GetHighlightButton()
        {
            return -1;
        }

        public bool TbTitleVisibility = false;
        public async void RefreshTbPreferences()
        {
            for (int i = 0; i < taskBarApps.Count; i++)
            {
                taskBarApps[i].ButtonTitleVisibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed;
            }

            //Bindings.Update();
            taskBarApps.Move(0, 0);
        }

        public void AddAppButton(int index, BitmapImage imageSource, string appTitle)
        {
            //var stackPanel = new StackPanel
            //{
            //    Orientation = Orientation.Horizontal,
            //    Margin = new Thickness(5, 0, 5, 1),
            //};
            //stackPanel.Children.Add(new Image { Width = 26, Height = 26, Stretch = Stretch.Uniform, Margin = new Thickness(4, 4, 4, 4), Source = imageSource });
            //stackPanel.Children.Add(new TextBlock { Text = appTitle, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, FontFamily = new FontFamily("HarmonyOS Sans SC"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Visibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed } );
            //var appFrontButton = new Button
            //{
            //    Background = new SolidColorBrush(Colors.Transparent),
            //    BorderBrush = new SolidColorBrush(Colors.Transparent),
            //    Height = 40,
            //    HorizontalAlignment = HorizontalAlignment.Stretch,
            //    CornerRadius = new CornerRadius(8),
            //    Margin = new Thickness(2, 0, 2, 0),
            //    Opacity = 0.4
            //};
            //var appBackButton = new Button
            //{
            //    Background = new SolidColorBrush(Colors.Transparent),
            //    BorderBrush = new SolidColorBrush(Colors.Transparent),
            //    Height = 40,
            //    HorizontalAlignment = HorizontalAlignment.Stretch,
            //    CornerRadius = new CornerRadius(8),
            //    Margin = new Thickness(2, 0, 2, 0),
            //    Opacity = 1,
            //    IsTabStop = false
            //};
            //var appButtonGrid = new Grid
            //{
            //    Background = new SolidColorBrush(Colors.Transparent),
            //    BorderBrush = new SolidColorBrush(Colors.Transparent),
            //    Height = 40,
            //    HorizontalAlignment = HorizontalAlignment.Center,
            //    CornerRadius = new CornerRadius(8),
            //    Margin = new Thickness(0, 0, 0, 0),
            //};
            //var appStatusBar = new Grid
            //{
            //    Background = new SolidColorBrush(Colors.Transparent),
            //    Height = 3 ,
            //    Width = 8,
            //    CornerRadius = new CornerRadius(1.5),
            //    HorizontalAlignment = HorizontalAlignment.Center,
            //    VerticalAlignment = VerticalAlignment.Bottom,
            //    Margin = new Thickness(0, 0, 0, 1),
            //    Opacity = 1
            //    // Opacity Width Background
            //};
            //appStatusBar.BackgroundTransition = new BrushTransition { Duration = TimeSpan.FromMilliseconds(200) };
            //// 初始化三种可切换的视觉状态（仅初始化，不触发切换）
            //// 状态1: Background = AccentFillColorDefaultBrush, Width = 16, Opacity = 1
            //// 状态2: Background = AccentFillColorDisabledBrush, Width = 8,  Opacity = 1
            //// 状态3: Background = AccentFillColorDisabledBrush, Width = 4,  Opacity = 0
            //// 为了兼容只有 GoToState 可用的环境，将状态组附加到一个 Control（ContentControl）上

            //// 创建三个 storyboard（默认不自动开始），并把它们放入 statusHost.Resources 以便后续调用
            //Storyboard MakeStoryboard(object backgroundResourceKey, double width, double opacity)
            //{
            //    var storyBoard = new Storyboard { Duration = TimeSpan.FromMilliseconds(200) };

            //    var widthFrames = new DoubleAnimationUsingKeyFrames();
            //    widthFrames.KeyFrames.Add(new SplineDoubleKeyFrame
            //    {
            //        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)),
            //        Value = width
            //    });
            //    var widthAnim = new DoubleAnimation
            //    {
            //        To = width,
            //        Duration = TimeSpan.FromMilliseconds(200),
            //    };
            //    Storyboard.SetTarget(widthFrames, appStatusBar);
            //    Storyboard.SetTargetProperty(widthFrames, "Width");
            //    storyBoard.Children.Add(widthFrames); //当前无效

            //    var opacityFrames = new DoubleAnimationUsingKeyFrames();
            //    opacityFrames.KeyFrames.Add(new SplineDoubleKeyFrame
            //    {
            //        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)),
            //        Value = opacity
            //    });
            //    Storyboard.SetTarget(opacityFrames, appStatusBar);
            //    Storyboard.SetTargetProperty(opacityFrames, "Opacity");
            //    storyBoard.Children.Add(opacityFrames);

            //    var objAnim = new ObjectAnimationUsingKeyFrames();
            //    var discrete = new DiscreteObjectKeyFrame
            //    {
            //        KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
            //        Value = (Application.Current.Resources.ContainsKey(backgroundResourceKey)
            //            ? Application.Current.Resources[backgroundResourceKey]
            //            : Application.Current.Resources["AccentFillColorDisabledBrush"])
            //    };
            //    objAnim.KeyFrames.Add(discrete);
            //    Storyboard.SetTarget(objAnim, appStatusBar);
            //    Storyboard.SetTargetProperty(objAnim, "Background");
            //    storyBoard.Children.Add(objAnim);

            //    return storyBoard;
            //}

            //appStatusBar.Resources["AppStatus_Default"] = MakeStoryboard("AccentFillColorDefaultBrush", 16, 1);
            //appStatusBar.Resources["AppStatus_DisabledVisible"] = MakeStoryboard("AccentFillColorDisabledBrush", 8, 1);
            //appStatusBar.Resources["AppStatus_DisabledHidden"] = MakeStoryboard("AccentFillColorDisabledBrush", 4, 0);


            //appFrontButton.Click += AppFrontButton_Click;

            //appButtonGrid.Children.Add(appBackButton);
            //appButtonGrid.Children.Add(appStatusBar);
            //appButtonGrid.Children.Add(stackPanel);
            //appButtonGrid.Children.Add(appFrontButton);
            //appButtonGrid.Transitions.Add(new RepositionThemeTransition());
            //TaskBarItemsControl.Children.Insert(index, appButtonGrid);
        }

        private void AppFrontButton_Click(object sender, RoutedEventArgs e)
        {
            //int index = TaskBarItemsControl.Children.IndexOf(((sender as Button).Parent as Grid));
            //SetHighlightButton(index);
        }

        private void Button_TbRbTimeArea_Front_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_TbRbStatusArea_Front_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Button_TbRbShowDskArea_Front_Click(object sender, RoutedEventArgs e)
        {
            ToggleDesktop();
        }

        private void Button_TbRbBkgAppArea_Front_Click(object sender, RoutedEventArgs e)
        {
            ShowTrayOverflow();
        }

        private void Button_TbRbKeyBoardArea_Front_Click(object sender, RoutedEventArgs e)
        {

        }
        ObservableCollection<TaskBarIcon> taskBarIcons = new(); // 全部窗口(任务视图用)
        ObservableCollection<TaskBarApp> taskBarApps = new();    // 任务栏按钮(按应用分组)
        private readonly Dictionary<IntPtr, TaskBarIcon> taskBarIconMap = new();
        private readonly Dictionary<string, TaskBarApp> taskBarAppMap = new(StringComparer.OrdinalIgnoreCase);
        private TaskViewPopupForm _windowPickerPopup;
        private double _trayAreaWidth;

        private WindowsTasksService _tasksService;
        private TrayNotificationArea _trayArea;
        private readonly Dictionary<TrayIcon, TrayIconUiHost> _trayIconUis = new();
        private ITrayOverflowHost _trayOverflow;
        private bool _usingFlyoutHost;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _imeTimer;
        private bool _trayInitialized;

        private void TaskBarItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            _tasksService = new WindowsTasksService();
            _tasksService.Windows.CollectionChanged += TasksService_WindowsChanged;
            _tasksService.DesktopActivated += (s, args) =>
            {
                foreach (var icon in taskBarIcons)
                {
                    icon.IsForeground = false;
                }

                foreach (var app in taskBarApps)
                {
                    app.IsForeground = false;
                }
            };
            _tasksService.Initialize(withMultiMonTracking: false);

            InitializeTray();
            StartClockTimer();
            StartImeTimer();
        }

        private void TasksService_WindowsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TaskbarWindow win in e.NewItems)
                {
                    AddTaskbarWindow(win);
                }
            }

            if (e.OldItems != null)
            {
                foreach (TaskbarWindow win in e.OldItems)
                {
                    RemoveTaskbarWindow(win);
                }
            }
        }

        private void AddTaskbarWindow(TaskbarWindow win)
        {
            var icon = new TaskBarIcon
            {
                IntPtr = win.Handle,
                Title = win.Title,
                IsActive = Visibility.Visible, // 窗口存在
                IsForeground = win.State == TaskbarWindow.WindowState.Active, // 在前台
                ButtonTitleVisibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed
            };

            win.PropertyChanged += TaskbarWindow_PropertyChanged;
            taskBarIconMap[win.Handle] = icon;

            // 只显示通过过滤的窗口(cloaked / 壳窗口 / 不可见不显示)。
            // 关键:UWP 启动瞬间会产生临时的 CoreWindow(创建后即被销毁),若不在此过滤,
            // 它会被当成第二个按钮显示,启动完成后又消失——即"启动时两个图标"。
            if (win.ShowInTaskbar && !taskBarIcons.Contains(icon))
            {
                taskBarIcons.Add(icon);
                AddWindowToGroup(win, icon);
                LoadIcon(win, icon, FindGroupForWindow(win));
            }
        }

        private void LoadIcon(TaskbarWindow win, TaskBarIcon icon, TaskBarApp app = null)
        {
            if (icon.Icon != null)
            {
                return;
            }

            // 图标转换必须在 UWP UI 线程进行(BitmapImage.SetSourceAsync)
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var bitmap = await GetWindowIconAsync(win.Handle);
                icon.Icon = bitmap;
                win.Icon = bitmap;

                if (app != null && app.Icon == null)
                {
                    app.Icon = bitmap;
                }
            });
        }

        private void RemoveTaskbarWindow(TaskbarWindow win)
        {
            if (taskBarIconMap.TryGetValue(win.Handle, out var icon))
            {
                taskBarIconMap.Remove(win.Handle);
                win.PropertyChanged -= TaskbarWindow_PropertyChanged;
                taskBarIcons.Remove(icon);
                RemoveWindowFromGroup(win, icon);
            }
        }

        private void TaskbarWindow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var win = (TaskbarWindow)sender;
            if (!taskBarIconMap.TryGetValue(win.Handle, out var icon))
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(TaskbarWindow.Title):
                    icon.Title = win.Title;
                    break;

                case nameof(TaskbarWindow.State):
                    // 激活/非激活/闪烁状态
                    icon.IsForeground = win.State == TaskbarWindow.WindowState.Active;
                    var app = FindGroupForWindow(win);
                    if (app != null)
                    {
                        UpdateGroupForeground(app);
                    }
                    break;

                case nameof(TaskbarWindow.ShowInTaskbar):
                    // cloaked / 壳窗口过滤结果变化时,同步按钮可见性
                    if (win.ShowInTaskbar)
                    {
                        if (!taskBarIcons.Contains(icon))
                        {
                            taskBarIcons.Add(icon);
                            AddWindowToGroup(win, icon);
                            LoadIcon(win, icon, FindGroupForWindow(win));
                        }
                    }
                    else
                    {
                        taskBarIcons.Remove(icon);
                        RemoveWindowFromGroup(win, icon);
                    }
                    break;
            }
        }

        // ===== 应用分组(一个应用一个按钮)=====

        private static string AumidKey(string aumid) => string.IsNullOrEmpty(aumid) ? null : "aumid:" + aumid;

        private static string ExeKey(string exePath) => string.IsNullOrEmpty(exePath) ? null : "exe:" + exePath.ToLowerInvariant();

        private void AddWindowToGroup(TaskbarWindow win, TaskBarIcon icon)
        {
            var app = FindOrCreateGroup(win);
            app.Windows.Add(icon);
            app.IsActive = Visibility.Visible;
            UpdateGroupForeground(app);
        }

        private void RemoveWindowFromGroup(TaskbarWindow win, TaskBarIcon icon)
        {
            var app = FindGroupForWindow(win);
            if (app == null)
            {
                return;
            }

            app.Windows.Remove(icon);

            if (app.Windows.Count == 0)
            {
                if (app.IsPinned)
                {
                    // 固定应用:保留按钮,只清除运行状态
                    app.IsActive = Visibility.Collapsed;
                    app.IsForeground = false;
                }
                else
                {
                    taskBarApps.Remove(app);
                    RemoveGroupKeys(app);
                }
            }
            else
            {
                UpdateGroupForeground(app);
            }
        }

        private TaskBarApp FindOrCreateGroup(TaskbarWindow win)
        {
            var existing = FindGroupForWindow(win);
            if (existing != null)
            {
                return existing;
            }

            string aumid = win.AppUserModelID;
            string exe = win.WinFileName;

            var app = new TaskBarApp
            {
                Key = AumidKey(aumid) ?? ExeKey(exe) ?? "hwnd:" + win.Handle,
                Title = GetAppTitle(win),
                ButtonTitleVisibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed
            };

            if (!string.IsNullOrEmpty(aumid))
            {
                taskBarAppMap[AumidKey(aumid)] = app;
            }

            if (!string.IsNullOrEmpty(exe))
            {
                taskBarAppMap[ExeKey(exe)] = app;
            }

            taskBarApps.Add(app);
            return app;
        }

        private TaskBarApp FindGroupForWindow(TaskbarWindow win)
        {
            string aumid = win.AppUserModelID;
            if (!string.IsNullOrEmpty(aumid) && taskBarAppMap.TryGetValue(AumidKey(aumid), out var byAumid))
            {
                return byAumid;
            }

            string exe = win.WinFileName;
            if (!string.IsNullOrEmpty(exe) && taskBarAppMap.TryGetValue(ExeKey(exe), out var byExe))
            {
                return byExe;
            }

            return null;
        }

        private void UpdateGroupForeground(TaskBarApp app)
        {
            app.IsForeground = app.Windows.Any(w => w.IsForeground);
        }

        private void RemoveGroupKeys(TaskBarApp app)
        {
            var keys = taskBarAppMap.Where(kv => kv.Value == app).Select(kv => kv.Key).ToList();
            foreach (var key in keys)
            {
                taskBarAppMap.Remove(key);
            }
        }

        private static string GetAppTitle(TaskbarWindow win)
        {
            // UWP 窗口用窗口标题(如 "Apple Music"),避免显示 ApplicationFrameHost
            if (win.IsUWP)
            {
                return win.Title;
            }

            try
            {
                if (!string.IsNullOrEmpty(win.WinFileDescription))
                {
                    return win.WinFileDescription;
                }
            }
            catch { }

            return win.Title;
        }

        private void TaskBar_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_tasksService != null)
                {
                    _tasksService.Windows.CollectionChanged -= TasksService_WindowsChanged;
                    _tasksService.Dispose();
                    _tasksService = null;
                }

                taskBarIconMap.Clear();
                taskBarIcons.Clear();
                taskBarAppMap.Clear();
                taskBarApps.Clear();

                if (_clockTimer != null)
                {
                    _clockTimer.Stop();
                    _clockTimer = null;
                }

                if (_imeTimer != null)
                {
                    _imeTimer.Stop();
                    _imeTimer = null;
                }

                if (_trayArea != null)
                {
                    _trayArea.Icons.CollectionChanged -= TrayIcons_CollectionChanged;
                    _trayArea.BalloonShown -= OnTrayBalloonShown;

                    foreach (TrayIconUiHost host in _trayIconUis.Values)
                    {
                        DetachTrayIconUi(host);
                    }

                    _trayIconUis.Clear();
                    _trayArea.Dispose();
                    _trayArea = null;
                }

                if (_trayOverflow != null)
                {
                    _trayOverflow.Dispose();
                    _trayOverflow = null;
                }

                _trayInitialized = false;
            }
            catch { }
        }

        public static async Task<BitmapImage> GetWindowIconAsync(IntPtr hWnd)
        {
            // 判断是否 UWP 窗口
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);
            string cls = className.ToString();

            // UWP 应用的任务栏窗口可能是 ApplicationFrameWindow(ApplicationFrameHost 宿主)
            // 或 Windows.UI.Core.CoreWindow(应用进程),两者都带 AppUserModelID
            var aumid = GetAppUserModelId(hWnd);
            if ((cls == "ApplicationFrameWindow" || cls == "Windows.UI.Core.CoreWindow") && !string.IsNullOrEmpty(aumid))
            {
                var icon = await GetUwpAppIconAsync(aumid);
                if (icon != null) return icon;
            }

            // 普通 Win32 窗口走原来的路径
            var hIcon = GetWindowIcon(hWnd);
            return await HIconToBitmapImageAsync(hIcon);
        }

        public static async Task<BitmapImage> HIconToBitmapImageAsync(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                //用 GDI 把 HICON 编码成 PNG 字节
                var icon = Icon.FromHandle(hIcon);
                var bitmap = icon.ToBitmap();
                var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                // 转为 UWP BitmapImage
                var bitmapImage = new BitmapImage();
                var ras = ms.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(ras);
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        private void TaskBarItemsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //不要用changed，用clicked
        }

        private void TaskBarItemsControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            // 兜底路径:若点击未被子级按钮吞掉,则直接切换
            if (e.ClickedItem is TaskBarApp app)
            {
                TaskBarApp_Click(app);
            }
        }

        private void TaskbarButton_Click(object sender, RoutedEventArgs e)
        {
            // DataTemplate 内按钮的 DataContext 即当前任务栏按钮数据
            if ((sender as FrameworkElement)?.DataContext is TaskBarApp app)
            {
                TaskBarApp_Click(app);
            }
        }

        private void TaskBarApp_Click(TaskBarApp app)
        {
            if (app.Windows.Count == 0)
            {
                // 固定但未运行 → 启动
                LaunchApp(app);
            }
            else if (app.Windows.Count == 1)
            {
                // 单窗口 → 切换 / 再点最小化
                ActivateWindow(app.Windows[0]);
            }
            else
            {
                // 多窗口 → 在该按钮上方弹出窗口选择列表
                ShowAppWindowPicker(app);
            }
        }

        private static void LaunchApp(TaskBarApp app)
        {
            try
            {
                if (!string.IsNullOrEmpty(app.AppUserModelId))
                {
                    // MSIX/UWP:通过 shell:AppsFolder 启动
                    Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{app.AppUserModelId}") { UseShellExecute = true });
                }
                else if (!string.IsNullOrEmpty(app.ShortcutPath))
                {
                    Process.Start(new ProcessStartInfo(app.ShortcutPath) { UseShellExecute = true });
                }
            }
            catch
            {
                // 启动失败静默处理
            }
        }

        private void ShowAppWindowPicker(TaskBarApp app)
        {
            if (_windowPickerPopup == null)
            {
                _windowPickerPopup = new TaskViewPopupForm(app.Title);
                _windowPickerPopup.FormClosed += (s, args) => _windowPickerPopup = null;
            }

            _windowPickerPopup.SetWindows(app.Windows, ActivateWindow, app.Title);

            var taskbar = ShellContext.TaskBarFormInstance;
            if (taskbar == null || taskbar.IsDisposed)
            {
                return;
            }

            // 定位到该按钮正上方(GetOffsetOfTbIndex 返回相对任务栏的 X)
            int index = taskBarApps.IndexOf(app);
            double offsetX = index >= 0 ? GetOffsetOfTbIndex(index) : -1;
            int left = offsetX > 0 ? (int)(taskbar.Left + offsetX) : taskbar.Left + 8;

            var screen = Screen.FromControl(taskbar).WorkingArea;
            left = Math.Max(screen.Left, Math.Min(left, screen.Right - _windowPickerPopup.Width));

            _windowPickerPopup.StartPosition = FormStartPosition.Manual;
            _windowPickerPopup.Location = new System.Drawing.Point(left, taskbar.Top - _windowPickerPopup.Height);
            _windowPickerPopup.Show();
            _windowPickerPopup.BringToFront();
            _windowPickerPopup.Activate();
        }

        private void ActivateWindow(TaskBarIcon icon)
        {
            var win = _tasksService?.Windows.FirstOrDefault(w => w.Handle == icon.IntPtr);
            if (win == null)
            {
                return;
            }

            // 任务栏窗体已设置 WS_EX_NOACTIVATE,点击任务栏不会夺取焦点,
            // 因此 GetForegroundWindow 拿到的就是用户正在操作的窗口(壳自身激活事件也会被忽略)。
            bool isForeground = GetForegroundWindow() == win.Handle;

            if (isForeground && !win.IsMinimized)
            {
                // 点击当前前台窗口 → 最小化
                win.Minimize();
            }
            else
            {
                // 点击后台 / 最小化窗口 → 还原并激活(同时清除闪烁状态)
                win.BringToFront();
            }
        }

        // ===== 开始菜单 =====

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // 暂时呼出系统原版开始菜单
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ===== 任务视图 =====

        private void TaskViewButton_Click(object sender, RoutedEventArgs e)
        {
            // 暂时呼出系统原版任务视图(Win+Tab)
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>预留系统托盘区宽度(挂载原生托盘后调用),居中簇在其左侧居中。</summary>
        public void SetTrayAreaWidth(int pixels)
        {
            if (pixels > 0)
            {
                _trayAreaWidth = pixels;
                UpdateCenterAlignment();
            }
        }

        // ===== 固定图标 =====

        private async Task LoadPinnedAppsAsync()
        {
            try
            {
                var apps = PinnedAppsService.GetPinnedApps();

                foreach (var app in apps)
                {
                    BitmapImage icon = null;

                    if (app.IsUwp && !string.IsNullOrEmpty(app.AppUserModelId))
                    {
                        // MSIX 固定图标:manifest PNG(裁剪透明留白,与 Win32 图标视觉一致)
                        try
                        {
                            var iconPath = ResolveUwpIconPath(app.AppUserModelId);
                            if (!string.IsNullOrEmpty(iconPath))
                            {
                                icon = await LoadUwpIconBitmapImageAsync(iconPath);
                            }
                        }
                        catch
                        {
                            // 图标解析失败则显示无图标条目
                        }
                    }
                    else
                    {
                        // Win32 固定图标:从目标 exe / IconLocation 取图标,避免快捷方式小箭头
                        IntPtr hIcon = GetShortcutIcon(app.ShortcutPath);
                        icon = await HIconToBitmapImageAsync(hIcon);
                    }

                    // 分组键:.lnk 的 AUMID 属性优先,否则目标 exe / IconLocation 路径
                    var keys = new List<string>();
                    if (!string.IsNullOrEmpty(app.AppUserModelId))
                    {
                        keys.Add(AumidKey(app.AppUserModelId));
                    }

                    string lnkAumid = GetShortcutAppUserModelId(app.ShortcutPath);
                    if (!string.IsNullOrEmpty(lnkAumid))
                    {
                        keys.Add(AumidKey(lnkAumid));
                    }

                    string targetPath = GetShortcutTargetPath(app.ShortcutPath);
                    if (!string.IsNullOrEmpty(targetPath))
                    {
                        keys.Add(ExeKey(targetPath));
                    }

                    if (keys.Count == 0)
                    {
                        keys.Add(ExeKey(app.ShortcutPath));
                    }

                    // 已存在的分组(该应用已在运行)→ 合并为固定按钮
                    TaskBarApp existing = keys
                        .Where(k => k != null)
                        .Select(k => taskBarAppMap.TryGetValue(k, out var a) ? a : null)
                        .FirstOrDefault(a => a != null);

                    if (existing != null)
                    {
                        existing.IsPinned = true;
                        existing.Title = app.Name;
                        existing.ShortcutPath = app.ShortcutPath;
                        existing.AppUserModelId = app.AppUserModelId;
                        if (icon != null)
                        {
                            existing.Icon = icon;
                        }

                        foreach (var k in keys)
                        {
                            if (k != null)
                            {
                                taskBarAppMap[k] = existing;
                            }
                        }

                        continue;
                    }

                    var ta = new TaskBarApp
                    {
                        Key = keys.FirstOrDefault(k => k != null) ?? "pinned:" + app.Name,
                        Title = app.Name,
                        ShortcutPath = app.ShortcutPath,
                        AppUserModelId = app.AppUserModelId,
                        Icon = icon,
                        IsPinned = true,
                        ButtonTitleVisibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed
                    };

                    // 固定项插在固定区末尾(始终在未固定的运行应用之前)
                    int pinnedIndex = taskBarApps.Count(a => a.IsPinned);
                    taskBarApps.Insert(pinnedIndex, ta);

                    foreach (var k in keys)
                    {
                        if (k != null)
                        {
                            taskBarAppMap[k] = ta;
                        }
                    }
                }
            }
            catch
            {
                // 固定图标加载失败不阻塞任务栏
            }
        }

        public double GetOffsetOfTbIndex(int index) // 相对整个任务栏的坐标，用上ScrollViewer的Offset
        {
            // 获取被点击项的索引并输出
            //Debug.WriteLine($"Clicked item index: {index}");

            var itemContainer = TaskBarItemsControl.ContainerFromIndex(index) as GridViewItem;
            if (itemContainer == null)
            {
                //Debug.WriteLine("Item container is null (可能尚未生成)");
                return -1;
            }

            // 尝试获取 ItemsPanel（你的 ItemsPanelTemplate 使用的是 StackPanel）
            var itemsPanel = TaskBarItemsControl.ItemsPanelRoot as Windows.UI.Xaml.Controls.Panel;

            // 确保有 ScrollViewer 引用
            if (taskBarScrollViewer == null)
            {
                taskBarScrollViewer = FindDescendant<ScrollViewer>(TaskBarItemsControl);
            }

            if (itemsPanel != null)
            {
                // item 在 itemsPanel（内容坐标系）中的位置
                GeneralTransform itemToItemsPanel = itemContainer.TransformToVisual(itemsPanel);
                Windows.Foundation.Point contentPos = itemToItemsPanel.TransformPoint(new Windows.Foundation.Point(0, 0));

                double scrollOffset = taskBarScrollViewer?.HorizontalOffset ?? 0.0;

                // itemsPanel 相对于整个 TaskBarGrid 的偏移（例如 ScrollViewer 放置位置）
                double itemsPanelOffsetX = 0.0;
                try
                {
                    var itemsPanelToTaskBar = itemsPanel.TransformToVisual(TaskBarGrid);
                    var p = itemsPanelToTaskBar.TransformPoint(new Windows.Foundation.Point(0, 0));
                    itemsPanelOffsetX = p.X;
                }
                catch
                {
                    itemsPanelOffsetX = 0.0;
                }

                // 计算相对于整个任务栏（TaskBarGrid）的 X：内容坐标 - 滚动偏移 + itemsPanel 在 TaskBarGrid 的偏移
                double finalX = contentPos.X - scrollOffset + itemsPanelOffsetX;
                //Debug.WriteLine($"contentPos.X={contentPos.X}, scrollOffset={scrollOffset}, itemsPanelOffsetX={itemsPanelOffsetX}, finalX={finalX}");
                return finalX;
            }

            // 退回到相对于整个 GridView 的坐标（不包含额外的 scroll 计算）
            GeneralTransform transform = itemContainer.TransformToVisual(TaskBarItemsControl);
            Windows.Foundation.Point position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            //Debug.WriteLine($"Fallback GridView-relative X={position.X}");
            return position.X;
        }

        // ScrollViewer 及偏移量，用于保存当前滚动位置
        private ScrollViewer taskBarScrollViewer;
        private double TaskBarScrollHorizontalOffset = 0.0;

        // 查找可视树中指定类型的后代元素（递归）
        private T FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindDescendant<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void TaskBarScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (taskBarScrollViewer != null)
            {
                TaskBarScrollHorizontalOffset = taskBarScrollViewer.HorizontalOffset;
                //Debug.WriteLine($"TaskBar Scroll HorizontalOffset = {TaskBarScrollHorizontalOffset}");

                if (TaskBarScrollHorizontalOffset > 220)
                {
                    //将开始按钮独立放置

                }
            }
        }

        // ===== 自研托盘 =====

        private void InitializeTray()
        {
            if (_trayInitialized || TrayIconsPanel == null)
            {
                return;
            }

            _trayInitialized = true;

            _trayArea = new TrayNotificationArea
            {
                PlacementProvider = GetTrayIconScreenRect
            };
            _trayArea.Icons.CollectionChanged += TrayIcons_CollectionChanged;
            _trayArea.BalloonShown += OnTrayBalloonShown;
            _trayArea.Initialize();
        }

        private void TrayIcons_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (e.NewItems != null)
                {
                    foreach (TrayIcon icon in e.NewItems)
                    {
                        // 任务栏只显示常驻(pinned)图标,其余进溢出面板
                        if (icon.IsPinned)
                        {
                            AddTrayIconUi(icon);
                        }
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (TrayIcon icon in e.OldItems)
                    {
                        RemoveTrayIconUi(icon);
                    }
                }

                UpdateTrayIconPlacements();
                RefreshTrayOverflow();
            }
            catch
            {
                // 图标集合变化处理失败不影响任务栏主体
            }
        }

        private void AddTrayIconUi(TrayIcon icon)
        {
            if (_trayIconUis.ContainsKey(icon))
            {
                return;
            }

            var image = new Windows.UI.Xaml.Controls.Image
            {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false
            };

            var host = new TrayIconUiHost
            {
                Width = 30,
                Height = 48,
                Background = new SolidColorBrush(Colors.Transparent),
                Icon = icon
            };
            host.Children.Add(image);

            var unpinButton = new Windows.UI.Xaml.Controls.Button
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right,
                VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 3, 0),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(4),
                Visibility = Visibility.Collapsed,
                Content = new FontIcon
                {
                    FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 9,
                    Glyph = "\uE711"
                },
                Tag = icon
            };
            ToolTipService.SetToolTip(unpinButton, "从任务栏取消固定");
            unpinButton.PointerPressed += (s, pe) => pe.Handled = true;
            unpinButton.Click += UnpinButton_Click;
            host.Children.Add(unpinButton);
            host.Image = image;
            host.UnpinButton = unpinButton;

            icon.PropertyChanged += TrayIcon_PropertyChanged;
            host.PointerPressed += TrayIconRoot_PointerPressed;
            host.PointerReleased += TrayIconRoot_PointerReleased;
            host.PointerEntered += TrayIconRoot_PointerEntered;
            host.PointerExited += TrayIconRoot_PointerExited;
            host.PointerMoved += TrayIconRoot_PointerMoved;
            host.PointerCaptureLost += TrayIconRoot_PointerCaptureLost;
            host.PointerCanceled += TrayIconRoot_PointerCanceled;

            _trayIconUis[icon] = host;
            TrayIconsPanel.Children.Add(host);

            UpdateTrayIconUi(host);
        }

        private void RemoveTrayIconUi(TrayIcon icon)
        {
            if (!_trayIconUis.TryGetValue(icon, out TrayIconUiHost host))
            {
                return;
            }

            DetachTrayIconUi(host);
            _trayIconUis.Remove(icon);
            TrayIconsPanel.Children.Remove(host);
        }

        private void DetachTrayIconUi(TrayIconUiHost host)
        {
            host.Icon.PropertyChanged -= TrayIcon_PropertyChanged;
            host.PointerPressed -= TrayIconRoot_PointerPressed;
            host.PointerReleased -= TrayIconRoot_PointerReleased;
            host.PointerEntered -= TrayIconRoot_PointerEntered;
            host.PointerExited -= TrayIconRoot_PointerExited;
            host.PointerMoved -= TrayIconRoot_PointerMoved;
            host.PointerCaptureLost -= TrayIconRoot_PointerCaptureLost;
            host.PointerCanceled -= TrayIconRoot_PointerCanceled;
            host.UnpinButton.Click -= UnpinButton_Click;
        }

        private void TrayIcon_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!(sender is TrayIcon icon))
            {
                return;
            }

            if (e.PropertyName == nameof(TrayIcon.IsPinned))
            {
                if (icon.IsPinned)
                {
                    if (!_trayIconUis.ContainsKey(icon))
                    {
                        AddTrayIconUi(icon);
                        UpdateTrayIconPlacements();
                    }
                }
                else
                {
                    RemoveTrayIconUi(icon);
                }

                RefreshTrayOverflow();
                return;
            }

            if (_trayIconUis.TryGetValue(icon, out TrayIconUiHost host))
            {
                UpdateTrayIconUi(host);
            }
        }

        private void UpdateTrayIconUi(TrayIconUiHost host)
        {
            try
            {
                ToolTipService.SetToolTip(host, string.IsNullOrEmpty(host.Icon.Title) ? host.Icon.Identifier : host.Icon.Title);

                if (host.Icon.HIcon != IntPtr.Zero && host.LoadedHIcon != host.Icon.HIcon)
                {
                    host.LoadedHIcon = host.Icon.HIcon;
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            var bitmap = await HIconToBitmapImageAsync(host.Icon.HIcon);
                            if (bitmap != null && _trayIconUis.TryGetValue(host.Icon, out TrayIconUiHost current) && current == host)
                            {
                                host.Image.Source = bitmap;
                            }
                            else
                            {
                                host.LoadedHIcon = IntPtr.Zero;
                            }
                        }
                        catch
                        {
                            // 图标解码失败时保留空占位
                            host.LoadedHIcon = IntPtr.Zero;
                        }
                    });
                }
            }
            catch
            {
                // 更新失败静默
            }
        }

        private void TrayIconRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is TrayIconUiHost host) || host.Icon == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(host);
            TrayMouseButton button;

            if (point.Properties.IsRightButtonPressed)
            {
                button = TrayMouseButton.Right;
            }
            else if (point.Properties.IsMiddleButtonPressed)
            {
                button = TrayMouseButton.Middle;
            }
            else
            {
                button = TrayMouseButton.Left;
            }

            host.PressedButton = button;
            host.CapturePointer(e.Pointer);
            host.Icon.IconMouseDown(button);
            e.Handled = true;
        }

        private void TrayIconRoot_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is TrayIconUiHost host) || host.Icon == null)
            {
                return;
            }

            TrayMouseButton button = host.PressedButton ?? TrayMouseButton.Left;

            host.PressedButton = null;
            host.ReleasePointerCapture(e.Pointer);
            host.Icon.IconMouseUp(button);

            e.Handled = true;
        }

        private void TrayIconRoot_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TrayIconUiHost host)
            {
                host.PressedButton = null;
            }
        }

        private void TrayIconRoot_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TrayIconUiHost host)
            {
                host.PressedButton = null;
            }
        }

        private void TrayIconRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TrayIconUiHost host)
            {
                host.UnpinButton.Visibility = Visibility.Visible;
                if (host.Icon != null)
                {
                    host.Icon.IconMouseEnter();
                }
            }
        }

        private void TrayIconRoot_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TrayIconUiHost host)
            {
                host.UnpinButton.Visibility = Visibility.Collapsed;
                if (host.Icon != null)
                {
                    host.Icon.IconMouseLeave();
                }
            }
        }

        private void TrayIconRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TrayIconUiHost host && host.Icon != null)
            {
                host.Icon.IconMouseMove();
            }
        }

        private void UnpinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Windows.UI.Xaml.Controls.Button button && button.Tag is TrayIcon icon)
            {
                _trayArea?.SetPinned(icon, false);
            }
        }

        // ===== 托盘溢出面板(隐藏/非常驻图标)=====

        private void ShowTrayOverflow()
        {
            try
            {
                if (_trayArea == null || Button_TbRbBkgAppArea_Front == null)
                {
                    return;
                }

                if (_trayOverflow == null)
                {
                    _trayOverflow = new TrayOverflowFlyoutWindow();
                    _usingFlyoutHost = true;
                }

                if (_trayOverflow.Visible)
                {
                    _trayOverflow.Hide();
                    return;
                }

                var icons = _trayArea.Icons.Where(i => !i.IsPinned).ToList();
                if (icons.Count == 0)
                {
                    return;
                }

                Rectangle? anchor = GetTrayOverflowAnchorRect();
                if (!anchor.HasValue)
                {
                    return;
                }

                float dpiScale = GetTaskBarDpiScale();
                _trayOverflow.ShowOverflow(
                    anchor.Value,
                    dpiScale,
                    icons,
                    OpenTrayIconFromOverflow,
                    PinTrayIconFromOverflow);

                // 独立线程 XAML Island 初始化失败时回退旧 WinForms 浮窗,保证功能可用
                if (_usingFlyoutHost && !_trayOverflow.IsAvailable)
                {
                    _trayOverflow.Dispose();
                    _trayOverflow = new TrayOverflowPopupForm();
                    _usingFlyoutHost = false;
                    _trayOverflow.ShowOverflow(
                        anchor.Value,
                        dpiScale,
                        icons,
                        OpenTrayIconFromOverflow,
                        PinTrayIconFromOverflow);
                }
            }
            catch
            {
                // 溢出面板打开失败不影响任务栏
            }
        }

        private void RefreshTrayOverflow()
        {
            try
            {
                if (_trayOverflow != null && _trayOverflow.Visible && _trayArea != null)
                {
                    _trayOverflow.RefreshIcons(_trayArea.Icons.Where(i => !i.IsPinned).ToList());
                }
            }
            catch
            {
                // 刷新失败忽略
            }
        }

        private void OpenTrayIconFromOverflow(TrayIcon icon)
        {
            if (icon == null)
            {
                return;
            }

            _trayOverflow?.Hide();
            icon.IconMouseDown(TrayMouseButton.Left);
            icon.IconMouseUp(TrayMouseButton.Left);
        }

        private void PinTrayIconFromOverflow(TrayIcon icon)
        {
            _trayArea?.SetPinned(icon, true);
        }

        private Rectangle? GetTrayOverflowAnchorRect()
        {
            try
            {
                var transform = Button_TbRbBkgAppArea_Front.TransformToVisual(null);
                var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                var form = ShellContext.TaskBarFormInstance;
                if (form == null || form.IsDisposed)
                {
                    return null;
                }

                return new Rectangle(
                    form.Left + (int)Math.Round(origin.X),
                    form.Top + (int)Math.Round(origin.Y),
                    (int)Math.Round(Button_TbRbBkgAppArea_Front.ActualWidth),
                    (int)Math.Round(Button_TbRbBkgAppArea_Front.ActualHeight));
            }
            catch
            {
                return null;
            }
        }

        private float GetTaskBarDpiScale()
        {
            try
            {
                var form = ShellContext.TaskBarFormInstance;
                if (form == null || form.IsDisposed)
                {
                    return 1f;
                }

                using (var g = form.CreateGraphics())
                {
                    return g.DpiX / 96f;
                }
            }
            catch
            {
                return 1f;
            }
        }

        private Rectangle? GetTrayIconScreenRect(TrayIcon icon)
        {
            if (!_trayIconUis.TryGetValue(icon, out TrayIconUiHost host) || host.ActualWidth <= 0)
            {
                return null;
            }

            var form = ShellContext.TaskBarFormInstance;
            if (form == null || form.IsDisposed)
            {
                return null;
            }

            try
            {
                var transform = host.TransformToVisual(null);
                var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                return new Rectangle(
                    form.Left + (int)Math.Round(origin.X),
                    form.Top + (int)Math.Round(origin.Y),
                    (int)Math.Round(host.ActualWidth),
                    (int)Math.Round(host.ActualHeight));
            }
            catch
            {
                return null;
            }
        }

        private void UpdateTrayIconPlacements()
        {
            foreach (TrayIconUiHost host in _trayIconUis.Values)
            {
                Rectangle? rect = GetTrayIconScreenRect(host.Icon);
                if (rect.HasValue)
                {
                    host.Icon.Placement = rect.Value;
                }
            }
        }

        private void OnTrayBalloonShown(object sender, TrayBalloonEventArgs e)
        {
            if (e.Icon == null || !_trayIconUis.TryGetValue(e.Icon, out TrayIconUiHost host))
            {
                return;
            }

            string balloonTip = string.IsNullOrEmpty(e.Info) ? e.Title : e.Title + "\n" + e.Info;
            ToolTipService.SetToolTip(host, balloonTip);

            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, async () =>
            {
                await Task.Delay(8000);
                if (_trayIconUis.TryGetValue(e.Icon, out TrayIconUiHost current))
                {
                    ToolTipService.SetToolTip(current, string.IsNullOrEmpty(current.Icon.Title) ? current.Icon.Identifier : current.Icon.Title);
                }
            });
        }

        private void TaskBarRightBottomStack_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _trayAreaWidth = e.NewSize.Width;
            UpdateCenterAlignment();
            UpdateTrayIconPlacements();
        }

        // ===== 时钟 =====

        private void StartClockTimer()
        {
            if (_clockTimer != null)
            {
                return;
            }

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        private void UpdateClock()
        {
            try
            {
                if (TbRbTime != null)
                {
                    TbRbTime.Text = DateTime.Now.ToString("HH:mm");
                }

                if (TbRbDate != null)
                {
                    TbRbDate.Text = DateTime.Now.ToString("yyyy/M/d");
                }
            }
            catch
            {
                // 时钟更新失败静默
            }
        }

        // ===== 输入法指示器 =====

        private void StartImeTimer()
        {
            if (_imeTimer != null)
            {
                return;
            }

            _imeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _imeTimer.Tick += (s, e) => UpdateImeIndicator();
            _imeTimer.Start();
            UpdateImeIndicator();
        }

        private void UpdateImeIndicator()
        {
            try
            {
                if (TbRbImeText == null)
                {
                    return;
                }

                IntPtr fg = GetForegroundWindow();
                uint tid = GetWindowThreadProcessId(fg, out _);
                IntPtr hkl = GetKeyboardLayout(tid);
                ushort langId = (ushort)((ulong)hkl & 0xFFFF);

                bool isZh = langId == 0x0804 || langId == 0x0404 || langId == 0x0C04 ||
                            langId == 0x1004 || langId == 0x1404;
                bool isJa = langId == 0x0411;
                bool isKo = langId == 0x0412;

                int mode = 0;
                int sentence = 0;
                IntPtr hIme = ImmGetDefaultIMEWnd(fg);
                if (hIme != IntPtr.Zero)
                {
                    ImmGetConversionStatus(hIme, out mode, out sentence);
                }

                bool native = (mode & IME_CMODE_NATIVE) != 0;

                if (!isZh && !isJa && !isKo)
                {
                    TbRbImeText.Text = "英";
                }
                else if (!native)
                {
                    TbRbImeText.Text = "英";
                }
                else if (isJa)
                {
                    TbRbImeText.Text = "あ";
                }
                else if (isKo)
                {
                    TbRbImeText.Text = "가";
                }
                else
                {
                    TbRbImeText.Text = "中";
                }
            }
            catch
            {
                // 输入法状态获取失败时保留原文本
            }
        }

        // ===== 显示桌面 =====

        private static void ToggleDesktop()
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    return;
                }

                object shell = Activator.CreateInstance(shellType);
                shellType.InvokeMember(
                    "ToggleDesktop",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    null);
            }
            catch
            {
                // 显示桌面失败静默
            }
        }

        /// <summary>单个托盘图标的 UI 宿主。</summary>
        /// <summary>任务栏托盘图标的 UI 宿主(Grid 子类,便于事件处理时取回状态)。</summary>
        private sealed class TrayIconUiHost : Grid
        {
            public TrayIcon Icon;
            public Windows.UI.Xaml.Controls.Image Image;
            public Windows.UI.Xaml.Controls.Button UnpinButton;
            public IntPtr LoadedHIcon;
            public TrayMouseButton? PressedButton;
        }
    }
}
