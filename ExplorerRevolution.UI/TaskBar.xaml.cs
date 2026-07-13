using ExplorerRevolution.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
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
        }

        private void TaskBarGrid_Loaded(object sender, RoutedEventArgs e)
        {
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

            for (int i = 0; i < (taskBarIcons).Count(); i++)
            {
                (taskBarIcons)[i].ButtonTitleVisibility = TbTitleVisibility ? Visibility.Visible : Visibility.Collapsed;
                (taskBarIcons)[i].IsForeground = TbTitleVisibility;
            }

            //Bindings.Update();
            taskBarIcons.Move(0, 0);
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

        }

        private void Button_TbRbBkgAppArea_Front_Click(object sender, RoutedEventArgs e)
        {
            TbTitleVisibility = !TbTitleVisibility;
            RefreshTbPreferences();
        }

        private void Button_TbRbKeyBoardArea_Front_Click(object sender, RoutedEventArgs e)
        {

        }
        ObservableCollection<TaskBarIcon> taskBarIcons = new();

        private void TaskBarItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            var monitor = new Common.TaskbarButtonMonitor();
            monitor.ButtonAdded += (intPtr, title) =>
            {
                if (!taskBarIcons.Select(icon => icon.IntPtr).Contains(intPtr)){
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
                        var taskBarIcon = new TaskBarIcon()
                        {
                            IntPtr = intPtr,
                            Title = GetWindowTitle(intPtr),
                            Icon = await GetWindowIconAsync(intPtr),
                            IsActive = Visibility.Visible,
                            IsForeground = TbTitleVisibility,
                            ButtonTitleVisibility = TbTitleVisibility ? Visibility.Collapsed : Visibility.Visible
                        };
                        taskBarIcons.Add(taskBarIcon);
                    });
                }
            };
            monitor.ButtonTitleChanged += (intPtr, title) =>
            {
                foreach (var icon in taskBarIcons)
                {
                    if (icon.IntPtr == intPtr)
                    {
                        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            icon.Title = title;
                        });
                        break;
                    }
                }
            };
            monitor.ButtonRemoved += (intPtr, title) =>
            {
                foreach (var icon in taskBarIcons)
                {
                    if (icon.IntPtr == intPtr)
                    {
                        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            taskBarIcons.Remove(icon);
                        });
                        break;
                    }
                }
            };
            monitor.Start();
        }

        public static async Task<BitmapImage> GetWindowIconAsync(IntPtr hWnd)
        {
            // 判断是否 UWP 窗口
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);

            if (className.ToString() == "ApplicationFrameWindow")
            {
                Debug.WriteLine(hWnd);
                var aumid = GetAppUserModelId(hWnd);
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
            (sender as GridView).SelectedIndex = (sender as GridView).SelectedIndex;
            //(sender as GridView).SelectedIndex = -1;
            SetHighlightButton((sender as GridView).SelectedIndex);
        }
    }
}
