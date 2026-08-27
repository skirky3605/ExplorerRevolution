using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
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
                return true;

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
            GetWindowThreadProcessId(hwnd, out uint pid);

            try
            {
                var process = Process.GetProcessById((int)pid);

                return process.ProcessName == "ApplicationFrameHost" &&
                       !string.IsNullOrEmpty(GetAppUserModelId(hwnd));
            }
            catch
            {
                return false;
            }
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

            // 方式 1:从 AppxManifest.xml 解析图标文件。
            // 不直接用 package.Logo:Store 包返回 ms-appx:/// URI,在非打包宿主(Mile.Xaml)中无法解析。
            string iconPath = ResolveManifestIconPath(package.InstalledLocation.Path);
            if (iconPath != null)
            {
                try
                {
                    // 读取时裁剪掉透明留白,使任务栏中的视觉尺寸与 Win32 图标一致
                    return await LoadUwpIconBitmapImageAsync(iconPath);
                }
                catch { }
            }

            // 方式 2:AppListEntry.DisplayInfo.GetLogo 直接取图标流(自带缩放)
            try
            {
                var apps = await package.GetAppListEntriesAsync();
                var app = apps.FirstOrDefault();
                if (app != null)
                {
                    var streamRef = app.DisplayInfo.GetLogo(new Windows.Foundation.Size(44, 44));
                    if (streamRef != null)
                    {
                        using (var stream = await streamRef.OpenReadAsync())
                        {
                            var bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmapImage.SetSourceAsync(stream);
                            return bitmapImage;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从包的 AppxManifest.xml 解析任务栏图标文件路径(uap:VisualElements/Square44x44Logo 等),
        /// 并解析 scale 限定符,返回最接近 150% 缩放的实体文件。
        /// </summary>
        public static string ResolveManifestIconPath(string packagePath)
        {
            try
            {
                string manifestPath = System.IO.Path.Combine(packagePath, "AppxManifest.xml");
                if (!System.IO.File.Exists(manifestPath)) return null;

                var doc = new XmlDocument();
                doc.Load(manifestPath);

                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("x", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
                ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

                var iconNode = doc.SelectSingleNode("//x:Application/uap:VisualElements/@Square44x44Logo", ns)
                            ?? doc.SelectSingleNode("//x:Application/uap:VisualElements/@Square30x30Logo", ns)
                            ?? doc.SelectSingleNode("//x:Application/uap:VisualElements/@Square150x150Logo", ns);
                if (iconNode == null) return null;

                string relative = iconNode.Value.Replace('/', '\\');
                string basePath = System.IO.Path.Combine(packagePath, relative);
                if (System.IO.File.Exists(basePath)) return basePath;

                // 处理 scale 限定符:资源名形如 "Assets\Logo.png",实际文件可能为 "Logo.scale-100.png" 等
                string dir = System.IO.Path.GetDirectoryName(basePath);
                string stem = System.IO.Path.GetFileNameWithoutExtension(basePath);
                string ext = System.IO.Path.GetExtension(basePath);

                if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                {
                    string bestPath = null;
                    int bestScore = int.MaxValue;

                    foreach (string file in System.IO.Directory.GetFiles(dir, stem + "*.scale-*" + ext))
                    {
                        string name = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (name.IndexOf("_contrast-", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        Match m = Regex.Match(name, @"\.scale-(\d+)$");
                        if (m.Success)
                        {
                            int scale = int.Parse(m.Groups[1].Value);
                            int score = Math.Abs(scale - 150);
                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestPath = file;
                            }
                        }
                    }

                    if (bestPath != null) return bestPath;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据 AUMID 解析 UWP 应用的 manifest 图标实体文件路径(供 WinForms 弹层直接加载 PNG)。
        /// </summary>
        public static string ResolveUwpIconPath(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;

            // AUMID 格式: PackageFamilyName!AppId
            var parts = aumid.Split('!');
            if (parts.Length < 2) return null;
            var familyName = parts[0];

            try
            {
                var pm = new PackageManager();
                var package = pm.FindPackagesForUser("", familyName).FirstOrDefault();
                if (package == null) return null;

                return ResolveManifestIconPath(package.InstalledLocation.Path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取文件/快捷方式的大图标(SHGetFileInfo,.lnk 会解析为目标程序图标)。
        /// 调用方负责在转换完成后销毁返回的 HICON。
        /// </summary>
        public static IntPtr GetFileIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return IntPtr.Zero;

            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_ICON | SHGFI_LARGEICON);
                return shfi.hIcon;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 解析 .lnk 快捷方式的目标路径(WScript.Shell COM)。
        /// 失败返回 null,调用方应回退到原 .lnk。
        /// </summary>
        public static string ResolveShortcutTarget(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath)) return null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;

                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { shortcutPath });

                    try
                    {
                        string target = shortcut.GetType().InvokeMember(
                            "TargetPath",
                            BindingFlags.GetProperty,
                            null,
                            shortcut,
                            null) as string;

                        return string.IsNullOrEmpty(target) ? null : target;
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取 .lnk 快捷方式的可执行目标路径。
        /// 优先用 TargetPath;为空时(如 File Explorer)回退 IconLocation 中的文件路径。
        /// </summary>
        public static string GetShortcutTargetPath(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath)) return null;

            string target = ResolveShortcutTarget(shortcutPath);
            if (!string.IsNullOrEmpty(target))
            {
                return target;
            }

            string iconLocation = GetShortcutIconLocation(shortcutPath);
            if (!string.IsNullOrEmpty(iconLocation))
            {
                int comma = iconLocation.LastIndexOf(',');
                string path = comma > 0 ? iconLocation.Substring(0, comma).Trim() : iconLocation.Trim();
                path = Environment.ExpandEnvironmentVariables(path);
                if (path.IndexOf('%') < 0)
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// 读取 .lnk 的 System.AppUserModel.ID 属性(Shell.Application COM)。
        /// 原生任务栏正是用该属性把固定项与运行窗口关联起来。
        /// </summary>
        public static string GetShortcutAppUserModelId(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath)) return null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;

                object shell = Activator.CreateInstance(shellType);
                object folder = null;
                object item = null;
                try
                {
                    folder = shellType.InvokeMember(
                        "Namespace",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { Path.GetDirectoryName(shortcutPath) });

                    item = folder.GetType().InvokeMember(
                        "ParseName",
                        BindingFlags.InvokeMethod,
                        null,
                        folder,
                        new object[] { Path.GetFileName(shortcutPath) });

                    object value = item.GetType().InvokeMember(
                        "ExtendedProperty",
                        BindingFlags.InvokeMethod,
                        null,
                        item,
                        new object[] { "System.AppUserModel.ID" });

                    string aumid = value as string;
                    return string.IsNullOrEmpty(aumid) ? null : aumid;
                }
                finally
                {
                    if (item != null) Marshal.FinalReleaseComObject(item);
                    if (folder != null) Marshal.FinalReleaseComObject(folder);
                    Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取 .lnk 快捷方式的图标(不含快捷方式小箭头)。
        /// 优先取目标 exe 的图标;目标为空时(如 File Explorer)改用 IconLocation 指定的
        /// 文件与图标索引;都失败时回退 .lnk 自身图标(可能带小箭头)。
        /// 调用方负责在转换完成后销毁返回的 HICON。
        /// </summary>
        public static IntPtr GetShortcutIcon(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath)) return IntPtr.Zero;

            try
            {
                // 1) 目标 exe 图标(无小箭头)
                string target = ResolveShortcutTarget(shortcutPath);
                IntPtr hIcon = !string.IsNullOrEmpty(target) ? GetFileIcon(target) : IntPtr.Zero;
                if (hIcon != IntPtr.Zero)
                {
                    return hIcon;
                }

                // 2) IconLocation,如 %windir%\explorer.exe,0
                string iconLocation = GetShortcutIconLocation(shortcutPath);
                if (!string.IsNullOrEmpty(iconLocation))
                {
                    hIcon = GetIconFromLocation(iconLocation);
                    if (hIcon != IntPtr.Zero)
                    {
                        return hIcon;
                    }
                }

                // 3) 回退:.lnk 自身图标
                return GetFileIcon(shortcutPath);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>读取 .lnk 的 IconLocation 属性(如 "%windir%\explorer.exe,0")。</summary>
        private static string GetShortcutIconLocation(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;

                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { shortcutPath });

                    try
                    {
                        return shortcut.GetType().InvokeMember(
                            "IconLocation",
                            BindingFlags.GetProperty,
                            null,
                            shortcut,
                            null) as string;
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>按 "path,index" 格式从文件提取指定索引的图标。</summary>
        private static IntPtr GetIconFromLocation(string iconLocation)
        {
            try
            {
                string path = iconLocation;
                int index = 0;
                int comma = iconLocation.LastIndexOf(',');
                if (comma > 0)
                {
                    string idxStr = iconLocation.Substring(comma + 1).Trim();
                    if (int.TryParse(idxStr, out int parsed))
                    {
                        index = parsed;
                        path = iconLocation.Substring(0, comma).Trim();
                    }
                }

                if (string.IsNullOrEmpty(path))
                {
                    return IntPtr.Zero;
                }

                path = Environment.ExpandEnvironmentVariables(path);
                if (path.IndexOf('%') >= 0)
                {
                    return IntPtr.Zero; // 仍有未展开的环境变量,跳过
                }

                if (ExtractIconEx(path, index, out IntPtr hLarge, out IntPtr hSmall, 1) > 0)
                {
                    if (hLarge != IntPtr.Zero)
                    {
                        if (hSmall != IntPtr.Zero)
                        {
                            DestroyIcon(hSmall);
                        }
                        return hLarge;
                    }

                    return hSmall;
                }
            }
            catch
            {
                // 忽略,走回退
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 读取图标文件,裁掉透明留白后重新编码为 PNG 字节。
        /// UWP 的 Square44x44Logo 等资产通常带透明边距,直接渲染会显得比 Win32 图标小一圈;
        /// 裁剪后两者视觉尺寸一致。失败时返回 null,调用方应回退到原文件。
        /// </summary>
        public static byte[] GetCroppedPngBytes(string iconPath)
        {
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(iconPath)))
                using (var bmp = new Bitmap(ms))
                {
                    Rectangle content = GetOpaqueBounds(bmp);
                    if (content.Width <= 0 || content.Height <= 0)
                    {
                        return null;
                    }

                    // 内容四周保留 1px 边距,避免贴边
                    content.Inflate(1, 1);
                    content.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));

                    using (var cropped = bmp.Clone(content, bmp.PixelFormat))
                    using (var outMs = new MemoryStream())
                    {
                        cropped.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
                        return outMs.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 加载 UWP manifest 图标为 WinForms Image(已裁剪透明留白)。
        /// </summary>
        public static Image LoadCroppedIconImage(string iconPath)
        {
            try
            {
                byte[] bytes = GetCroppedPngBytes(iconPath) ?? File.ReadAllBytes(iconPath);
                var ms = new MemoryStream(bytes);
                return Image.FromStream(ms); // 流由 Image 持有,随 Image 一起释放
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 加载 UWP manifest 图标为 XAML BitmapImage(已裁剪透明留白,失败回退原文件)。
        /// </summary>
        public static async Task<Windows.UI.Xaml.Media.Imaging.BitmapImage> LoadUwpIconBitmapImageAsync(string iconPath)
        {
            try
            {
                byte[] bytes = GetCroppedPngBytes(iconPath) ?? File.ReadAllBytes(iconPath);

                using (var ms = new MemoryStream(bytes))
                {
                    var bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    var ras = ms.AsRandomAccessStream();
                    await bitmapImage.SetSourceAsync(ras);
                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>计算图片不透明内容(alpha &gt; 8)的包围矩形。</summary>
        private static Rectangle GetOpaqueBounds(Bitmap bmp)
        {
            int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
            var full = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData data = null;

            try
            {
                data = bmp.LockBits(full, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                int stride = Math.Abs(data.Stride);
                byte[] buf = new byte[stride * data.Height];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                for (int y = 0; y < data.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < data.Width; x++)
                    {
                        if (buf[row + x * 4 + 3] > 8)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            catch
            {
                return Rectangle.Empty;
            }
            finally
            {
                if (data != null)
                {
                    bmp.UnlockBits(data);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
