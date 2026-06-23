using ExplorerRevolution.Common;
using ExplorerRevolution.UI;
using Mile.Xaml;
using System;
using System.Drawing;
using System.Windows.Forms;
using Windows.UI.Xaml;
using static ExplorerRevolution.Common.NativeMethods;
using Application = System.Windows.Forms.Application;

namespace ExplorerRevolution
{
    public static class Program
    {
        public static Form MainForm { get; private set; }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            App app = new();

            var DesktopForm = new Form();
            DesktopForm.FormBorderStyle = FormBorderStyle.None;
            DesktopForm.WindowState = FormWindowState.Normal;
            DesktopForm.Bounds = Screen.PrimaryScreen.Bounds;

            DesktopForm.AllowTransparency = true;
            DesktopForm.BackColor = Color.LimeGreen;
            DesktopForm.TransparencyKey = Color.LimeGreen;
            DesktopForm.TopMost = false;
            Common.HookExplorer.HideExplorer();
            Common.HookExplorer.AttachToWorkerW(DesktopForm.Handle);
            WindowsXamlHost DesktopXamlHost = new WindowsXamlHost();
            DesktopForm.Controls.Add(DesktopXamlHost);
            DesktopXamlHost.AutoSize = true;
            DesktopXamlHost.Dock = DockStyle.Fill;
            DesktopXamlHost.Child = new DesktopPage();
            Application.Run(DesktopForm);

            MainForm = new MainForm();
            #region 替代explorer
            MainForm.FormBorderStyle = FormBorderStyle.None;
            MainForm.WindowState = FormWindowState.Normal;
            MainForm.Bounds = Screen.PrimaryScreen.Bounds;

            MainForm.AllowTransparency = true;
            MainForm.BackColor = Color.LimeGreen;
            MainForm.TransparencyKey = Color.LimeGreen;
            MainForm.TopMost = false;
            Common.HookExplorer.HideExplorer();
            Common.HookExplorer.AttachToWorkerW(MainForm.Handle);
            #endregion

            Application.Run(MainForm);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                HookExplorer.RestoreExplorer();
            };

            // Only for test
            WindowHelpers.GetTaskBarIcons();

            app.Close();
        }

        public static void Run()
        {
            Main();
        }
    }
}
