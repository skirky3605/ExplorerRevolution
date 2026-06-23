using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public static class WindowHelpers
    {
        public class TaskBarIcon
        {
            public enum ProcessState
            {
                None,
                Processing,
                Value
            }

            public static string Title;
            public static BitmapSource iconSource;
            public static ProcessState ProcessStatus;
            public static int ProcessValue;
        }

        public static List<(IntPtr Hwnd, string Title)> GetTaskbarWindows()
        {
            var result = new List<(IntPtr, string)>();

            IntPtr shellWindow = GetShellWindow();

            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == shellWindow)
                    return true;

                if (!IsWindowVisible(hWnd))
                    return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                var sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);

                result.Add((hWnd, sb.ToString()));

                return true;
            }, IntPtr.Zero);

            return result;
        }

        public static List<TaskBarIcon> GetTaskBarIcons()
        {
            var windows = GetTaskbarWindows();
            foreach (var item in windows)
            {
                Debug.WriteLine(item.Hwnd);
                Debug.WriteLine(item.Title);
            }
            return null;
        }
    }
}
