# ExplorerRevolution 路线图与实现方案

> 参考蓝本:[cairoshell/ManagedShell](https://github.com/cairoshell/ManagedShell)
> 参考版本:0.0.346(NuGet 基准),Apache-2.0 许可。
> 参考实现:Cairo Shell、RetroBar。

## 1. 背景与约束

当前工程的技术底座是 **WinForms 宿主 + Mile.Xaml 托管的 UWP XAML**:

- 窗口宿主:WinForms Form(`ShellContext` 中的桌面层 / 任务栏层)
- UI:XAML 页面运行在 UWP 运行时(`Windows.UI.Xaml`,`Windows.UI.Core` 线程模型)
- 目标框架:net48 + Windows SDK Contracts

而 ManagedShell 是 **纯 WPF 栈** 设计的库(`DependencyObject`、`ICollectionView`、
`ImageSource`、`DispatcherTimer`、`AppBarWindow : Window` 均为 WPF 类型),
与 UWP 底层的类型系统、Dispatcher 和窗口模型不能完全兼容。

**结论:不直接引用 ManagedShell 程序集。** 改为将其作为"架构蓝本",把其
与 UI 栈无关的 Win32/COM 服务层逻辑移植到本项目,UI 侧保持 UWP/Mile.Xaml 原生。

## 2. 为什么仍然值得参考 ManagedShell

ManagedShell 的难点不在"UI 怎么写",而在于它踩平了的系统级细节,这些与 WPF 无关:

- `SHELLHOOK` 注册/注销时序、`TaskbarCreated` / `TaskbarButtonCreated` 消息
- cloak/uncloak(`DWMWA_CLOAKED`)与最小化、闪烁、前台状态的完整状态机
- 任务栏按钮过滤规则(可见性、toolwindow、owner、标题、AUMID)
- 托盘区(`TrayNotifyWnd` 子类化、`TaskbarCreated` 重发、图标增删/悬浮/点击)
- AppBar 消息(`SHAppBarMessage`、工作区保留、自动隐藏、多显示器)
- UWP 应用枚举与图标(解析 Package manifest、`shell:appsfolder`)
- ShellFolders(桌面/文件图标、右键菜单、文件操作,纯 COM 互操作)

这些逻辑可以直接移植(或原样复制后替换 WPF 类型),Apache-2.0 允许这样做,
前提是保留版权与许可声明(见 §5 许可证)。

## 3. 移植策略

### 3.1 分级

| 级别 | 内容 | 处理方式 |
| --- | --- | --- |
| **直接移植** | 纯 Win32/COM,与 UI 栈无关:`NativeMethods`(分文件)、`ShellFolders` 全部、`ExplorerHelper`、`ShellHelper`、`WindowHelper`、`IconHelper`、`StoreAppHelper`、`ImmersiveShellHelper`、`AppVisibilityHelper`、`StartupRunner`、`Logging` 骨架 | 复制/改写,保留 Apache-2.0 头 |
| **改写移植** | 核心逻辑保留,仅替换 WPF 类型:`TasksService`/`Tasks`/`ApplicationWindow`、`TrayService`/`NotificationArea`/`NotifyIcon`、`FullScreenHelper`、`AppBarManager` 的 ABM 逻辑 | 按 §4 映射表改写 |
| **仅参考** | WPF 专属管线:`AppBarWindow` 的 WPF Window 生命周期、`ICollectionView` 分组绑定 | 不移植,用本项目等价物替代 |

### 3.2 新工程结构

```text
ExplorerRevolution.slnx
├─ reference/ManagedShell/        # 固定版本源码快照(仅参考,不加入编译)
├─ ExplorerRevolution.Common      # 升级为"壳服务层",按 ManagedShell 风格分目录
│  ├─ Interop/                    # NativeMethods 按模块分文件(User32/DwmApi/Shell32/Ole32…)
│  ├─ Services/                   # TasksService、TrayService、ExplorerHelper、FullScreenHelper、AppVisibilityHelper
│  ├─ Shell/                      # ShellFolder、ShellItem、ShellFile、ShellContextMenu、FileOperationWorker
│  ├─ Helpers/                    # ShellHelper、WindowHelper、IconHelper、VolumeHelper、PowerHelper、SoundHelper
│  ├─ UWP/                        # StoreAppHelper、ImmersiveShellHelper(COM 互操作,与 UI 栈无关)
│  └─ Logging/                    # 自建 ShellLogger 风格日志
└─ ExplorerRevolution.UI          # 保持 Mile.Xaml,消费 Common 服务,UWP 原生数据模型
```

## 4. 类型兼容映射:WPF → UWP

改写移植时按此表替换类型,服务逻辑保持不变:

| ManagedShell(WPF) | 本项目等价物(UWP) | 说明 |
| --- | --- | --- |
| `System.Windows.DependencyObject`(TasksService/NotificationArea 基类) | 普通类 + `INotifyPropertyChanged` | 服务层无需依赖属性;UI 侧 ViewModel 自行实现绑定 |
| `System.Windows.Data.ICollectionView`(分组/过滤) | `Windows.UI.Xaml.Data.ICollectionView` 或自建分组 VM | **适配点**:UWP `CollectionViewSource` 没有 `Filter`/`GroupDescriptions`,分组用 `IsSourceGrouped` + `ItemsPath`,过滤在 VM 层完成 |
| `System.Windows.Media.ImageSource`(图标) | `Windows.UI.Xaml.Media.Imaging.BitmapImage` | 沿用现有 HICON→PNG→`BitmapImage` 管线;UWP 图标直接取 manifest 图标路径 |
| `System.Windows.Threading.DispatcherTimer` | `Windows.UI.Xaml.DispatcherTimer` | 基于 `CoreDispatcher`,Mile.Xaml 宿主内可用;Phase 0 验证 |
| `System.Windows.Window` + `WindowInteropHelper`(AppBarWindow) | WinForms `Form.Handle` + `WindowsXamlHost` | 维持现有宿主;`SHAppBarMessage`/`APPBARDATA` 逻辑原样移植 |
| `Dispatcher.BeginInvoke` | `CoreDispatcher.RunAsync` / `DispatcherQueue.TryEnqueue` | 事件回调统一编排到 UI 线程 |
| `ICollectionView.GroupDescriptions` | `CollectionViewSource.IsSourceGrouped` + `ItemsPath`,或 VM 分组集合 | 任务栏按进程/类别分组时实现 |
| `ShellLogger` | 自建同名日志接口 + 文件观察者 | 结构照抄,去掉 WPF 依赖 |
| `NativeMethods.*` | 原样复制 | 纯 Win32,无改动 |

## 5. 许可证

ManagedShell 为 Apache-2.0:允许复制、修改、再分发,但需保留版权声明并标明修改。
落地方式:

- 每个移植文件头部保留原始 Apache-2.0 版权头,并追加一行"Adapted for
  ExplorerRevolution (UWP port)"。
- 仓库根增加 `NOTICE` 或 `THIRD_PARTY_NOTICES.md`,注明来源
  `https://github.com/cairoshell/ManagedShell`(Apache-2.0)。

## 6. 模块映射:现状 → 移植来源

| 当前自研实现 | 移植/替换自 ManagedShell |
| --- | --- |
| `HookExplorer.HideExplorer / RestoreExplorer` | `ExplorerHelper`(ABM AutoHide + `SWP_HIDEWINDOW`,含二级任务栏);桌面图标用 `ShellHelper.ToggleDesktopIcons` |
| `TaskbarButtonMonitor`(WinEvent 枚举/增删) | `TasksService` 核心(SHELLHOOK + cloak 钩子 + 初始枚举 + `TaskbarButtonCreated`) |
| `WindowHelpers.ShouldShowInTaskbar / GetTaskbarWindows` | `ApplicationWindow.CanAddToTaskbar / ShowInTaskbar` 过滤逻辑 |
| `ForegroundMonitor`(未接入) | `TasksService` 的 HSHELL 事件(前台激活/桌面激活/闪烁) |
| `Helpers.IsUwpWindow + GetUwpAppIconAsync` | `ApplicationWindow.IsUWP` 判定 + `StoreAppHelper`(manifest 图标路径解析) |
| `GetWindowIcon / HIconToBitmapImageAsync` | `ApplicationWindow.Icon` 的获取策略 + 现有 PNG→BitmapImage 管线 |
| 任务栏按钮点击(未实现) | `ApplicationWindow.BringToFront / Minimize / Restore / Maximize / Close / Move / Size` |
| 任务栏数据模型 `Data.TaskBarIcon` | 自建 `WindowInfo` ViewModel(包装移植后的窗口服务 + `BitmapImage`) |
| 右侧托盘区(空壳) | `TrayService + NotificationArea`(TrayNotifyWnd 子类化、`TaskbarCreated` 重发) |
| 时钟/音量/网络/电池(未实现) | `ImmersiveShellHelper` flyout + `VolumeHelper / PowerHelper / SoundHelper` |
| 控制中心/通知中心(未实现) | `ImmersiveShellHelper.ShowControlCenter / ShowActionCenter`(Win11 22H2+ 差异验证) |
| 全屏应用检测(未实现) | `FullScreenHelper` + `AppVisibilityHelper` |
| 任务栏定位/工作区(自绘窗体) | `AppBarManager` 的 ABM 封装(宿主保持 WinForms Form) |
| 桌面右键菜单(雏形) | `ShellContextMenu / ShellFolderContextMenu` |
| 桌面图标(未来) | `ShellFolder.IsDesktop + Files` + `ShellFile` |
| 文件操作(未来) | `ShellFile` + `FileOperationWorker` + `ShellHelper.SendToRecycleBin` |
| 启动菜单/启动器(未来) | `StoreAppHelper.AppList` + `ShellLink` + `ShellHelper.ActivateApplication / ShowStartMenu / ShowRunDialog` |
| 日志(无) | `ShellLogger` 风格自建(文件/控制台观察者) |

## 7. 路线图

### Phase 0 — 参考基线(spike,0.5–1 周)

**目标**:锁定参考版本、产出移植清单,验证最关键的技术假设。

- [ ] 将 ManagedShell 0.0.346 源码快照放入 `reference/ManagedShell`(只读参考,
      不进编译;或记录固定 commit 以便随时拉取)
- [ ] 产出"可移植文件清单"与 §4 类型映射落地明细
- [ ] spike 1:**SHELLHOOK 消息窗口**在当前 WinForms + Mile.Xaml 宿主下注册并
      收到事件(移植 `TasksService` 的消息窗口 + `RegisterShellHookWindow` 部分)
- [ ] spike 2:`Windows.UI.Xaml.DispatcherTimer` 在宿主内正常触发(为托盘/全屏轮询铺路)
- [ ] 接入自建 `ShellLogger` 文件日志
- [ ] 建立构建/运行验证脚本(继续用 VS MSBuild,见 §8)

**验收**:spike 均通过;现有功能零回归;移植清单评审通过。

### Phase 1 — 任务服务移植(1–2 周)

**目标**:任务栏从自研 WinEvent 监控切换到移植后的 `TasksService`,补齐核心交互。

- [ ] 移植 `TasksService` 核心(SHELLHOOK、cloak 钩子、初始枚举、`TaskbarButtonCreated`),
      事件改为 .NET event,经 `CoreDispatcher` 编排到 UI
- [ ] 移植 `ApplicationWindow` 数据逻辑 → 自建 `WindowInfo` ViewModel
      (Title/Icon/BitmapImage/IsUWP/IsMinimized/CanMinimize/State…)
- [ ] **前台高亮**:前台/桌面激活/闪烁状态机,按钮三态 + 底部指示条动画
      (激活 16px 实色、非激活 8px、隐藏 4px 透明)
- [ ] **点击交互**:左键 `BringToFront`(已激活则 `Minimize`),`Restore`;
      右键菜单(关闭、最大化/还原,预留)
- [ ] **UWP 图标**:移植 `StoreAppHelper`(manifest 图标路径)替换现有不稳定的
      `GetUwpAppIconAsync`;删除 `IsUwpWindow` 重复实现
- [ ] 拖动重排保持(GridView `CanReorderItems`),顺序持久化
- [ ] 标题/最小化/进度状态通过 `PropertyChanged` 实时刷新

**验收**:与原生任务栏对齐 ≥90%(打开/关闭/切换/最小化/还原/标题/图标);
UWP 图标正确;前台高亮与指示条可用。

### Phase 2 — 托盘与系统状态区(1–2 周)

- [x] 移植 `TrayService` + `NotificationArea`(Shell_TrayWnd/TrayNotifyWnd 假窗口、
      图标增删/悬浮/点击/双击、`TaskbarCreated` 重发、explorer 初始枚举),
      UI 侧以自绘图标区呈现(不再裁剪/嫁接 explorer 原托盘)
- [x] 时钟:实时走时(自研,不依赖 explorer 渲染)
- [x] 输入法指示器:按前台线程键盘布局 + `ImmGetConversionStatus` 显示 中/英
- [ ] 时钟/输入法点击弹出系统 flyout(`ImmersiveShellHelper.ShowClockFlyout` 等)
- [ ] 音量:`VolumeHelper` + `ShowSoundFlyout`
- [ ] 网络/电池:`PowerHelper` 状态图标 + `ShowNetworkFlyout / ShowBatteryFlyout`
- [ ] 控制中心/通知中心按钮(`ShowControlCenter / ShowActionCenter`,Win11 22H2+ 验证)
- [x] 托盘常驻/溢出分组:任务栏只显示固定(pinned)图标,其余收进 chevron 溢出
      Flyout;hover 固定/取消固定,固定列表持久化到注册表
- [x] 溢出面板改为 **UWP Flyout 独立窗口**(`TrayOverflowFlyoutPage` + 独立 STA 线程
      `TrayOverflowFlyoutWindow`):真正的 UWP XAML 卡片渲染(圆角/箭头/hover 图钉),
      独立线程承载第二个 XAML Island,避开"同线程多 Island 破坏任务栏 acrylic"
      (microsoft-ui-xaml #3482);初始化失败自动回退旧 WinForms 浮窗
- [x] 托盘鼠标协议修复:NOTIFYICON_VERSION_4 下回调 wParam 发送真实屏幕坐标
      (低字 X、高字 Y),lParam 为 `消息 | UID<<16`;v3 保持 wParam=UID。
      修复部分程序右键菜单跑到屏幕左上角的问题(此前误把 MK_* 常量当坐标)

**验收**:托盘图标出现且交互正常(点击/右键/双击/悬浮);时钟走时;输入法中英正确;
各 flyout 可打开;右键菜单出现在鼠标位置;与 explorer 原托盘不冲突。

### Phase 3 — AppBar、全屏与多显示器(1–2 周)

- [ ] 移植 `AppBarManager` 的 ABM 封装(`SHAppBarMessage`、工作区保留、
      自动隐藏、边缘枚举),宿主保持 WinForms Form
- [ ] 全屏检测:`FullScreenHelper` + `AppVisibilityHelper`(视频/游戏自动隐藏任务栏)
- [ ] 多显示器/DPI:`ScreenHelper / DpiHelper` 移植;Win11 DPI 行为处理
      (参考 `EnableWin11DpiWorkaround` 的思路,宿主是 WinForms 时重点验证)
- [ ] 任务栏高度/边距按 DPI 缩放系统化(当前写死 `48 * dpiScale`)

**验收**:全屏应用自动隐藏;DPI 切换(125%/150%)无错位;多显示器任务栏正确。

### Phase 4 — 桌面与文件操作(1–2 周)

- [ ] 移植 `ShellFolder`(桌面)+ `Files` 集合 + `ShellFile`(图标/名称/重命名)
- [ ] 移植 `ShellFolderContextMenu`(查看/排序/新建/刷新/属性)
- [ ] 文件操作:`ShellHelper.SendToRecycleBin`、`FileOperationWorker` 复制/移动
- [ ] 拖放支持

**验收**:桌面图标显示/重命名/删除与原生一致;右键菜单可用。

### Phase 5 — 启动菜单/启动器(1–2 周)

- [ ] 应用列表:移植 `StoreAppHelper.AppList`(UWP)+ `ShellLink`(开始菜单快捷方式)
- [ ] 启动:Win32 `ShellHelper.StartProcess`;UWP `ShellHelper.ActivateApplication(aumid)`
- [ ] 固定/取消固定(持久化)
- [ ] 搜索(`SearchHelper` 移植)
- [ ] 电源按钮(关机/重启/锁屏/注销:`PowerHelper` + `ShellHelper.Lock/Logoff`)

**验收**:能列出并启动全部应用(含 UWP);固定项重启后保留。

### Phase 6 — 产品化与稳定性(持续)

- [ ] 运行模式:并存模式(现状)与替换模式(`IsAppRunningAsShell` 等价判断);
      崩溃/退出时恢复原生壳
- [ ] 设置持久化与深浅色主题跟随(WinUI SunValley 资源已有)
- [ ] 全局异常兜底 + 日志 + 崩溃自动重启
- [ ] 安装/自启动
- [ ] 性能:图标缓存、集合更新集中到 UI 线程
- [ ] 测试矩阵:Win10/Win11、多显示器、DPI、UWP/全屏/游戏、explorer 重启恢复

## 8. 风险清单

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 移植代码需自行维护,ManagedShell 上游更新不同步 | 中 | 锁定参考版本;移植文件保留原始文件名与注释,便于 diff 同步 |
| UWP `ICollectionView` 无 Filter/GroupDescriptions | 中 | 分组用 `IsSourceGrouped`+`ItemsPath` 或 VM 分组集合(§4 适配点) |
| SHELLHOOK/托盘与 explorer 并存冲突(`SetTaskmanWindow` 抢占) | 高 | 按"是否替换 explorer"区分模式;优先验证替换模式 |
| Win11 行为差异(22H2 flyout、DPI) | 中 | 版本判断 + 测试矩阵覆盖 |
| `dotnet build` 无法编译 UWP XAML 工程 | 中 | 固定 VS MSBuild 命令;CI 用相同工具链 |
| Apache-2.0 合规 | 低 | 移植文件保留许可头 + `THIRD_PARTY_NOTICES.md` |

## 9. 建议执行顺序

1. **Phase 0 先行**:两个 spike(SHELLHOOK 消息窗口、DispatcherTimer)决定后续所有
   写法的可行性,1 周内完成。
2. **再打 Phase 1**:直接解决当前两个最痛点(交互缺失、UWP 图标),收益最高。
3. Phase 2(托盘)与 Phase 4/5(桌面、启动器)相互独立,可并行推进;
   Phase 3 的 ABM 封装建议在 Phase 1 稳定后做。
4. Phase 6 产品化穿插进行,从 Phase 1 起就保留日志与异常兜底。
