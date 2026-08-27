using System;
using System.Drawing;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 纯 WinForms 弹层基类。
    /// 说明:同一 UI 线程上创建第二个 XAML Island 窗口会破坏任务栏的 acrylic 渲染
    /// (microsoft-ui-xaml #3482 / CommunityToolkit #170 已知缺陷),因此开始菜单、
    /// 任务视图等弹层改用原生 WinForms 窗体绘制,任务栏本身的 acrylic 不再受影响。
    /// </summary>
    public class ShellPopupForm : Form
    {
        public ShellPopupForm(Size size)
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(32, 32, 32);
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Size = size;
            KeyPreview = true;
            Font = new Font("Segoe UI", 10f);

            Deactivate += (s, e) =>
            {
                // 点击外部 / 失去激活时关闭(light dismiss)
                if (Visible)
                {
                    Hide();
                }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            try
            {
                ExplorerRevolution.Common.Helpers.HideFromAltTab(Handle);

                // Win11 圆角(不支持时静默失败,保持直角)
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
                // 装饰失败不影响功能
            }
        }

        /// <summary>将弹层定位到任务栏正上方(左缘 +leftOffset)并显示。</summary>
        public void ShowAbove(Form anchor, int leftOffset)
        {
            if (anchor == null || anchor.IsDisposed)
            {
                return;
            }

            Location = new Point(anchor.Left + leftOffset, anchor.Top - Height);
            Show();
            BringToFront();
            Activate();
        }
    }
}
