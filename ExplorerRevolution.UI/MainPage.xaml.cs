using ExplorerRevolution.UI;
using System;
using System.Runtime.InteropServices;
using System.Security.Policy;
using Windows.Gaming.Input.ForceFeedback;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace MileXamlBlankAppNetFrameworkModern
{
    public sealed partial class MainPage : Page
    {
        public string FrameworkDescription => RuntimeInformation.FrameworkDescription;

        public MainPage()
        {
            InitializeComponent();
            frame1.Navigate(typeof(TaskBar));
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            /*var contentDialog = new ContentDialog
            {

                Title = "Hello world",
                Content = "Where do you want to go today?",
                PrimaryButtonText = "OK",
                XamlRoot = (sender as Button).XamlRoot,
            };
            await contentDialog.ShowAsync();*/

            (frame1.Content as TaskBar).AddAppButton(0, new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("D:\\A2778\\Pictures\\图标\\WinUI\\AnimationInterop.png")), "结束");
            (frame1.Content as TaskBar).AddAppButton(0, new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("D:\\A2778\\Pictures\\图标\\WinUI\\AutomationProperties.png")), "结束");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            (frame1.Content as TaskBar).SetHighlightButton(1); 
        }
    }
}
