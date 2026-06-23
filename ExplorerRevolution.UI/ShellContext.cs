using Mile.Xaml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExplorerRevolution.UI
{
    public class ShellContext : ApplicationContext
    {
        public Form DesktopForm { get; }
        public Form TaskBarForm { get; }

        public ShellContext()
        {
            Common.HookExplorer.HideExplorer();
            DesktopForm = CreateDesktopForm();
            MainForm = CreateTaskBarForm();

            DesktopForm.FormClosed += OnFormClosed;
            MainForm.FormClosed += OnFormClosed;

            DesktopForm.Show();
            MainForm.Show();
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            if (DesktopForm.IsDisposed && MainForm.IsDisposed)
            {
                ExitThread();
            }
        }

        private Form CreateDesktopForm()
        {
            var DesktopForm = new Form();
            DesktopForm.FormBorderStyle = FormBorderStyle.None;
            DesktopForm.Bounds = Screen.PrimaryScreen.Bounds;
            DesktopForm.BackColor = Color.LimeGreen;
            DesktopForm.TransparencyKey = Color.LimeGreen;

            Common.HookExplorer.AttachToWorkerW(DesktopForm.Handle);

            WindowsXamlHost DesktopXamlHost = new WindowsXamlHost();
            DesktopForm.Controls.Add(DesktopXamlHost);
            DesktopXamlHost.AutoSize = true;
            DesktopXamlHost.Dock = DockStyle.Fill;
            DesktopXamlHost.Child = new DesktopPage();

            return DesktopForm;
        }

        private Form CreateTaskBarForm()
        {
            var TaskBarForm = new Form();
            TaskBarForm.FormBorderStyle = FormBorderStyle.None;
            TaskBarForm.BackColor = Color.LimeGreen;
            TaskBarForm.TransparencyKey = Color.LimeGreen;
            TaskBarForm.TopMost = true;

            TaskBarForm.Show();

            using var g = TaskBarForm.CreateGraphics();
            float dpiScale = g.DpiX / 96f;

            int taskBarHeight = (int)(48 * dpiScale);

            var screen = Screen.FromControl(TaskBarForm).Bounds;

            TaskBarForm.Width = screen.Width;
            TaskBarForm.Height = taskBarHeight;
            TaskBarForm.Left = screen.Left;
            TaskBarForm.Top = screen.Bottom - taskBarHeight;

            WindowsXamlHost TaskBarXamlHost = new WindowsXamlHost();
            TaskBarForm.Controls.Add(TaskBarXamlHost);
            TaskBarXamlHost.AutoSize = false;
            TaskBarXamlHost.Dock = DockStyle.Fill;
            TaskBarXamlHost.Child = new TaskBar();

            return TaskBarForm;
        }
    }
}
