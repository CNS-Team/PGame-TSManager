# UI 技术栈决策：保持 WPF，不迁移 Avalonia（2026-10-11）

## 结论

**不迁移**。UI 保持 **WPF（.NET 9 / `net9.0-windows` / `UseWPF` + `UseWindowsForms`）**，继续在现有界面上迭代美化与功能。

后续会话**不要**再提议重建 UI 栈；若确要重开这个话题，先读完本文的评估数据再谈。

## 评估过的方案

候选：**Avalonia UI**（<https://github.com/AvaloniaUI/Avalonia>，MIT）。

技术上可行 —— 窗口、托盘、自定义标题栏、富文本、半透明、单文件自包含发布都有对应能力；
但**不是"换主题"，而是整套 UI 层重写**。

## 当时的量化盘点（仓库实测）

| 类别 | 数量 |
|---|---|
| XAML 界面 | 15 个文件 / **1,755 行**（MainWindow 497 / ServerSettingsWindow 285 / AppStyles 159 …） |
| 与 WPF 强耦合的 C# | 16 个文件 / **5,168 行**（ServerContainer 1876 / ServerSettingsWindow 675 / MainWindow 651 / ControlApiServer 525 / App 432 …） |
| `System.Windows.` 引用点 | **1,274 处**（Brush 148 / MessageBox 70 / WindowChrome 53 / Dispatcher 32 / `new Run()` 28 …） |
| 可平移的核心逻辑 | 约 **1,900 行**（ManagerConfig / PluginSync / SelfCheck / Alerter / Cli / ControlEventHub / ExternalProcessRuntime / ScheduleService / IdleMemoryTrim） |
| 需要改写的 UI 总量 | **≈ 6,900 行** |

## 五个"硬点"（迁移时必须重做）

| 硬点 | 现状（WPF） | Avalonia 对应 |
|---|---|---|
| 控制台彩色渲染 | `FlowDocument` + `Paragraph.Inlines.Add(new Run{Foreground})`（ServerContainer 1878–1908，28 处 `Run`） | 无 FlowDocument；需 `TextBlock.Inlines` / 自绘 / 虚拟化列表，长日志性能要实测 |
| 托盘 | `System.Windows.Forms.NotifyIcon` + `ContextMenuStrip`（App 155–222、MainWindow 321–341） | 内置 `TrayIcon` + `NativeMenu`（Windows/macOS 可用，Linux 受限） |
| 暗色标题栏 + 圆角 | `WindowChromeHelper`（118 行，DWM P/Invoke） | `ExtendClientAreaToDecorationsHint` + 仍需 Win32 P/Invoke |
| 液态玻璃 | WPF 半透明画刷 + 渐变（不依赖系统模糊，RDP 不掉效果） | 可自绘；`ExperimentalAcrylicBorder` 在 Windows 需另开 |
| `MessageBox` ×70 / `Dispatcher` ×32 | 内置 | **无内置 MessageBox**（自写或第三方）；`Dispatcher.UIThread` |

## 决定"不划算"的三个实测约束

1. **本机无 GPU**：只有 `Microsoft Remote Display Adapter` + `Microsoft Basic Display Adapter`，全程 RDP（会话 2）→ 现在就是软件渲染；
   Avalonia(Skia) 在无 GPU/RDP 下**可能**更稳更清晰 —— 这是唯一潜在真实收益，但属"未验证的收益"。
2. **内存不会更省**：TSM 现在 **工作集 100 MB / 私有 66 MB / 32 线程**；Avalonia 同量级，且 Skia 软渲染在 RDP 下可能更吃 CPU。
3. **程序天生 Windows-only**：WMI 扫进程、拉起 TShock 子进程、托盘、单实例互斥都绑 Windows。
   Avalonia 的跨平台只在「**瘦客户端**（连远程 Control API 管服）」场景才用得上 —— 而该 API 我们**已经有了**（`tsm.control.v1`，`127.0.0.1:8765`），
   真要做 Linux/mac 客户端，**不需要动现有 Windows 管理器**，新写一个瘦客户端更省。

## 若将来重开这个话题（给未来会话的路线）

1. **先做 spike，不要直接整体迁移**：新建 `PGame-TSManager.Avalonia`，只实现「服务器列表 + 控制台 + 启停」，复用 `ControlApiServer` / `ManagerConfig`，
   在远程 RDP 实测三个指标：渲染流畅度与字体清晰度、内存、单文件自包含体积；
2. 预算参考：spike 约 1~2 天（80% 时间在控制台 Inline 渲染 + 托盘/标题栏）；整体迁移约 5~8 个工作日（6,900 行 UI + 5 个硬点 + 打包/CI 改造 + 全量回归）；
3. 迁移期间**另开分支**，线上运维（窗口/热更/重建）继续走 WPF 主线，两条线不要混。
