using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.WindowHelpers;
using TaskBarIcon = ExplorerRevolution.Data.TaskBarIcon;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 任务视图弹层(纯 WinForms):当前窗口列表,点击切换 / 再点最小化。
    /// </summary>
    public sealed class TaskViewPopupForm : ShellPopupForm
    {
        private readonly ListBox _list;
        private readonly Label _header;
        private Action<TaskBarIcon> _activate;
        private int _hoverIndex = -1;

        public TaskViewPopupForm(string title = "任务视图") : base(new Size(440, 360))
        {
            _header = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(16, 14, 16, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 20f, FontStyle.Regular)
            };

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f),
                IntegralHeight = false,
                ItemHeight = 56,
                DrawMode = DrawMode.OwnerDrawFixed,
                SelectionMode = SelectionMode.One
            };
            _list.DrawItem += List_DrawItem;
            _list.MouseUp += List_MouseUp;
            _list.MouseMove += List_MouseMove;
            _list.MouseLeave += List_MouseLeave;
            _list.KeyDown += List_KeyDown;

            Controls.Add(_list);
            Controls.Add(_header);
        }

        /// <summary>用当前任务栏窗口快照重建列表,并注册点击回调。</summary>
        public void SetWindows(IEnumerable<TaskBarIcon> icons, Action<TaskBarIcon> activate, string title = null)
        {
            _activate = activate;

            if (!string.IsNullOrEmpty(title))
            {
                _header.Text = title;
            }

            _list.BeginUpdate();

            foreach (var obj in _list.Items)
            {
                if (obj is TaskViewItem item)
                {
                    item.Image?.Dispose();
                    item.Stream?.Dispose();
                }
            }
            _list.Items.Clear();

            foreach (var icon in icons)
            {
                var image = GetWindowImage(icon.IntPtr);
                _list.Items.Add(new TaskViewItem
                {
                    Icon = icon,
                    Name = string.IsNullOrEmpty(icon.Title) ? "窗口" : icon.Title,
                    Image = image.Image,
                    Stream = image.Stream
                });
            }

            _list.EndUpdate();
        }

        private static (Image Image, MemoryStream Stream) GetWindowImage(IntPtr hWnd)
        {
            try
            {
                // UWP 窗口:优先用 manifest 图标 PNG
                var className = new StringBuilder(256);
                GetClassName(hWnd, className, 256);
                string cls = className.ToString();

                var aumid = GetAppUserModelId(hWnd);
                if ((cls == "ApplicationFrameWindow" || cls == "Windows.UI.Core.CoreWindow") && !string.IsNullOrEmpty(aumid))
                {
                    string iconPath = ResolveUwpIconPath(aumid);
                    if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    {
                        // 裁剪透明留白,视觉尺寸与 Win32 图标一致
                        return (LoadCroppedIconImage(iconPath), null);
                    }
                }

                // 普通 Win32 窗口走 HICON 转换
                var hIcon = GetWindowIcon(hWnd);
                if (hIcon != IntPtr.Zero)
                {
                    try
                    {
                        return (Icon.FromHandle(hIcon).ToBitmap(), null);
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch
            {
                // 单个窗口图标失败则显示无图标条目
            }

            return (null, null);
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || !(_list.Items[e.Index] is TaskViewItem item))
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool hovered = e.Index == _hoverIndex;
            Color bg = selected || hovered ? Color.FromArgb(64, 64, 64) : Color.FromArgb(32, 32, 32);

            using (var brush = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (item.Image != null)
            {
                var iconRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + (e.Bounds.Height - 36) / 2, 36, 36);
                e.Graphics.DrawImage(item.Image, iconRect);
            }

            var textRect = new Rectangle(e.Bounds.Left + 60, e.Bounds.Top, e.Bounds.Width - 72, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                item.Name,
                _list.Font,
                textRect,
                Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            int index = IndexAtPoint(e.Location);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                _list.Invalidate();
            }
        }

        private void List_MouseLeave(object sender, EventArgs e)
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _list.Invalidate();
            }
        }

        private void List_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = IndexAtPoint(e.Location);
            InvokeItem(index);
        }

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                InvokeItem(_list.SelectedIndex);
                e.Handled = true;
            }
        }

        private void InvokeItem(int index)
        {
            if (index < 0 || index >= _list.Items.Count || !(_list.Items[index] is TaskViewItem item))
            {
                return;
            }

            _activate?.Invoke(item.Icon);
            Hide();
        }

        private int IndexAtPoint(Point point)
        {
            int index = _list.IndexFromPoint(point);
            if (index < 0 || index >= _list.Items.Count)
            {
                return -1;
            }

            return _list.GetItemRectangle(index).Contains(point) ? index : -1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _list != null)
            {
                foreach (var obj in _list.Items)
                {
                    if (obj is TaskViewItem item)
                    {
                        item.Image?.Dispose();
                        item.Stream?.Dispose();
                    }
                }
            }

            base.Dispose(disposing);
        }

        private sealed class TaskViewItem
        {
            public TaskBarIcon Icon;
            public string Name;
            public Image Image;
            public MemoryStream Stream;
        }
    }
}
