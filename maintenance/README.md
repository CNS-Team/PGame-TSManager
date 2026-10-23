# 维护说明

PGame-TSManager 运行端固定使用四目录结构：

```text
Core/       TShock 运行时、依赖、数据、日志、备份
Servers/    Profiles 配置和 Worlds 世界
Plugins/    统一插件总库
Tools/      维护工具和模板
```

## 维护目录

| 目录 | 职责 |
| --- | --- |
| `tools/` | 迁移、模板生成、配置校验和构建说明（执行入口已内置到管理器 exe） |
| `templates/` | 通用 TShock 配置模板 |
| `tshock-update/` | TShock 上游跟进流程 |
| `config-translation/` | TShock 中文键映射 |
| `rest-fixes/` | REST 用户名 / 用户组修复规则 |
| `ssc-lock/` | SSC 英文冻结规则 |
| `help-lock/` | `/help` 行为冻结规则 |
| `plugin-maintenance/` | 插件维护和依赖规则 |
| `PruneTool/` | GitHub Actions 存储清理（C#，无脚本） |

## 固定规则

1. `/help` 不修改、不禁用、不覆盖。
2. `sscconfig.json` 保持英文，不翻译、不改键、不改字段。
3. TShock `config.json` 使用中文键。
4. REST 令牌字段固定为「用户名 / 用户组」。
5. 世界、数据库、日志和玩家数据不进入 Git。

## 通用模板

通用包和模板包会自动写入：

| 模板 | 游戏端口 | REST 端口 |
| --- | ---: | ---: |
| `1.生存` | 7777 | 7878 |
| `2.生存2` | 7778 | 7879 |

## 构建顺序

1. 修改源码。
2. 本地构建和自检。
3. 提交 Git。
4. GitHub Actions 构建。
5. 在测试环境部署验证。
6. 同步桌面运行端并上报测试群。

## 常用命令

```powershell
dotnet build PGame-TSManager.sln -c Release

dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root .
```

仓库和发布包不包含 `.ps1/.bat/.cmd/.sh`；所有维护动作由管理器进程内 C# 执行。
