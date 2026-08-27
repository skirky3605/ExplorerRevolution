using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace ExplorerRevolution.Common
{
    /// <summary>
    /// 开始菜单应用条目(纯数据,UWP 与应用图标由 UI 层转换为 BitmapImage)。
    /// </summary>
    public class StartMenuApp
    {
        public string Name;
        public string IconPath;
        public string AppUserModelId;
        public string ShortcutPath;
        public bool IsUwp;
    }

    /// <summary>
    /// 开始菜单应用枚举:UWP 应用(PackageManager + AppListEntry,名称自动本地化)
    /// + 开始菜单快捷方式(.lnk)。
    /// </summary>
    public static class StartMenuService
    {
        public static async Task<List<StartMenuApp>> GetAppsAsync()
        {
            var result = new List<StartMenuApp>();

            result.AddRange(await GetUwpAppsAsync());
            result.AddRange(GetShortcutApps());

            return result.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static async Task<List<StartMenuApp>> GetUwpAppsAsync()
        {
            var list = new List<StartMenuApp>();

            try
            {
                var pm = new PackageManager();

                foreach (var pkg in pm.FindPackagesForUser(""))
                {
                    try
                    {
                        var entries = await pkg.GetAppListEntriesAsync();
                        string iconPath = WindowHelpers.ResolveManifestIconPath(pkg.InstalledLocation.Path);

                        foreach (var entry in entries)
                        {
                            string name = entry.DisplayInfo?.DisplayName;
                            if (string.IsNullOrEmpty(name))
                            {
                                name = pkg.Id.Name;
                            }

                            list.Add(new StartMenuApp
                            {
                                Name = name,
                                IconPath = iconPath,
                                AppUserModelId = entry.AppUserModelId,
                                IsUwp = true
                            });
                        }
                    }
                    catch
                    {
                        // 单个包失败不影响其余包
                    }
                }
            }
            catch
            {
                // 包枚举失败时降级为仅快捷方式
            }

            return list;
        }

        private static List<StartMenuApp> GetShortcutApps()
        {
            var list = new List<StartMenuApp>();

            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };

            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;

                try
                {
                    foreach (string lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        list.Add(new StartMenuApp
                        {
                            Name = Path.GetFileNameWithoutExtension(lnk),
                            IconPath = lnk,
                            ShortcutPath = lnk,
                            IsUwp = false
                        });
                    }
                }
                catch
                {
                    // 目录读取失败跳过
                }
            }

            return list;
        }
    }
}
