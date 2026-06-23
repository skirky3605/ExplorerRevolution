using System;
using System.Windows.Forms;

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

            MainForm = new MainForm();

            #region 替代explorer
            MainForm.FormBorderStyle = FormBorderStyle.None;
            MainForm.WindowState = FormWindowState.Normal;
            MainForm.Bounds = Screen.PrimaryScreen.Bounds;
            MainForm.TopMost = false;
            Common.HookExplorer.HideExplorer();
            Common.HookExplorer.AttachToWorkerW(MainForm.Handle);
            Common.KeyInputHandler.Install();
            #endregion

            Common.Helpers.SetMicaBackdrop(MainForm.Handle);

            Application.Run(MainForm);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Common.HookExplorer.RestoreExplorer();
            };

            app.Close();
        }

        public static void Run()
        {
            Main();
        }
    }
}
