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
        private static void Main(string[] args)
        {
            // 看门狗模式:等待主进程结束(含被强制结束),恢复 explorer 原状
            if (args.Length >= 2 && args[0] == "--watchdog")
            {
                Watchdog.Run(args[1]);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 启动看门狗:即使本进程被"结束任务"强制终止,explorer 也能被恢复
            Watchdog.Start(Process.GetCurrentProcess().Id);

            App app = new();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                HookExplorer.RestoreExplorer();
            };

            Application.Run(new ShellContext());

            app.Close();
        }

        public static void Run()
        {
            Main(Array.Empty<string>());
        }
    }
}
