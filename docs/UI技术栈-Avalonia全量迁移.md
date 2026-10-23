# UI 技术栈：Avalonia 11.3 全量迁移（2026-10-23）

## 结论

PGame-TSManager 的 UI 已从 WPF 全量迁移到 **Avalonia UI 11.3.22**。

- 目标框架保持 `net9.0-windows`。
- `UseWPF=false`、`UseWindowsForms=false`。
- 不再保留 `.xaml` / `.xaml.cs`。
- 所有应用启动、主题、主窗口、运维中心、设置页和二级窗口集中在 `PGame-TSManager\Ui.cs`。
- 发布产物仍是 Windows 自包含单文件，不要求目标机器预装 Avalonia 或 .NET。

## 迁移口径

| 原 WPF 能力 | Avalonia 实现 |
| --- | --- |
| `Application` / `App.xaml` | `Avalonia.Application` + `UiTheme.Apply` |
| `MainWindow` 与全部二级窗口 | 代码构建的 `Window` / `UserControl` |
| `FlowDocument` 控制台 | `ListBox` + `TextBlock.Inlines` 分段着色 |
| `Dispatcher` | `Avalonia.Threading.Dispatcher.UIThread` |
| WinForms `NotifyIcon` | Avalonia `TrayIcon` + `NativeMenu` |
| WPF 样式与模板 | FluentTheme + 代码样式 + `FuncDataTemplate<T>` |
| WPF 文件选择器 | Avalonia `IStorageProvider` |
| 自定义标题栏 | `ExtendClientAreaToDecorationsHint` + 代码构建的标题栏、拖动/双击最大化 |

## 控制台颜色

`ServerContainer` 将每条日志解析为多个 `ConsoleRun`：

- 支持 ANSI 真彩色：`\x1b[38;2;R;G;Bm`、`\x1b[0m` 及常见 16 色。
- 支持 Terraria 标记：`[c/RRGGBB:文字]`。
- UI 用 `TextBlock.Inlines` 渲染每一段颜色，不再把整行压成单色。

## 目录约束

`PGame-TSManager\Ui.cs` 是唯一 UI 源文件。后续新增窗口、对话框、主题和控件辅助方法继续放入该文件，避免重新拆回 XAML 或大量 code-behind。

业务控制面、配置、插件同步、备份、看门狗和 Control API 仍保留在各自既有源文件中，不因 UI 迁移改变职责边界。
