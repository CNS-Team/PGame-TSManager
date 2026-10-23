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
  <a href="#快速开始">快速开始</a>
  ·
  <a href="#构建">本地构建</a>
  ·
  <a href="https://github.com/CNS-Team/PGame-TSManager/issues">问题反馈</a>
</p>

## 项目简介

PGame-TSManager 是一个用 Avalonia 编写的 Windows TShock 多服务器管理器，统一管理服务器进程、控制台、插件、配置、备份和运行状态。

**核心原则：**

- **只负责 TShock 控制面**：不处理 RPG、Bot、QQ 绑定、流光维度等业务逻辑。
- **纯 C# 实现**：运行、维护、建服、打包和更新均不依赖 `.ps1`、`.bat`、`.cmd` 或 `.sh` 脚本。
- **通用版与个人版分离**：本仓库只维护通用版；私人插件、服务器配置和业务内容放在个人版 overlay 中。

## 仓库说明

| 项目 | 说明 |
| --- | --- |
| 源码 | 本仓库只保存源码和文档，作为上游代码存放处 |
| 构建 | 仅在本机构建，不在 GitHub 构建或发布 |
| 产物 | 不提供 GitHub Releases；需要时在本地运行打包命令 |
| 清理 | `.github/workflows/prune.yml` 仅用于清理历史构建残留 |
| 数据 | 世界、数据库、日志、令牌和玩家数据不进入 Git |

## 当前通用版

通用包内置两套可直接使用的 TShock 模板：

| 模板 | 目录 | 游戏端口 | REST 端口 |
| --- | --- | ---: | ---: |
| 生存 | `Servers\Profiles\1.生存` | `7777` | `7878` |
| 生存2 | `Servers\Profiles\2.生存2` | `7778` | `7879` |

每个模板包含：

- 管理器服务器清单 `config.json`
- 中文 TShock 配置和 REST 配置
- SQLite 默认数据库配置
- 必需的 `TShockAPI.dll`、`HotReload.dll`
- 公共占位配置，不包含私人令牌、数据库密码或玩家数据

通用包不包含世界文件。把世界放入 `Servers\Worlds` 后即可启动。

## 功能

**服务器管理**

- 启动、停止、重启、顺序启动和多服务器切换
- 异常退出自动重启、连续失败停止、健康检查和异常告警
- 启动前备份、回滚中心和单实例唤醒

**控制台与指令**

- 查看实时控制台，支持 ANSI 真彩色和 Terraria 分段着色
- 向当前服务器、全部服务器或指定服务器发送指令
- 支持快速刷新和自动滚动到底部

**插件与配置**

- 统一插件总库，按每服清单同步和停用插件
- 可视化编辑 TShock、SSC、初始物品和服务器设置
- 插件依赖放入 `Core\bin`，不污染 `Plugins`

**内置维护**

- 配置检查、建服、模板补齐、目录迁移和 TShock 更新
- 自包含打包、Release 清理和脚本审计
- 所有维护功能由 `PGame-TSManager.exe --tool` 在进程内完成

## 快速开始

### 运行环境

- Windows 10 或 Windows 11
- 直接运行发布包时不需要预装 .NET
- 从源码构建时需要 .NET 9 SDK

### 使用步骤

1. 获取本地构建的通用包，或使用维护方分发的通用包。
2. 解压到目标目录，例如 `PGame-TSManager`。
3. 将世界文件放入 `Servers\Worlds`。
4. 按需修改 `Servers\Profiles\<模板>\tshock\config.json`。
5. 首次部署执行自检：

```powershell
PGame-TSManager.exe --selfcheck
```

6. 自检通过后双击 `PGame-TSManager.exe`，或在需要时顺序启动全部服务器：

```powershell
PGame-TSManager.exe --startall
```

自检结果写入 `selfcheck.txt`。

## 构建

在仓库根目录执行一条命令：

```powershell
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root .
```

该命令会：

1. 发布 Windows x64 自包含单文件管理器。
2. 下载官方 TShock 运行时并注入 `Core`。
3. 生成通用包、模板包和源码包。
4. 写入使用说明并执行脚本审计。

构建产物位于 `artifacts\`：

| 文件 | 用途 |
| --- | --- |
| `PGame-TSManager-universal.zip` | 管理器、维护命令和两套 TShock 模板 |
| `PGame-TSManager-template.zip` | 两套 TShock 服务器模板 |
| `PGame-TSManager-source.zip` | 源码与文档 |

常用参数：

```powershell
# 跳过 TShock 运行时下载
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root . --skip-runtime

# 指定输出目录
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root . --out D:\out
```

## 常用命令

| 命令 | 用途 |
| --- | --- |
| `PGame-TSManager.exe` | 打开管理器 |
| `PGame-TSManager.exe --startall` | 启动管理器并顺序启动全部服务器 |
| `PGame-TSManager.exe --syncplugins` | 同步插件，不启动服务器 |
| `PGame-TSManager.exe --selfcheck` | 执行无界面自检 |
| `PGame-TSManager.exe --send <服务器名> <指令>` | 通过 REST 发送服务器指令 |
| `PGame-TSManager.exe --nowindow` | 强制无界面运行 |
| `PGame-TSManager.exe --tool help` | 查看全部内置维护命令 |

维护命令统一使用以下形式：

```powershell
PGame-TSManager.exe --tool <command> [options]
```

支持 `script-audit`、`config-check`、`new-server`、`add-templates`、`package`、`migrate-layout`、`prune-releases`、`update-tshock` 和 `housekeeping`。执行结果同时输出到控制台和 `tool-result.txt`。

## 目录结构

```text
PGame-TSManager/
├─ PGame-TSManager.exe
├─ config.json
├─ README.md
├─ 使用说明.txt
├─ Core/                 # TShock 运行时、依赖、日志、备份和运行沙箱
├─ Servers/
│  ├─ Profiles/          # 每服配置和 TShock 配置
│  └─ Worlds/            # 世界文件
└─ Plugins/              # 插件总库
```

## 插件规则

- `Plugins` 是统一插件总库。
- 每台服务器加载哪些插件，由该服 `config.json` 的插件清单决定。
- 未列出的第三方插件会移入停用目录，不会直接删除。
- `TShockAPI.dll` 和 `HotReload.dll` 为必需插件。
- `TShockAPI.*` 由 TShock 自身保留，不参与插件同步。
- 插件依赖放入 `Core\bin`，不要混入 `Plugins`。

## 配置规则

- 管理器配置使用中文键。
- `sscconfig.json` 保持英文，不翻译、不改键、不改字段。
- REST 令牌字段使用“用户名 / 用户组”。
- `/help` 保持 TShock 原逻辑，不覆盖、不禁用。
- 世界、数据库、日志、令牌和玩家数据不提交到 Git。

## 通用版与个人版

| 内容 | 通用版 | 个人版 |
| --- | --- | --- |
| 管理器、UI、维护工具 | 有 | 有 |
| TShock 模板 | 两套通用模板 | 私人服务器配置 |
| RPG、Bot、维度等业务插件 | 无 | 有 |
| 私人令牌、端口、数据库配置 | 无 | 有 |
| 世界、数据库、玩家数据 | 不进入 Git | 不进入 Git |

架构边界详见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。TSM 只做 TShock 多进程控制面，业务层不进入公共 Core。

## 公开维护

本项目由 [CNS-Team](https://github.com/CNS-Team) 公开维护，维护负责人为 [Pigeon-X](https://github.com/Pigeon-X)。

- 源码仓库：[CNS-Team/PGame-TSManager](https://github.com/CNS-Team/PGame-TSManager)
- 问题反馈：[Issues](https://github.com/CNS-Team/PGame-TSManager/issues)
- 提交要求：说明版本、复现步骤、验证方法和脱敏日志
- 禁止提交：私人配置、令牌、世界文件、数据库和玩家数据

## 致谢

- 旧版多服管理器思路来自 [cc004/TSManager](https://github.com/cc004/TSManager)
- TShock 由 [Pryaxis/TShock](https://github.com/Pryaxis/TShock) 维护
- 感谢 [Pigeon-X](https://github.com/Pigeon-X) 发起并持续维护本项目
