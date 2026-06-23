using ExplorerRevolution.Common;
using System;
using System.Threading.Tasks;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public class HookExplorer
    {
        public static void HideExplorer()
        {
            OperateTaskbar(SW_HIDE);
            OperateDesktop(SW_HIDE);
        }

        public static void RestoreExplorer()
        {
            OperateTaskbar(SW_SHOW);
            OperateDesktop(SW_SHOW);
            KeyInputHandler.Uninstall();
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
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);

            if (taskbar != IntPtr.Zero)
            {
                ShowWindow(taskbar, status);
            }
        }

        private static void OperateDesktop(int status)
        {
            IntPtr progman = FindWindow("Progman", null);

            if (progman != IntPtr.Zero)
            {
                ShowWindow(progman, status);
            }
        }
    }
}
