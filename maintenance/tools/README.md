# 维护工具

维护命令已内置到 `PGame-TSManager.exe`，不再提供 `.ps1/.bat/.cmd/.sh`。

通用包按四目录结构工作：

```text
Core/       TShock 运行时和依赖
Servers/    每服配置和世界
Plugins/    插件总库
Tools/      维护说明与模板
```

## 常用命令

```powershell
PGame-TSManager.exe --tool script-audit --root .
PGame-TSManager.exe --tool config-check "Servers\Profiles\1.生存" --json
PGame-TSManager.exe --tool new-server --root . --name "生存3" --port 7779 --rest-port 7880 --template "1.生存"
PGame-TSManager.exe --tool add-templates --root .
PGame-TSManager.exe --tool package --root .
PGame-TSManager.exe --tool migrate-layout --root . --apply
PGame-TSManager.exe --tool update-tshock --manager . --package "<TShock.zip>" --apply
PGame-TSManager.exe --tool prune-releases --repo "CNS-Team/PGame-TSManager" --token "<token>" --keep 10
PGame-TSManager.exe --tool housekeeping
```

## TShock 模板

`--tool add-templates` 会创建两套通用模板：

| 模板 | 游戏端口 | REST 端口 |
| --- | ---: | ---: |
| `1.生存` | 7777 | 7878 |
| `2.生存2` | 7778 | 7879 |

两套模板默认必需启用 `TShockAPI.dll` 和 `HotReload.dll`。

## 新建服务器

`--tool new-server` 会按当前四目录结构创建新服务器目录：

- 自动计算 `Servers\Profiles\序号.名字`
- 复制模板服的 `tshock\` 配置
- 写入该服的 PGame-TSManager `config.json`
- 提示世界、REST 和数据库需要修改的位置

```powershell
PGame-TSManager.exe --tool new-server --root "D:\PGame-TSManager" --name "生存3" --port 7779 --rest-port 7880
```

## 配置校验

`--tool config-check` 只读检查：

1. `tshock\config.json` 使用中文键
2. REST 令牌字段使用「用户名 / 用户组」
3. `sscconfig.json` 保持英文

```powershell
PGame-TSManager.exe --tool config-check "D:\PGame-TSManager\Servers\Profiles\1.生存"
```

退出码：`0` = 通过，`1` = 有失败项。

## 内置命令

| 命令 | 用途 |
| --- | --- |
| `--tool script-audit` | 扫描并拒绝脚本文件 |
| `--tool package` | 构建三套包并审计产物 |
| `--tool add-templates` | 写入 `生存`、`生存2` 两套模板 |
| `--tool new-server` | 新建受管服务器 |
| `--tool config-check` | 校验 TShock 中文键、REST 字段和 SSC 英文 |
| `--tool migrate-layout` | 迁移旧目录到 `Core / Servers / Plugins / Tools` |
| `--tool update-tshock` | 预览或替换 TShock 运行时 |
| `--tool prune-releases` | 清理旧 GitHub Release 与 tag |
| `--tool housekeeping` | 清理管理器自身备份与过期日志 |

GitHub Actions 的 Artifacts / Caches / Release / Workflow run 清理由
`maintenance/PruneTool`（C#）执行，工作流不再内嵌 PowerShell。

## 重要规则

- 不修改 `/help`
- SSC 保持英文
- REST 令牌字段固定为「用户名 / 用户组」
- 私人服务器配置、RPG 插件、世界和数据库不进入通用包
- 任何 `.ps1/.bat/.cmd/.sh` 都不得进入仓库或发布包
