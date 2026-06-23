using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class DesktopIcons : Page
    {
        public DesktopIcons()
        {
            this.InitializeComponent();
        }

        private void Desktop_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            Debug.WriteLine("Right Clicked");
            var pos = e.GetPosition(Desktop);
            Debug.WriteLine(pos.X);
            Debug.WriteLine(pos.Y);
        }
    }
}
