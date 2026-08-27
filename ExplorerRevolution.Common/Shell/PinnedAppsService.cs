using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Management.Deployment;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    /// <summary>
    /// 任务栏固定图标条目。
    /// Win32 应用对应 User Pinned\TaskBar 下的 .lnk;
    /// MSIX/UWP 应用没有 .lnk,以 AUMID 形式存放在注册表 Taskband\Favorites 中。
    /// </summary>
    public class PinnedApp
    {
        public string Name;
        public string ShortcutPath;
        public string AppUserModelId;
        public bool IsUwp;

        internal string Key => !string.IsNullOrEmpty(AppUserModelId)
            ? "aumid:" + AppUserModelId
            : "lnk:" + (ShortcutPath ?? "").ToLowerInvariant();
    }

    /// <summary>
    /// 任务栏固定图标枚举。
    /// Windows 11 的任务栏固定项同时存在两种存储:
    ///   1. User Pinned\TaskBar 目录下的 .lnk(经典 Win32 应用);
    ///   2. 注册表 HKCU\...\Explorer\Taskband\Favorites 二进制(含 .lnk 短文件名与 MSIX AUMID)。
    /// 本服务按 Favorites 中的出现顺序解析两者,并以目录枚举兜底。
    /// </summary>
    public static class PinnedAppsService
    {
        public static string PinnedFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            }
        }

        public static List<PinnedApp> GetPinnedApps()
        {
            var result = new List<PinnedApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] lnkFiles;
            try
            {
                lnkFiles = Directory.Exists(PinnedFolder)
                    ? Directory.EnumerateFiles(PinnedFolder, "*.lnk").ToArray()
                    : Array.Empty<string>();
            }
            catch
            {
                lnkFiles = Array.Empty<string>();
            }

            // 1. 按任务栏顺序解析 Taskband\Favorites(.lnk 与 MSIX 交错出现)
            foreach (var app in ParseTaskbandFavorites(lnkFiles))
            {
                if (app != null && seen.Add(app.Key))
                {
                    result.Add(app);
                }
            }

            // 2. 兜底:目录中未被注册表覆盖的 .lnk(解析失败或新增文件时)
            foreach (string lnk in lnkFiles)
            {
                var app = new PinnedApp
                {
                    Name = Path.GetFileNameWithoutExtension(lnk),
                    ShortcutPath = lnk,
                    IsUwp = false
                };

                if (seen.Add(app.Key))
                {
                    result.Add(app);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析 Taskband\Favorites 二进制。
        /// 该格式未公开且混合编码:.lnk 名称以 ANSI 字节存储(多为 8.3 短名),
        /// MSIX 包字段以 UTF-16 存储(包族名或完整 AUMID)。
        /// 这里不依赖具体布局,分别扫描两种特征串,顺序与任务栏一致。
        /// </summary>
        private static IEnumerable<PinnedApp> ParseTaskbandFavorites(string[] lnkFiles)
        {
            byte[] blob;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband"))
                {
                    if (key == null)
                    {
                        yield break;
                    }

                    blob = key.GetValue("Favorites") as byte[];
                }
            }
            catch
            {
                yield break;
            }

            if (blob == null || blob.Length == 0)
            {
                yield break;
            }

            // 1) .lnk 名称:ANSI 字节层扫描(8.3 短名,如 GOOGLE~1.LNK)
            string ansi = Encoding.GetEncoding(28591).GetString(blob);
            var lnkRegex = new Regex(
                @"[A-Za-z0-9 _\-\(\)\.~]{1,80}\.LNK",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (Match m in lnkRegex.Matches(ansi))
            {
                string path = MatchLnk(lnkFiles, m.Value);
                if (path != null)
                {
                    yield return new PinnedApp
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        ShortcutPath = path,
                        IsUwp = false
                    };
                }
            }

            // 2) MSIX:UTF-16 层扫描。
            // 注意:固定项可能只存"包族名"(如 AppleInc.AppleMusicWin_nzyj5cx40ttqa),
            // 也可能直接存完整 AUMID(如 Microsoft.WindowsStore_...!App),因此 !App 部分为可选。
            string text = Encoding.Unicode.GetString(blob);
            var uwpRegex = new Regex(
                @"[A-Za-z0-9_.]+\.[A-Za-z0-9_.]+_[A-Za-z0-9]{13}(?:![A-Za-z0-9_.]+)?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (Match m in uwpRegex.Matches(text))
            {
                var uwpApp = CreateUwpApp(m.Value);
                if (uwpApp != null)
                {
                    yield return uwpApp;
                }
            }
        }

        private static string MatchLnk(string[] lnkFiles, string token)
        {
            foreach (string file in lnkFiles)
            {
                if (string.Equals(Path.GetFileName(file), token, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            // 注册表里可能存的是 8.3 短名(如 GOOGLE~1.LNK)
            foreach (string file in lnkFiles)
            {
                string shortPath = GetShortPathName(file);
                if (!string.IsNullOrEmpty(shortPath)
                    && string.Equals(Path.GetFileName(shortPath), token, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return null;
        }

        private static PinnedApp CreateUwpApp(string aumid)
        {
            try
            {
                // 二进制里可能只有包族名,先用族名解析包
                string familyName = aumid.Split('!')[0];

                var pm = new PackageManager();
                var package = pm.FindPackagesForUser("", familyName).FirstOrDefault();
                if (package == null)
                {
                    return null;
                }

                // AppListEntry 只包含可启动条目,且自带完整 AUMID(如 ...!App)
                var entries = package.GetAppListEntries();
                if (entries == null || entries.Count == 0)
                {
                    return null;
                }

                var entry = entries.FirstOrDefault(e =>
                           string.Equals(e.AppUserModelId, aumid, StringComparison.OrdinalIgnoreCase))
                        ?? entries.First();

                string displayName = entry.DisplayInfo?.DisplayName;
                string name = string.IsNullOrEmpty(displayName) ? familyName : displayName;

                return new PinnedApp
                {
                    Name = name,
                    AppUserModelId = entry.AppUserModelId,
                    IsUwp = true
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
