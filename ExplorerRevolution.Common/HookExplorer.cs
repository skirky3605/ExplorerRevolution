using System;
using System.Threading.Tasks;

namespace ExplorerRevolution.Common
{
    public class HookExplorer
    {
        public static Task HideExplorer()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
            {
                ShowWindow(taskbar, SW_HIDE);
            }
        }
    }
}
