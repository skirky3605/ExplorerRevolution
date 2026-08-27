using ExplorerRevolution.Common;
using ExplorerRevolution.Common.Shell.Tray;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 托盘溢出面板(纯 WinForms):独立浮窗,右对齐悬浮在任务栏上方,
    /// 横向排列非常驻图标;hover 显示固定按钮,点击图标转发给应用,
    /// 点击外部自动关闭且不抢焦点。
    /// </summary>
    public sealed class TrayOverflowPopupForm : ShellPopupForm, ITrayOverflowHost
    {
        private const int ItemSize = 44;
        private const int ItemSpacing = 4;
        private const int PanelPadding = 10;

        public bool IsAvailable => true;

        private readonly FlowLayoutPanel _panel;
        private readonly ToolTip _tooltip = new ToolTip();
        private readonly List<OverflowItem> _items = new List<OverflowItem>();
        private Action<TrayIcon> _pinRequest;
        private Action<TrayIcon> _openRequest;
        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHook;
        private bool _isShowing;
        private Rectangle _lastAnchorRect;

        public TrayOverflowPopupForm() : base(new Size(64, ItemSize + PanelPadding * 2))
        {
            _panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(PanelPadding),
                BackColor = Color.FromArgb(32, 32, 32)
            };
            Controls.Add(_panel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Helpers.SetNoActivate(Handle);
        }

        /// <summary>填充图标并显示在任务栏上方。</summary>
        public void ShowOverflow(Rectangle anchorRect, float dpiScale, IEnumerable<TrayIcon> icons, Action<TrayIcon> openRequest, Action<TrayIcon> pinRequest)
        {
            _openRequest = openRequest;
            _pinRequest = pinRequest;
            _lastAnchorRect = anchorRect;

            SetIcons(icons);
            PositionAbove(anchorRect);

            Show();
            BringToFront();
            InstallMouseHook();
            _isShowing = true;
        }

        /// <summary>面板已打开时刷新内容(固定/取消固定、图标增删后调用)。</summary>
        public void RefreshIcons(IEnumerable<TrayIcon> icons)
        {
            if (!_isShowing || IsDisposed)
            {
                return;
            }

            var list = new List<TrayIcon>(icons);
            if (list.Count == 0)
            {
                Hide();
                return;
            }

            SetIcons(list);
            PositionAbove(_lastAnchorRect);
        }

        public new void Hide()
        {
            UninstallMouseHook();
            _isShowing = false;
            base.Hide();
        }

        private void SetIcons(IEnumerable<TrayIcon> icons)
        {
            foreach (OverflowItem item in _items)
            {
                _tooltip.SetToolTip(item, null);
                item.OpenClicked -= OnItemOpen;
                item.PinClicked -= OnItemPin;
                _panel.Controls.Remove(item);
                item.Image?.Dispose();
                item.Dispose();
            }

            _items.Clear();

            foreach (TrayIcon icon in icons)
            {
                if (icon == null)
                {
                    continue;
                }

                var item = new OverflowItem(icon, LoadIconImage(icon))
                {
                    Margin = new Padding(0, 0, ItemSpacing, 0)
                };
                item.OpenClicked += OnItemOpen;
                item.PinClicked += OnItemPin;
                _tooltip.SetToolTip(item, string.IsNullOrEmpty(icon.Title) ? icon.Identifier : icon.Title);
                _panel.Controls.Add(item);
                _items.Add(item);
            }

            int width = Math.Max(1, _items.Count) * (ItemSize + ItemSpacing) - ItemSpacing + PanelPadding * 2;
            Width = Math.Max(64, width);
            Height = ItemSize + PanelPadding * 2;
        }

        private void PositionAbove(Rectangle taskbarRect)
        {
            if (taskbarRect.IsEmpty)
            {
                return;
            }

            int left = taskbarRect.Right - Width - 8;
            var screen = Screen.FromRectangle(taskbarRect).WorkingArea;
            left = Math.Max(screen.Left + 8, left);

            Location = new Point(left, taskbarRect.Top - Height - 8);
        }

        private static Bitmap LoadIconImage(TrayIcon icon)
        {
            try
            {
                if (icon.HIcon == IntPtr.Zero)
                {
                    return null;
                }

                using (Icon ico = Icon.FromHandle(icon.HIcon))
                {
                    return ico.ToBitmap();
                }
            }
            catch
            {
                return null;
            }
        }

        private void OnItemOpen(OverflowItem item)
        {
            _openRequest?.Invoke(item.Icon);
            Hide();
        }

        private void OnItemPin(OverflowItem item)
        {
            _pinRequest?.Invoke(item.Icon);
        }

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
            if (nCode >= 0 && _isShowing && lParam != IntPtr.Zero)
            {
                uint msg = (uint)wParam;
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    if (!Bounds.Contains(new Point(ms.pt.X, ms.pt.Y)))
                    {
                        BeginInvoke(new Action(Hide));
                    }
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UninstallMouseHook();
                _tooltip.Dispose();

                foreach (OverflowItem item in _items)
                {
                    item.Image?.Dispose();
                    item.Dispose();
                }

                _items.Clear();
            }

            base.Dispose(disposing);
        }

        /// <summary>单个溢出图标项(自绘:图标 + hover 高亮 + 右上角固定按钮)。</summary>
        private sealed class OverflowItem : Control
        {
            public TrayIcon Icon;
            public Bitmap Image;
            public event Action<OverflowItem> OpenClicked;
            public event Action<OverflowItem> PinClicked;

            private bool _hover;
            private Rectangle _pinRect;

            public OverflowItem(TrayIcon icon, Bitmap image)
            {
                Icon = icon;
                Image = image;
                Size = new Size(ItemSize, ItemSize);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hover = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);

                if (e.Button == MouseButtons.Left)
                {
                    if (_pinRect.Contains(e.Location))
                    {
                        PinClicked?.Invoke(this);
                    }
                    else
                    {
                        OpenClicked?.Invoke(this);
                    }
                }
                else if (e.Button == MouseButtons.Right)
                {
                    // 右键转发给应用(面板保持打开,由应用决定是否关闭)
                    Icon?.IconMouseDown(TrayMouseButton.Right);
                    Icon?.IconMouseUp(TrayMouseButton.Right);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                if (_hover)
                {
                    using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 7))
                    using (var brush = new SolidBrush(Color.FromArgb(255, 64, 64, 64)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }

                if (Image != null)
                {
                    int size = 26;
                    var rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                    e.Graphics.DrawImage(Image, rect);
                }

                _pinRect = Rectangle.Empty;
                if (_hover)
                {
                    _pinRect = new Rectangle(Width - 21, 2, 19, 19);
                    using (GraphicsPath path = RoundedRect(_pinRect, 6))
                    using (var brush = new SolidBrush(Color.FromArgb(255, 90, 90, 90)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }

                    using (var font = new Font("Segoe MDL2 Assets", 8f))
                    {
                        TextRenderer.DrawText(
                            e.Graphics,
                            "\uE718",
                            font,
                            _pinRect,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    }
                }
            }

            private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
