using ExplorerRevolution.Common;
using System;
using System.Diagnostics;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 看门狗:以独立进程监控主进程。主进程无论以何种方式结束(包括被任务管理器
    /// "结束任务"强制终止,此时 AppDomain.ProcessExit 不会触发),都会恢复
    /// explorer 的任务栏与桌面图标,避免壳程序被杀后系统任务栏一直隐藏。
    /// </summary>
    public static class Watchdog
    {
        private const string ARG_WATCHDOG = "--watchdog";

        /// <summary>
        /// 以当前进程为目标,在独立进程中启动看门狗。
        /// </summary>
        public static void Start(int targetPid)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"{ARG_WATCHDOG} {targetPid}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
            }
            catch
            {
                // 看门狗启动失败不应阻塞主程序运行
            }
        }

        /// <summary>
        /// 看门狗入口:等待目标进程退出,然后恢复 explorer 原状并退出自身。
        /// </summary>
        public static void Run(string targetPid)
        {
            if (!int.TryParse(targetPid, out int pid))
            {
                return;
            }

            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    process.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                // 目标进程已不存在,直接进入恢复流程
            }
            catch (Exception)
            {
                // 等待过程出现异常,仍尝试恢复
            }

            try
            {
                HookExplorer.RestoreExplorer();
            }
            catch
            {
                // 恢复失败不影响看门狗退出
            }
        }
    }
}
