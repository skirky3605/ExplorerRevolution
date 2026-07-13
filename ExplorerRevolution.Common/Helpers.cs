using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public static class Helpers
    {
        public static void SetMicaBackdrop(IntPtr hwnd)
        {
            int backdrop = 2; // 2 = Mica

            DwmSetWindowAttribute(
                hwnd,
                38,
                ref backdrop,
                4);
        }
        public static void SetTranspartBackdrop(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);

            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        }

        public static void HideFromAltTab(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        public static bool IsUwpWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(hwnd, out uint pid);

            IntPtr process = OpenProcess(
                PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                pid);

            if (process == IntPtr.Zero)
                return false;

            try
            {
                uint length = 0;

                // 第一次调用获取长度
                GetApplicationUserModelId(
                    process,
                    ref length,
                    IntPtr.Zero);

                if (length == 0)
                    return false;

                IntPtr buffer = Marshal.AllocHGlobal((int)length);

                try
                {
                    int result = GetApplicationUserModelId(
                        process,
                        ref length,
                        buffer);

                    if (result == 0)
                    {
                        string aumid =
                            Marshal.PtrToStringUni(buffer);

                        // 有 AUMID 基本就是 UWP/打包应用
                        return !string.IsNullOrEmpty(aumid);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(process);
            }

            return false;
        }
    }
}
