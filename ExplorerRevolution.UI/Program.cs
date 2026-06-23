using System;
using System.Drawing;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;

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

            MainForm.AllowTransparency = true;
            MainForm.BackColor = Color.LimeGreen;
            MainForm.TransparencyKey = Color.LimeGreen;
            //const int GWL_EXSTYLE = -20;
            //const int WS_EX_LAYERED = 0x80000;
            //const int WS_EX_TRANSPARENT = 0x20;
            //// 确保不带 WS_EX_TRANSPARENT，窗口可以接收点击
            //int style = GetWindowLong(MainForm.Handle, GWL_EXSTYLE);
            //style |= WS_EX_LAYERED;
            //style &= ~WS_EX_TRANSPARENT;         // 移除穿透标志
            //SetWindowLong(MainForm.Handle, GWL_EXSTYLE, style);
            MainForm.TopMost = false;
            Common.HookExplorer.HideExplorer();
            Common.HookExplorer.AttachToWorkerW(MainForm.Handle);
            #endregion

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
