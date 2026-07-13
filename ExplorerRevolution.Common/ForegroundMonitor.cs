using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public class ForegroundMonitor
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private static WinEventDelegate callback;

        public static void Start()
        {
            callback = OnForegroundChanged;

            SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                callback,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }


        private static void OnForegroundChanged(
            IntPtr hook,
            uint evt,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint thread,
            uint time)
        {
            if (hwnd != IntPtr.Zero)
            {
                Console.WriteLine(
                    $"焦点窗口改变: {hwnd}");
            }
        }
    }
}
