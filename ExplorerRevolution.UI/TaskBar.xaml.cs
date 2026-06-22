using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
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
            if (index < TaskBarStack.Children.Count() && index >= 0)
            {
                ((TaskBarStack.Children[index] as Grid).Children[0] as Button).Background = new Brush;
            }

            for (int i = 0; i < TaskBarStack.Children.Count(); i++)
            {
                if(i != index)
                {
                    ((TaskBarStack.Children[i] as Grid).Children[0] as Button).Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        public int GetHighlightButton()
        {
            return -1;
        }

        public void AddAppButton(int index, BitmapImage imageSource, string appTitle)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 1),
            };
            stackPanel.Children.Add(new Image { Width = 28, Height = 28, Stretch = Stretch.Uniform, Margin = new Thickness(4, 4, 4, 4), Source = imageSource });
            stackPanel.Children.Add(new TextBlock { Text = appTitle, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, FontFamily = new FontFamily("HarmonyOS Sans SC"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var appFrontButton = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2, 0, 2, 0),
                Opacity = 0.4
            };
            var appBackButton = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2, 0, 2, 0),
                Opacity = 1,
                IsTabStop = false
            };
            var appButtonGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2, 0, 2, 0),
            };
            appButtonGrid.Children.Add(appBackButton);
            appButtonGrid.Children.Add(stackPanel);
            appButtonGrid.Children.Add(appFrontButton);
            appButtonGrid.Transitions.Add(new RepositionThemeTransition());
            TaskBarStack.Children.Insert(index, appButtonGrid);
        }
    }
}
