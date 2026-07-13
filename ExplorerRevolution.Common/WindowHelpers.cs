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

        public static bool ShouldShowInTaskbar(IntPtr hWnd)
        {
            if (Helpers.IsUwpWindow(hWnd))
            {
                // 对 UWP 窗口采用更严格的判断：必须可见、未被 Cloak，并且能获取到 AUMID
                if (!IsWindowVisible(hWnd)) return false;

                if (IsCloaked(hWnd)) return false;

                var aumid = GetAppUserModelId(hWnd);
                if (string.IsNullOrEmpty(aumid))
                {
                    // 有时 AUMID 在子窗口上，尝试在子窗口上查找
                    IntPtr child = FindWindowEx(hWnd, IntPtr.Zero, null, null);
                    while (child != IntPtr.Zero)
                    {
                        aumid = GetAppUserModelId(child);
                        if (!string.IsNullOrEmpty(aumid)) break;
                        child = FindWindowEx(hWnd, child, null, null);
                    }
                }

                if (string.IsNullOrEmpty(aumid)) return false;

                return true;
            }

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

        // 检测 UWP 窗口
        /*private static bool IsUwpApplicationWindow(IntPtr hwnd)
        {
            // 方法 A：快速通过窗口类名识别
            var className = GetWindowClassName(hwnd);
            if (className == "ApplicationFrameWindow")
                return true;

            // 方法 B：通过是否存在 AppUserModelID 判断（更可靠但稍慢）
            // 可以仅在类名不确定时调用
            if (!string.IsNullOrEmpty(GetAppUserModelId(hwnd)))
                return true;

            return false;
        }*/
        public static bool IsUwpWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            try
            {
                // 优先直接读取窗口的 AppUserModelID，最可靠
                var aumid = GetAppUserModelId(hwnd);
                if (!string.IsNullOrEmpty(aumid)) return true;

                // 一些 UWP 窗口以 ApplicationFrameWindow 为顶级类名
                var cls = GetWindowClassName(hwnd);
                if (cls == "ApplicationFrameWindow") return true;

                // 有时 AUMID 在子窗口上（被框架包装），尝试遍历子窗口查找
                IntPtr child = FindWindowEx(hwnd, IntPtr.Zero, null, null);
                while (child != IntPtr.Zero)
                {
                    aumid = GetAppUserModelId(child);
                    if (!string.IsNullOrEmpty(aumid)) return true;
                    child = FindWindowEx(hwnd, child, null, null);
                }
            }
            catch
            {
                // 忽略错误，按非 UWP 处理
            }

            return false;
        }

        public static async Task<Windows.UI.Xaml.Media.Imaging.BitmapImage> GetUwpAppIconAsync(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;
            // AUMID 格式: PackageFamilyName!AppId （部分情况可能只包含 PackageFamilyName）
            var parts = aumid.Split('!');
            var familyName = parts.Length > 0 ? parts[0] : null;
            var appId = parts.Length > 1 ? parts[1] : null;

            if (string.IsNullOrEmpty(familyName)) return null;

            var pm = new PackageManager();
            var package = pm.FindPackagesForUser("", familyName).FirstOrDefault();
            if (package == null) return null;

            var apps = await package.GetAppListEntriesAsync();
            object selectedApp = null;

            if (!string.IsNullOrEmpty(appId))
            {
                foreach (var a in apps)
                {
                    try
                    {
                        var t = a.GetType();
                        var prop = t.GetProperty("AppUserModelId") ?? t.GetProperty("Id");
                        if (prop != null)
                        {
                            var val = prop.GetValue(a) as string;
                            if (!string.IsNullOrEmpty(val) && (val.Equals(appId, StringComparison.OrdinalIgnoreCase) || val.EndsWith("!" + appId, StringComparison.OrdinalIgnoreCase) || val.EndsWith(appId, StringComparison.OrdinalIgnoreCase)))
                            {
                                selectedApp = a;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            if (selectedApp == null)
                selectedApp = apps.FirstOrDefault();

            Uri logoUri = null;
            if (selectedApp != null)
            {
                try
                {
                    var diProp = selectedApp.GetType().GetProperty("DisplayInfo");
                    if (diProp != null)
                    {
                        var di = diProp.GetValue(selectedApp);
                        if (di != null)
                        {
                            // 尝试读取 DisplayInfo.Logo
                            var logoProp = di.GetType().GetProperty("Logo") ?? di.GetType().GetProperty("LogoUri") ?? di.GetType().GetProperty("LogoPath");
                            if (logoProp != null)
                            {
                                var logoVal = logoProp.GetValue(di);
                                if (logoVal is Uri u) logoUri = u;
                                else if (logoVal != null) {
                                    Uri.TryCreate(logoVal.ToString(), UriKind.RelativeOrAbsolute, out logoUri);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (logoUri == null)
            {
                try { logoUri = package.Logo; } catch { logoUri = null; }
            }

            // 缓存按 aumid
            try
            {
                if (_uwpIconCache.TryGetValue(aumid, out var cached))
                    return cached;
            }
            catch { }

            Windows.UI.Xaml.Media.Imaging.BitmapImage bitmapImage = null;

            // 优先使用已知的 logoUri
            if (logoUri != null)
            {
                try
                {
                    bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    bitmapImage.UriSource = logoUri;
                }
                catch { bitmapImage = null; }
            }

            // 如果没有合适的 logoUri，则尝试从包的 InstalledLocation 中查找更合适的资源（Assets 或根目录）
            if (bitmapImage == null)
            {
                try
                {
                    var installed = package.InstalledLocation;
                    Windows.Storage.StorageFolder searchFolder = null;
                    try
                    {
                        searchFolder = await installed.GetFolderAsync("Assets");
                    }
                    catch { searchFolder = installed; }

                    var files = await searchFolder.GetFilesAsync();
                    var candidates = new List<Windows.Storage.StorageFile>();
                    foreach (var f in files)
                    {
                        var name = f.Name.ToLowerInvariant();
                        if (name.Contains("logo") || name.Contains("tile") || name.Contains("store") || name.Contains("square"))
                            candidates.Add(f);
                    }

                    // 按 scale 优先选择
                    string[] scales = new[] { ".scale-400", ".scale-256", ".scale-200", ".scale-150", ".scale-125", ".scale-100" };
                    Windows.Storage.StorageFile pick = null;
                    foreach (var sc in scales)
                    {
                        pick = candidates.FirstOrDefault(f => f.Name.ToLowerInvariant().Contains(sc));
                        if (pick != null) break;
                    }

                    // 若没按 scale 找到，优先选择包含 square44 或 square150 等的文件
                    if (pick == null)
                        pick = candidates.FirstOrDefault(f => f.Name.ToLowerInvariant().Contains("square44") || f.Name.ToLowerInvariant().Contains("square150") || f.Name.ToLowerInvariant().Contains("square"));

                    // 再退回到任意候选
                    if (pick == null && candidates.Count > 0)
                        pick = candidates.OrderByDescending(f => f.Name.Length).FirstOrDefault();

                    if (pick != null)
                    {
                        try
                        {
                            using (var stream = await pick.OpenReadAsync())
                            {
                                var bi = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                                await bi.SetSourceAsync(stream);
                                bitmapImage = bi;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 最终回退到 package.Logo 或 null
            if (bitmapImage == null && logoUri != null)
            {
                try
                {
                    bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    bitmapImage.UriSource = logoUri;
                }
                catch { bitmapImage = null; }
            }

            try
            {
                if (bitmapImage != null)
                    _uwpIconCache[aumid] = bitmapImage;
            }
            catch { }

            return bitmapImage;
        }

        static System.Collections.Concurrent.ConcurrentDictionary<string, Windows.UI.Xaml.Media.Imaging.BitmapImage> _uwpIconCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Windows.UI.Xaml.Media.Imaging.BitmapImage>();
    }
}
