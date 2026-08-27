using ExplorerRevolution.Common.Shell.Tray;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 托盘溢出面板的 UWP Flyout 页面:Win11 风格圆角卡片,横向(可换行)排列
    /// 非常驻图标;hover 显示图钉按钮,左键打开、右键转发给应用。
    /// 运行在独立 XAML Island 线程上,事件通过宿主转发回主线程。
    /// </summary>
    public sealed partial class TrayOverflowFlyoutPage : Page
    {
        private const double ItemSize = 44;
        private const double ItemSpacing = 4;
        private const double CardPadding = 8;
        private const double CardBorder = 2;
        private const double ArrowZone = 8;
        private const int MaxItemsPerRow = 8;

        private readonly Dictionary<TrayIcon, FlyoutIconItem> _items =
            new Dictionary<TrayIcon, FlyoutIconItem>();

        public event Action<TrayIcon> OpenRequested;
        public event Action<TrayIcon> PinRequested;

        public TrayOverflowFlyoutPage()
        {
            this.InitializeComponent();
        }

        /// <summary>箭头中心距卡片右缘的偏移(DIP,由宿主按 chevron 位置计算)。</summary>
        public double ArrowOffsetFromRight
        {
            set => FlyoutArrow.Margin = new Thickness(0, 0, Math.Max(4, value), 1);
        }

        /// <summary>重新填充图标并返回整个页面的期望尺寸(DIP)。</summary>
        public Size SetIcons(IEnumerable<TrayIcon> icons)
        {
            foreach (FlyoutIconItem old in _items.Values)
            {
                old.OpenClicked -= OnItemOpen;
                old.PinClicked -= OnItemPin;
                old.RightTapped -= OnItemRightTapped;
                if (old.Parent is Panel panel)
                {
                    panel.Children.Remove(old);
                }
            }

            _items.Clear();
            RowsPanel.Children.Clear();

            var list = icons?.Where(i => i != null).ToList() ?? new List<TrayIcon>();
            int rows = Math.Max(1, (int)Math.Ceiling(list.Count / (double)MaxItemsPerRow));

            for (int r = 0; r < rows; r++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                RowsPanel.Children.Add(row);

                for (int c = 0; c < MaxItemsPerRow; c++)
                {
                    int index = r * MaxItemsPerRow + c;
                    if (index >= list.Count)
                    {
                        break;
                    }

                    TrayIcon icon = list[index];
                    var item = new FlyoutIconItem(icon)
                    {
                        Margin = new Thickness(0, 0, ItemSpacing, 0)
                    };
                    item.OpenClicked += OnItemOpen;
                    item.PinClicked += OnItemPin;
                    item.RightTapped += OnItemRightTapped;
                    ToolTipService.SetToolTip(item, string.IsNullOrEmpty(icon.Title) ? icon.Identifier : icon.Title);
                    row.Children.Add(item);
                    _items[icon] = item;

                    _ = item.LoadIconAsync();
                }
            }

            int maxInRow = Math.Min(list.Count, MaxItemsPerRow);
            double cardWidth = maxInRow * (ItemSize + ItemSpacing) + CardPadding * 2 + CardBorder;
            double cardHeight = rows * (ItemSize + ItemSpacing) + CardPadding * 2 + CardBorder;

            double width = Math.Max(40, cardWidth);
            double height = cardHeight + ArrowZone;
            LayoutRoot.Width = width;
            LayoutRoot.Height = height;
            return new Size(width, height);
        }

        private void OnItemOpen(FlyoutIconItem item)
        {
            OpenRequested?.Invoke(item.Icon);
        }

        private void OnItemPin(FlyoutIconItem item)
        {
            PinRequested?.Invoke(item.Icon);
        }

        private void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FlyoutIconItem item && item.Icon != null)
            {
                // 右键直接转发给应用(纯 Win32 消息,跨线程安全);
                // 面板保持打开,是否关闭由应用决定(与原生一致)
                item.Icon.IconMouseDown(TrayMouseButton.Right);
                item.Icon.IconMouseUp(TrayMouseButton.Right);
                e.Handled = true;
            }
        }

        /// <summary>单个溢出图标项:图标 + hover 高亮 + 右上角图钉按钮。</summary>
        private sealed class FlyoutIconItem : Grid
        {
            public TrayIcon Icon { get; }

            public event Action<FlyoutIconItem> OpenClicked;
            public event Action<FlyoutIconItem> PinClicked;

            private readonly Border _hoverBackground;
            private readonly Image _iconImage;
            private readonly Button _pinButton;
            private readonly Button _openButton;

            public FlyoutIconItem(TrayIcon icon)
            {
                Icon = icon;
                Width = ItemSize;
                Height = ItemSize;

                _hoverBackground = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Background = null
                };
                Children.Add(_hoverBackground);

                _iconImage = new Image
                {
                    Width = 26,
                    Height = 26,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Children.Add(_iconImage);

                _openButton = new Button
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    Background = null,
                    BorderBrush = null,
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsTabStop = false
                };
                _openButton.Click += (s, e) => OpenClicked?.Invoke(this);
                Children.Add(_openButton);

                _pinButton = new Button
                {
                    Width = 20,
                    Height = 20,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 2, 2, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = null,
                    BorderThickness = new Thickness(0),
                    Visibility = Visibility.Collapsed,
                    Content = new FontIcon
                    {
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        FontSize = 10,
                        Glyph = "\uE718"
                    }
                };
                _pinButton.Click += (s, e) => PinClicked?.Invoke(this);
                Children.Add(_pinButton);

                PointerEntered += OnPointerEntered;
                PointerExited += OnPointerExited;
            }

            public async Task LoadIconAsync()
            {
                try
                {
                    if (Icon.HIcon == IntPtr.Zero)
                    {
                        return;
                    }

                    var bitmap = await HIconToBitmapImageAsync(Icon.HIcon);
                    if (bitmap != null)
                    {
                        _iconImage.Source = bitmap;
                    }
                }
                catch
                {
                    // 图标解码失败时保留空占位
                }
            }

            private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
            {
                _hoverBackground.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
                _pinButton.Visibility = Visibility.Visible;
            }

            private void OnPointerExited(object sender, PointerRoutedEventArgs e)
            {
                _hoverBackground.Background = null;
                _pinButton.Visibility = Visibility.Collapsed;
            }

            private static async Task<BitmapImage> HIconToBitmapImageAsync(IntPtr hIcon)
            {
                using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                using (var bitmap = icon.ToBitmap())
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);

                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                    return bitmapImage;
                }
            }
        }
    }
}
