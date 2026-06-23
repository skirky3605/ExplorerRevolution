using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public static class KeyInputHandler
    {
        private static LowLevelMouseProc _proc = HookCallback;

        public static void Install()
        {
            _hookId = SetHook(_proc);
        }

        public static void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(14, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_RBUTTONDOWN = 0x204;
            const int WM_RBUTTONUP = 0x205;

            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();

                if (msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP)
                {
                    // ❗ 吃掉右键事件
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
