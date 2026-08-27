using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExplorerRevolution.Common;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.WindowHelpers;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 开始菜单弹层(纯 WinForms):图标 + 名称列表,点击启动应用。
    /// </summary>
    public sealed class StartMenuPopupForm : ShellPopupForm
    {
        private readonly ListBox _list;
        private bool _loaded;
        private int _hoverIndex = -1;

        public StartMenuPopupForm() : base(new Size(440, 560))
        {
            var header = new Label
            {
                Text = "开始",
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
                ItemHeight = 52,
                DrawMode = DrawMode.OwnerDrawFixed,
                SelectionMode = SelectionMode.One
            };
            _list.DrawItem += List_DrawItem;
            _list.MouseUp += List_MouseUp;
            _list.MouseMove += List_MouseMove;
            _list.MouseLeave += List_MouseLeave;
            _list.KeyDown += List_KeyDown;

            Controls.Add(_list);
            Controls.Add(header);
        }

        /// <summary>首次打开时枚举开始菜单应用(结果缓存,重复打开不重新枚举)。</summary>
        public async Task EnsureLoadedAsync()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            try
            {
                var apps = await StartMenuService.GetAppsAsync();

                _list.BeginUpdate();
                foreach (var app in apps)
                {
                    _list.Items.Add(new StartMenuItem
                    {
                        App = app,
                        Name = app.Name,
                        Icon = LoadAppIcon(app)
                    });
                }
                _list.EndUpdate();
            }
            catch
            {
                // 开始菜单加载失败不阻塞任务栏
            }
        }

        private static Image LoadAppIcon(StartMenuApp app)
        {
            try
            {
                if (string.IsNullOrEmpty(app.IconPath))
                {
                    return null;
                }

                if (app.IsUwp)
                {
                    // UWP 图标是 manifest 解析出的 PNG 文件(裁剪透明留白,视觉尺寸一致)
                    if (File.Exists(app.IconPath))
                    {
                        return LoadCroppedIconImage(app.IconPath);
                    }
                }
                else
                {
                    // Win32 快捷方式:从目标 exe / IconLocation 取图标,避免快捷方式小箭头
                    IntPtr hIcon = GetShortcutIcon(app.IconPath);
                    if (hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            return Icon.FromHandle(hIcon).ToBitmap();
                        }
                        finally
                        {
                            DestroyIcon(hIcon);
                        }
                    }
                }
            }
            catch
            {
                // 单个图标失败则显示无图标条目
            }

            return null;
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || !(_list.Items[e.Index] is StartMenuItem item))
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

            if (item.Icon != null)
            {
                var iconRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + (e.Bounds.Height - 32) / 2, 32, 32);
                e.Graphics.DrawImage(item.Icon, iconRect);
            }

            var textRect = new Rectangle(e.Bounds.Left + 56, e.Bounds.Top, e.Bounds.Width - 68, e.Bounds.Height);
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
            if (index < 0 || index >= _list.Items.Count || !(_list.Items[index] is StartMenuItem item))
            {
                return;
            }

            LaunchApp(item.App);
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

        public static void LaunchApp(StartMenuApp app)
        {
            try
            {
                if (app.IsUwp && !string.IsNullOrEmpty(app.AppUserModelId))
                {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && _list != null)
            {
                foreach (var obj in _list.Items)
                {
                    if (obj is StartMenuItem item)
                    {
                        item.Icon?.Dispose();
                    }
                }
            }

            base.Dispose(disposing);
        }

        private sealed class StartMenuItem
        {
            public StartMenuApp App;
            public string Name;
            public Image Icon;
        }
    }
}
