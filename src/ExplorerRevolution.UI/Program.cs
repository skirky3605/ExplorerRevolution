using ExplorerRevolution.Common;
using ExplorerRevolution.UI;
using Mile.Xaml;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.UI.Xaml;
using static ExplorerRevolution.Common.NativeMethods;
using Application = System.Windows.Forms.Application;

namespace ExplorerRevolution
{


    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            App app = new();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                HookExplorer.RestoreExplorer();
                ShellContext.shellManager.ExplorerHelper.HideExplorerTaskbar = false;
            };

            Application.Run(new ShellContext());

            app.Close();
        }

        public static void Run()
        {
            Main();
        }
    }
}
