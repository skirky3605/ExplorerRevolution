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
    /// 托盘溢出面板的系统 Flyout 内容容器,按行排列非常驻图标。
    /// 左键打开、右键转发给应用,悬停时提供固定操作。
    /// </summary>
    public sealed partial class TrayOverflowFlyoutPage : StackPanel
    {
        private const double ItemSize = 48;
        private const double ItemSpacing = 6;
        private const int MaxItemsPerRow = 5;
        private const double FlyoutPadding = 0;

        private readonly Dictionary<TrayIcon, FlyoutIconItem> _items =
            new Dictionary<TrayIcon, FlyoutIconItem>();

        public event Action<TrayIcon> OpenRequested;
        public event Action<TrayIcon> PinRequested;
        public event Action<TrayIcon> RightRequested;
        public event Action ContextDismissRequested;

        public TrayOverflowFlyoutPage()
        {
            this.InitializeComponent();
        }

        // Retained for source compatibility with the legacy island host; the in-process
        // taskbar Flyout uses the platform placement arrow and does not need an offset.
        public double ArrowOffsetFromRight
        {
            set { }
        }

        public void ResetIconInteractions()
        {
            foreach (FlyoutIconItem item in _items.Values.ToList())
            {
                item.Icon.IconMouseLeave();
            }
        }

        /// <summary>重新填充图标并返回整个页面的期望尺寸(DIP)。</summary>
        public Size SetIcons(IEnumerable<TrayIcon> icons)
        {
            foreach (FlyoutIconItem old in _items.Values.ToList())
            {
                old.Icon.IconMouseLeave();
                old.PinClicked -= OnItemPin;
                old.RightTappedRequested -= OnItemRightTapped;
                old.OpenPointerReleased -= OnItemOpenPointerReleased;
                old.RightPointerPressed -= OnItemRightPointerPressed;
                old.Icon.PropertyChanged -= old.OnIconPropertyChanged;
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
                var row = new StackPanel
                {
                    Height = ItemSize,
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Top
                };
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
                    item.PinClicked += OnItemPin;
                    item.RightTappedRequested += OnItemRightTapped;
                    item.OpenPointerReleased += OnItemOpenPointerReleased;
                    item.RightPointerPressed += OnItemRightPointerPressed;
                    item.Icon.PropertyChanged += item.OnIconPropertyChanged;
                    ToolTipService.SetToolTip(item, string.IsNullOrEmpty(icon.Title) ? icon.Identifier : icon.Title);
                    row.Children.Add(item);
                    _items[icon] = item;

                    _ = item.LoadIconAsync();
                }
            }

            int maxInRow = Math.Min(list.Count, MaxItemsPerRow);
            double width = Math.Max(40, maxInRow * (ItemSize + ItemSpacing) + FlyoutPadding * 2);
            double height = rows * (ItemSize + ItemSpacing) - ItemSpacing + FlyoutPadding * 2;
            Width = width;
            // Let the system FlyoutPresenter measure the rows instead of stretching
            // the content to the XAML Island's full height.
            Height = double.NaN;
            return new Size(width, height);
        }

        private void OnItemOpenPointerReleased(FlyoutIconItem item)
        {
            OpenRequested?.Invoke(item.Icon);
        }

        private void OnItemPin(FlyoutIconItem item)
        {
            PinRequested?.Invoke(item.Icon);
        }

        private void OnItemRightPointerPressed(FlyoutIconItem item)
        {
            if (item?.Icon != null)
            {
                // 右键在按下阶段开始协议,避免依赖 RightTapped 手势事件。
                RightRequested?.Invoke(item.Icon);
            }
        }

        private void OnItemRightTapped(FlyoutIconItem item)
        {
            // Fallback for XAML hosts that expose RightTapped but do not expose
            // right-button state in PointerPressed.
            if (item?.Icon != null && !item.RightButtonWasPressed)
            {
                RightRequested?.Invoke(item.Icon);
            }
        }

        private void RowsPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Pointer events from icon items bubble to the root. Walk up from the
            // original source so only genuine empty Flyout space dismisses the
            // previous context interaction.
            DependencyObject current = e.OriginalSource as DependencyObject;
            while (current != null && current != this)
            {
                if (current is FlyoutIconItem)
                {
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            ContextDismissRequested?.Invoke();
        }

        /// <summary>单个溢出图标项:图标 + hover 高亮 + 右上角图钉按钮。</summary>
        private sealed class FlyoutIconItem : Grid
        {
            public TrayIcon Icon { get; }

            public event Action<FlyoutIconItem> PinClicked;
            public event Action<FlyoutIconItem> RightPointerPressed;
            public event Action<FlyoutIconItem> RightTappedRequested;

            public bool RightButtonWasPressed => _rightButtonHandled;

            private readonly Border _hoverBackground;
            private readonly Image _iconImage;
            private readonly Button _pinButton;
            private bool _openPointerReleased;
            private bool _rightPointerPressed;
            private bool _rightButtonHandled;

            public event Action<FlyoutIconItem> OpenPointerReleased;

            public FlyoutIconItem(TrayIcon icon)
            {
                Icon = icon;
                Width = ItemSize;
                Height = ItemSize;

                _hoverBackground = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Background = null,
                    IsHitTestVisible = false
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
                _pinButton.PointerPressed += (s, e) => e.Handled = true;
                _pinButton.PointerReleased += (s, e) => e.Handled = true;
                Children.Add(_pinButton);

                PointerEntered += OnPointerEntered;
                PointerExited += OnPointerExited;
                PointerMoved += OnPointerMoved;
                RightTapped += OnRightTappedEvent;
                PointerPressed += OnOpenPointerPressed;
                PointerReleased += OnOpenPointerReleased;
            }

            public async Task LoadIconAsync()
            {
                try
                {
                    IntPtr hIcon = Icon.HIcon;
                    if (hIcon == IntPtr.Zero)
                    {
                        return;
                    }

                    var bitmap = await HIconToBitmapImageAsync(hIcon);
                    if (bitmap != null && Icon.HIcon == hIcon)
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
                _hoverBackground.Background = new SolidColorBrush(Color.FromArgb(0x24, 0x80, 0x80, 0x80));
                _pinButton.Visibility = Visibility.Visible;
                Icon.IconMouseEnter();
            }

            private void OnPointerExited(object sender, PointerRoutedEventArgs e)
            {
                _hoverBackground.Background = null;
                _pinButton.Visibility = Visibility.Collapsed;
                Icon.IconMouseLeave();
            }

            private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
            {
                Icon.IconMouseMove();
            }

            private void OnOpenPointerPressed(object sender, PointerRoutedEventArgs e)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    _rightPointerPressed = false;
                    _openPointerReleased = true;
                    CapturePointer(e.Pointer);
                    e.Handled = true;
                }
                else if (point.Properties.IsRightButtonPressed)
                {
                    _rightPointerPressed = true;
                    _rightButtonHandled = true;
                    CapturePointer(e.Pointer);
                    e.Handled = true;
                    RightPointerPressed?.Invoke(this);
                }
            }

            private void OnOpenPointerReleased(object sender, PointerRoutedEventArgs e)
            {
                if (!_openPointerReleased)
                {
                    if (_rightPointerPressed)
                    {
                        _rightPointerPressed = false;
                        ReleasePointerCapture(e.Pointer);
                        e.Handled = true;
                    }
                    return;
                }

                _openPointerReleased = false;
                ReleasePointerCapture(e.Pointer);
                OpenPointerReleased?.Invoke(this);
                e.Handled = true;
            }

            private void OnRightTappedEvent(object sender, RightTappedRoutedEventArgs e)
            {
                if (!_rightButtonHandled)
                {
                    RightTappedRequested?.Invoke(this);
                }
                _rightButtonHandled = false;
                e.Handled = true;
            }

            public void OnIconPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(TrayIcon.HIcon))
                {
                    _ = LoadIconAsync();
                }
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
