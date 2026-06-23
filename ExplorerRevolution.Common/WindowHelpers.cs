using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Management.Deployment;
using Windows.ApplicationModel;
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

        private static bool ShouldShowInTaskbar(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;

            // Cloaked 窗口不显示（UWP 后台窗口、虚拟桌面不在当前桌面的窗口）
            if (IsCloaked(hWnd)) return false;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

            if (isToolWindow && !isAppWindow) return false;

            IntPtr owner = GetWindow(hWnd, GW_OWNER);
            if (owner != IntPtr.Zero && !isAppWindow) return false;

            // 没有标题且没有 WS_EX_APPWINDOW 的窗口通常不显示
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            if (sb.Length == 0 && !isAppWindow) return false;

            return true;
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool cloaked, Marshal.SizeOf<bool>());
            return cloaked;
        }

        public static List<IntPtr> GetTaskbarWindows()
        {
            var result = new List<IntPtr>();
            EnumWindows((hWnd, _) =>
            {
                if (ShouldShowInTaskbar(hWnd))
                    result.Add(hWnd);
                return true;
            }, IntPtr.Zero);
            return result;
        }

        public static IntPtr GetWindowIcon(IntPtr hWnd)
        {
            const uint WM_GETICON = 0x007F;
            const int ICON_BIG = 1;
            const int ICON_SMALL2 = 2;   // 窗口类小图标
            const int GCL_HICON = -14;
            const int GCL_HICONSM = -34;

            IntPtr hIcon = IntPtr.Zero;

            // 1. 小图标
            SendMessageTimeout(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 100, out hIcon);
            if (hIcon != IntPtr.Zero) return hIcon;

            // 2. 大图标
            SendMessageTimeout(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero,
                SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG, 100, out hIcon);
            if (hIcon != IntPtr.Zero) return hIcon;

            // 3. 从窗口类获取
            hIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
            if (hIcon != IntPtr.Zero) return hIcon;

            hIcon = GetClassLongPtr(hWnd, GCL_HICON);
            return hIcon;
        }

        public static string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            return sb.ToString();
        }

        public static async Task<Windows.UI.Xaml.Media.Imaging.BitmapImage> GetUwpAppIconAsync(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;

            // AUMID 格式: PackageFamilyName!AppId
            var parts = aumid.Split('!');
            if (parts.Length < 2) return null;
            var familyName = parts[0];

            var pm = new PackageManager();
            var package = pm.FindPackagesForUser("", familyName).FirstOrDefault();
            if (package == null) return null;

            // 获取应用入口
            var apps = await package.GetAppListEntriesAsync();
            var app = apps.FirstOrDefault();
            if (app == null) return null;

            // 获取图标
            //var thumbnail = await app.GetAppInfoAsync();
            // 直接用包的 Logo
            var logoUri = package.Logo;

            var bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
            bitmapImage.UriSource = logoUri;
            return bitmapImage;
        }
    }
}
