using ExplorerRevolution.Common;
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

        /// <summary>任务栏窗体实例(供弹层定位等使用)。</summary>
        public static Form TaskBarFormInstance { get; private set; }

        private ShellFullscreenMonitor _fullscreenMonitor;
        private TaskBar _taskbarPage;

        public ShellContext()
        {
            Common.HookExplorer.HideExplorer();
            DesktopForm = CreateDesktopForm();
            MainForm = CreateTaskBarForm();
            TaskBarFormInstance = MainForm;

            DesktopForm.FormClosed += OnFormClosed;
            MainForm.FormClosed += OnFormClosed;

            DesktopForm.Show();
            MainForm.Show();

            _fullscreenMonitor = new ShellFullscreenMonitor(MainForm);
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _fullscreenMonitor?.Dispose();
            _fullscreenMonitor = null;

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
            DesktopForm.ShowInTaskbar = false;
            Helpers.HideFromAltTab(DesktopForm.Handle);

            HookExplorer.AttachToWorkerW(DesktopForm.Handle);

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
            // Keep the host non-layered so DWM can provide the Windows 11 Mica backdrop
            // to the taskbar and to Flyout content using HostBackdrop Acrylic.
            TaskBarForm.BackColor = Color.Black;
            TaskBarForm.TopMost = true;
            TaskBarForm.ShowInTaskbar = false;
            Helpers.HideFromAltTab(TaskBarForm.Handle);
            Helpers.SetNoActivate(TaskBarForm.Handle);

            TaskBarForm.Show();

            using var g = TaskBarForm.CreateGraphics();
            float dpiScale = g.DpiX / 96f;

            int taskBarHeight = (int)(48 * dpiScale);

            var screen = Screen.FromControl(TaskBarForm).Bounds;

            TaskBarForm.Width = screen.Width;
            TaskBarForm.Height = taskBarHeight;
            TaskBarForm.Left = screen.Left;
            TaskBarForm.Top = screen.Bottom - taskBarHeight;
            Helpers.SetMicaBackdrop(TaskBarForm.Handle);

            WindowsXamlHost TaskBarXamlHost = new WindowsXamlHost();
            TaskBarForm.Controls.Add(TaskBarXamlHost);
            TaskBarXamlHost.AutoSize = false;
            TaskBarXamlHost.Dock = DockStyle.Fill;
            _taskbarPage = new TaskBar();
            TaskBarXamlHost.Child = _taskbarPage;

            return TaskBarForm;
        }
    }
}
