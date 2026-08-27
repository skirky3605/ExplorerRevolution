using System;

namespace ExplorerRevolution.Common
{
    /// <summary>
    /// 极简日志(骨架参考 ManagedShell.Common.Logging.ShellLogger,后续可加文件/控制台观察者)。
    /// </summary>
    public static class ShellLog
    {
        public static void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine("[Shell] " + message);
        }

        public static void Info(string message)
        {
            System.Diagnostics.Debug.WriteLine("[Shell] " + message);
        }

        public static void Warning(string message)
        {
            System.Diagnostics.Debug.WriteLine("[Shell][WARN] " + message);
        }

        public static void Error(string message, Exception ex = null)
        {
            System.Diagnostics.Debug.WriteLine("[Shell][ERROR] " + message + (ex != null ? " " + ex : ""));
        }
    }
}
