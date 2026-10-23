# PGame-TSManager

<p align="center">
  <strong>Windows 平台 TShock 多服务器管理器</strong>
</p>

<p align="center">
  团队：流光工作室　·　作者：<a href="https://github.com/cc004">cc004</a>、<a href="https://github.com/Pigeon-X">Pigeon-X</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/build-local%20only-8A2BE2" alt="Local build only">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/TShock-6.2.x-4B8BBE" alt="TShock 6.2.x">
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4" alt=".NET 9">
  <img src="https://img.shields.io/badge/UI-Avalonia%2011.3-8A2BE2" alt="Avalonia 11.3">
</p>

<p align="center">
  <a href="#构建">本地构建说明</a>
  ·
  <a href="https://github.com/CNS-Team/PGame-TSManager/issues">问题反馈</a>
</p>

> [!IMPORTANT]
> **本仓库只保存源码，不在 GitHub 上构建。**
> 自 2026-10-11 起所有 GitHub Actions 构建流水线已移除：仓库仅作为**上游最新代码的存放处**，
> 构建产物（Artifacts / Release 资产）一并清空，以后也不会再产生。
> 需要可运行的包请在本地按下面「[构建](#构建)」一节自行打（一条命令），或使用维护方在本地构建后分发的包。
> 历史工件的清理规则见 `.github/workflows/prune.yml`（每日自动删净构建产物）。

> [!NOTE]
> 本仓库维护的是**通用版**：管理器、维护工具和两套 TShock 服务器模板。
> RPG、私人插件、个人服务器配置不会进入通用版，由独立个人版覆盖层维护。

## 当前通用版

通用包现在默认包含两套可直接被管理器识别的 TShock 模板：

| 模板 | 目录 | 游戏端口 | REST 端口 |
| --- | --- | ---: | ---: |
| 生存 | `Servers\Profiles\1.生存` | `7777` | `7878` |
| 生存2 | `Servers\Profiles\2.生存2` | `7778` | `7879` |

模板包含：

- PGame-TSManager 的 `config.json`
- 中文 TShock `tshock\config.json`
- SQLite 默认数据库配置
- REST API 默认配置
- 默认必需插件：`TShockAPI.dll`、`HotReload.dll`
- 打包命令会下载官方 TShock Windows x64 运行时并注入 `Core`（**本地构建**时执行，GitHub 上不再构建）
- 公共占位令牌，不包含私人令牌、MySQL 密码或玩家数据

通用包不包含世界文件。使用者只需要把世界文件放入 `Servers\Worlds`。

## 项目定位

PGame-TSManager 用一个窗口管理多个 TShock 服务端。它负责：

- 服务器启动、停止和顺序启动
- 控制台查看和服务器指令发送
- 插件总库与每服插件清单同步
- TShock config、SSC、初始物品可视化编辑
- 启动前备份、回滚中心、看门狗和异常告警
- 自检、维护与打包全部由管理器 exe 的 `--tool` 命令完成（流水线已迁出 GitHub）

它不替代 TShock 本身，也不会把世界、数据库、日志或玩家数据提交到 Git。

> [!IMPORTANT]
> 管理器运行期逻辑全部在 C# 系统底层完成，不调用 PowerShell、`.bat`、`.cmd` 或其它脚本协作完成功能。
> 维护、建服、配置校验、打包、迁移、TShock 更新和 Release 清理同样内置在 exe 中；仓库与发布包均不携带 `.ps1/.bat/.cmd/.sh`。

## UI 技术栈

界面已从 WPF 全量迁移到 **Avalonia UI 11.3**，运行时仍只面向 Windows（WMI、子进程、托盘和单实例互斥都依赖 Windows）。

- 所有应用启动、主题、主窗口、运维中心、设置页和对话框都集中在 `PGame-TSManager\Ui.cs`。
- 仓库不再包含 `.xaml` 或 `*.xaml.cs`；`UseWPF` 与 `UseWindowsForms` 均为 `false`。
- 控制台保留 ANSI 真彩色和 Terraria `[c/RRGGBB:文字]` 的分段着色。
- 发布方式仍是本地自包含单文件，不依赖目标机器预装 Avalonia 或 .NET。

## 干净管理包目录

运行端统一使用四个可见目录：

```text
PGame-TSManager/
├─ PGame-TSManager.exe
├─ config.json
├─ README.md
├─ 使用说明.txt
├─ Core/
│  ├─ TShock.Server.exe
│  ├─ GeoIP.dat
│  ├─ bin/
│  ├─ i18n/
│  ├─ Data/
│  ├─ Backups/
│  ├─ Logs/
│  └─ _runtime/
├─ Servers/
│  ├─ Profiles/
│  │  ├─ 1.生存/
│  │  │  ├─ config.json
│  │  │  └─ tshock/config.json
│  │  └─ 2.生存2/
│  │     ├─ config.json
│  │     └─ tshock/config.json
│  └─ Worlds/
└─ Plugins/
```

| 目录 | 用途 |
| --- | --- |
| `Core` | TShock 运行时、依赖、数据、日志、备份和运行沙箱 |
| `Servers` | 每服配置、TShock 配置和世界文件 |
| `Plugins` | 统一插件总库 |
| `Tools` | 不出现在干净管理包；维护命令已内置到 exe |

## 快速开始

1. 取一个通用包 —— 两种来源：① 本地构建（见「[构建](#构建)」，一条命令产出 `artifacts\PGame-TSManager-universal.zip`）；
   ② 维护方本地构建后分发的包。**本仓库不再提供 Releases 下载。**
2. 解压到目标目录。
3. 通用包已经准备好 `Core` TShock 运行时，不需要手动复制：

```text
Core/
├─ TShock.Server.exe
├─ GeoIP.dat
├─ bin/
├─ i18n/
├─ runtimes/
└─ x64/
```

4. 将世界文件放入 `Servers\Worlds\生存.wld` 和 `Servers\Worlds\生存2.wld`。
5. 按需修改两个模板里的 `tshock\config.json`。
6. 双击 `PGame-TSManager.exe`。
7. 首次部署执行：

```powershell
PGame-TSManager.exe --selfcheck
```

检查结果会写入 `selfcheck.txt`。

## 服务器清单

根目录 `config.json` 的 `serverProfiles` 负责服务器列表：

```json
{
  "name": "生存",
  "rootPath": "Servers\\Profiles\\1.生存",
  "executable": "TShock.Server.exe",
  "arguments": "",
  "enabled": true,
  "remark": "通用模板 7777 / REST 7878",
  "plugins": [],
  "pluginLibrary": ""
}
```

每服自己的 `config.json` 描述世界、端口、语言和插件清单：

```json
{
  "服务器名称": "生存",
  "启用": true,
  "世界": "生存.wld",
  "语言": 7,
  "端口": 7777,
  "REST端口": 7878,
  "最大玩家": 16,
  "IP": "0.0.0.0",
  "密码": "",
  "启动参数": "",
  "插件": [],
  "插件总库": "Plugins",
  "覆盖插件目录": true,
  "备注": "通用模板 7777 / REST 7878"
}
```

管理器会自动发现 `Servers\Profiles` 下带有 `config.json` 且填写了端口的服务器目录。

## 功能概览

| 功能 | 说明 |
| --- | --- |
| 多服务器管理 | 一个窗口切换不同 TShock 服务端 |
| 顺序启动 | 上一台真正就绪后再启动下一台，降低同时加载世界的压力 |
| 控制台 | TShock 输出、插件、警告、错误和成功状态分类着色 |
| 指令发送 | 支持当前服发送、全部服发送和 `--send` 命令 |
| 插件同步 | 按每服清单从 `Plugins` 同步，停用插件移入 `ServerPlugins.disabled` |
| 配置管理 | TShock、SSC、初始物品和更多设置分页编辑 |
| 备份与回滚 | 启动前备份，支持查看和恢复管理器更新备份 |
| 看门狗 | 异常退出自动重启，连续失败后停止并告警 |
| 内存压缩 | 空服时压缩工作集，不关闭端口、不中断首次进入 |
| 单实例 | 重复双击 exe 只唤醒已有管理器，不创建第二个窗口 |
| 自定义标题栏 | 左侧红黄绿三点、居中标题、右侧全屏/最小化/最大化/关闭；支持拖动、双击最大化和 `Esc` 退出全屏 |

## 常用命令

| 命令 | 用途 |
| --- | --- |
| `PGame-TSManager.exe --startall` | 启动管理器并顺序启动全部服务器 |
| `PGame-TSManager.exe --syncplugins` | 只同步插件，不启动服务器 |
| `PGame-TSManager.exe --selfcheck` | 无界面自检 |
| `PGame-TSManager.exe --send <服务器名> <指令>` | 通过 REST 发送服务器指令 |
| `PGame-TSManager.exe --nowindow` | 强制无界面运行 |

## 插件规则

- `Plugins` 是统一插件总库。
- 每台服务器加载哪些插件，由该服 `config.json` 的 `插件` 清单决定。
- 未列出的第三方插件会被移动到停用目录，不会直接删除。
- `TShockAPI.*` 由 TShock 自身保留，不参与插件同步和停用。
- `TShockAPI.dll` 和 `HotReload.dll` 默认必需，插件开关不能取消。
- 插件依赖 DLL 放入 `Core\bin`，不要混入 `Plugins`。

## TShock 维护规则

- TShock 更新通过 `Tools\maintenance\tshock-update` 独立处理。
- 管理器运行期不执行任何 PowerShell、bat 或 cmd 脚本。
- `config.json` 使用中文键。
- REST 令牌字段使用「用户名 / 用户组」。
- `sscconfig.json` 保持英文，不翻译、不改键、不改字段。
- `/help` 保持 TShock 原逻辑，不覆盖、不禁用。

## 构建

> **一条命令**（推荐）：需要 .NET 9 SDK。在仓库根目录执行：

```powershell
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root .
```

它会依次完成：`dotnet publish` 自包含单文件 → 下载官方 TShock 运行时注入 `Core` → 生成三套包
（`universal` / `template` / `source`）→ 把使用说明与更新说明塞进 zip → 对产物做脚本审计。
产物在 `artifacts\` 下；常用参数：

```powershell
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root . --skip-runtime
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root . --out D:\out
```

手工分步（与工具等价）：

```powershell
dotnet build PGame-TSManager.sln -c Release

dotnet publish PGame-TSManager/PGame-TSManager.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/manager
```

通用包和模板包由 `--tool package` 在 C# 内写入两套 TShock 模板。

## 发布包

| 包 | 用途 |
| --- | --- |
| `PGame-TSManager-universal.zip` | 管理器、内置维护命令和两套 TShock 模板 |
| `PGame-TSManager-template.zip` | 管理器模板和两套 TShock 模板 |
| `PGame-TSManager-source.zip` | 源码与文档 |

**GitHub 不再构建、也不再发布**（2026-10-11 起）：流水线 `build.yml` 已移除，
历史 Artifacts 与 Release 资产已清空，`.github/workflows/prune.yml` 每日兜底删净残留。
构建一律在本地（上面一条命令），产物由维护方直接部署到服务器使用。

## 通用版与个人版

| 内容 | 通用版 | 个人版 |
| --- | --- | --- |
| 管理器与 UI | 有 | 有 |
| 维护工具 | 有 | 有 |
| TShock 模板 | 有 | 有自己的三服配置 |
| RPG 插件 | 无 | 有 |
| PGameAPI/PChrome | 无 | 有 |
| 个人服务器配置 | 无 | 有 |
| 世界、数据库、玩家数据 | 不进入 Git | 不进入 Git |

边界规则与对接原则见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)：
TSM 只做「TShock 多进程控制面」，业务层（RPG / 维度 / Bot / 绑定）不进公共 Core。

## 公开维护与反馈

本项目由 [CNS-Team](https://github.com/CNS-Team) 组织公开维护，维护负责人为 [Pigeon-X](https://github.com/Pigeon-X)。
正式源码仓库为 [CNS-Team/PGame-TSManager](https://github.com/CNS-Team/PGame-TSManager)。
欢迎通过 [Issues](https://github.com/CNS-Team/PGame-TSManager/issues) 报告问题，或通过 Pull Request 提交通用功能与修复。
提交问题时请附管理器、TShock 版本与脱敏日志；提交代码时请说明验证方法，不提交私人配置、令牌、世界或玩家数据。
构建继续在本地执行，方式见上文「构建」。

## 致谢

- 旧版多服管理器思路来自 [cc004/TSManager](https://github.com/cc004/TSManager)。
- TShock 上游由 [Pryaxis/TShock](https://github.com/Pryaxis/TShock) 维护。
- 感谢 [Pigeon-X](https://github.com/Pigeon-X) 发起并持续维护本项目。
