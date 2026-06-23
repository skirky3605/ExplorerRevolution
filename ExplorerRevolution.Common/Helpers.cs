using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
