using ExplorerRevolution.Common;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public class HookExplorer
    {
        public static void HideExplorer()
        {
            OperateTaskbar(SW_HIDE);
            OperateSecondaryTaskbars(SW_HIDE);
            HideDesktopIcons();
        }

        public static void RestoreExplorer()
        {
            OperateTaskbar(SW_SHOW);
            OperateSecondaryTaskbars(SW_SHOW);
            ShowDesktopIcons();
            EnsureTaskbarExists();
        }

        /// <summary>
        /// 兜底:若任务栏窗口缺失(例如被挂载为子窗口后随壳进程一起销毁),
        /// 重启 explorer 让系统重建原生任务栏。
        /// </summary>
        private static void EnsureTaskbarExists()
        {
            try
            {
                if (FindVisibleExplorerTaskbar() != IntPtr.Zero)
                {
                    return;
                }

                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // 忽略单个进程结束失败
                    }
                }

                Process.Start("explorer.exe");
            }
            catch
            {
                // 兜底失败时由系统在 shell 退出后自动重启 explorer
            }
        }

        /// <summary>查找 explorer 的真实任务栏(跳过本进程注册的假 Shell_TrayWnd)。</summary>
        private static IntPtr FindVisibleExplorerTaskbar()
        {
            int myPid = Process.GetCurrentProcess().Id;

            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            while (taskbar != IntPtr.Zero)
            {
                GetWindowThreadProcessId(taskbar, out uint pid);
                if (pid != (uint)myPid && IsWindowVisible(taskbar))
                {
                    return taskbar;
                }

                taskbar = FindWindowEx(IntPtr.Zero, taskbar, "Shell_TrayWnd", null);
            }

            return IntPtr.Zero;
        }

        private static IntPtr GetWorkerW()
        {
            IntPtr progman = FindWindow("Progman", null);

            SendMessage(progman, 0x052C, IntPtr.Zero, IntPtr.Zero);

            IntPtr workerw = IntPtr.Zero;

            EnumWindows((hwnd, lParam) =>
            {
                IntPtr defView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);

                if (defView != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return workerw;
        }

        public static void AttachToWorkerW(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, -16);
            SetWindowLong(hwnd, -16, (int)((style | 0x40000000) & ~0x80000000));

            SetParent(hwnd, GetWorkerW());

            ShowWindow(hwnd, 5);

            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                0x0001 | 0x0002 | 0x0040);
        }

        private static void OperateTaskbar(int status)
        {
            int myPid = Process.GetCurrentProcess().Id;

            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            while (taskbar != IntPtr.Zero)
            {
                GetWindowThreadProcessId(taskbar, out uint pid);
                if (pid != (uint)myPid)
                {
                    ShowWindow(taskbar, status);
                }

                taskbar = FindWindowEx(IntPtr.Zero, taskbar, "Shell_TrayWnd", null);
            }
        }

        private static void OperateSecondaryTaskbars(int status)
        {
            // 多显示器时的二级任务栏(Shell_SecondaryTrayWnd)
            IntPtr taskbar = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_SecondaryTrayWnd", null);

            while (taskbar != IntPtr.Zero)
            {
                ShowWindow(taskbar, status);
                taskbar = FindWindowEx(IntPtr.Zero, taskbar, "Shell_SecondaryTrayWnd", null);
            }
        }

        private static IntPtr GetDesktopListViewHandle()
        {
            IntPtr progman = FindWindow("Progman", null);

            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (defView == IntPtr.Zero)
            {
                // Windows 10/11 常见结构：WorkerW
                IntPtr workerW = IntPtr.Zero;
                do
                {
                    workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                }
                while (defView == IntPtr.Zero && workerW != IntPtr.Zero);
            }

            return FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        }

        public static void HideDesktopIcons()
        {
            IntPtr hWnd = GetDesktopListViewHandle();
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_HIDE);
            }
        }

        public static void ShowDesktopIcons()
        {
            IntPtr hWnd = GetDesktopListViewHandle();
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_SHOW);
            }
        }
    }
}
