# 策略：GitHub 只存上游源码，构建改本地（2026-10-11，用户决定）

## 决定

```text
本仓库（PGame-TSManager / PGame-TSManager-Personal）**不再在 GitHub 上构建**：
  · 只保存上游最新源代码（Git 仓库本身）；
  · 不再产生 Artifacts / Release 资产；历史构建产物全部清空；
  · 构建一律在本地做，产物直接部署到远程服务器使用（"本地构建远程使用"）。
```

## 为什么

1. Pigeon-X 组织 Free 计划 **Actions 存储配额 0.5GB**（Artifacts + Caches + Release 资产共用），
   而本仓库单次构建产出的 artifact 约 270MB、默认保留 90 天 —— 实测累积到 **98 个 ≈ 24GB**；
   Release 侧另有 10 个 × 182MB ≈ 1.8GB（`-Keep 10`）。**纯属浪费**。
2. 构建产物**本来就只在本地/远程使用**：远程是测试源与配置更新源，本地产物直接部署，
   GitHub 上的包基本没人下载 —— CI 的价值只有"验证能编译"，而本地一条命令同样能做到（23 秒）。
3. 让仓库回归"上游代码存档"这个唯一职责，仓库体积与权限面都更小。

## 具体改了什么

| 对象 | 处理 |
|---|---|
| `.github/workflows/build.yml`（主库） | **删除**（含 `release` / `publish-build` 两个发版 job 与 `Prune-GitHubReleases.ps1 -Keep` 调用） |
| `.github/workflows/build-personal.yml`（个人库） | **删除** |
| 历史 Artifacts | **清空**（prune.yml 参数改为 `artifact_days=0`） |
| 历史 Releases + 资产 | **清空**（`release_keep=0`；注意：**只删 Release 与资产，不动 git tag**） |
| `.github/workflows/prune.yml`（两库保留） | 保留并改默认值为「全部清空」：`artifact_days=0 / cache_days=0 / release_keep=0 / run_days=0 / dry_run=false`；仍带 **push 自举触发** + 每日 UTC18:30 |
| README（两库） | 顶部徽章/下载链接改为"仅本地构建"；「构建」章节改成一条命令的本地流程；「自动构建」章节改写为本地构建说明 |

## 本地构建怎么做

```powershell
# 主库（Core + 通用版三套包）
./maintenance/tools/Build-Local.ps1
#   -SkipTShockRuntime  已有 Core 运行时 / 离线时跳过下载
#   -OutputRoot D:\out  换输出目录

# 个人库（Core + 个人 overlay → 个人版包）
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Local.ps1 `
  -CorePath "<PGame-TSManager 仓库路径>" -SourceRoot "D:\59934\Desktop\PGame-TSManager"
```

主库脚本**等价于原 CI 的打包流程**（`dotnet publish` 自包含单文件 → 注入官方 TShock 运行时 →
生成 `universal` / `template` / `source` 三套包 → 塞入使用说明与更新说明 → 仓库布局自检），
2026-10-11 本地实测：**23 秒**跑完，产出 `source 0.7MB` / `template 90.7MB` / `universal 90.7MB`，
布局自检 `passed: 248 tracked files`。

## 恢复方式（万一以后要重开 CI）

流水线定义仍在 Git 历史里（删除它们的提交之前），`git show <commit>^:.github/workflows/build.yml` 可取回；
`maintenance/tools/Prune-GitHubReleases.ps1` 也保留未删。

## 对分发的影响（需要维护方决定的一件事）

删除 Releases 后，**README 里原来的"下载构建"入口不再可用**（已改为"本地构建 / 维护方分发的包"）。
如果将来要让外部使用者拿到通用版，需要一个新渠道 —— 可选：
① 手工建一个 Release 挂最新包（prune 默认会删，需要把 `release_keep` 临时改回 1+）；
② 放到别处（网盘/站点）；
③ 继续维持"只在本地构建、不公开发布"。

## ⚠ 附：Actions 计费拦截的边界（2026-10-11 实测更正）

11-Bot 侧的诊断是「**全账号** Actions 被 Billing 拦住，所有 run 3-4 秒失败」。TSM 侧实测**只对私有仓库成立**：

| 仓库 | 可见性 | 当日 run 结果 |
|---|---|---|
| `PGame-TSManager`（本仓库） | **public** | `Prune #1` **54s success**、`#2` **10s success**、`#3` **15s success**；`Build #128/#129/#130/#132` 全 success（120~125s） |
| `PGame-TSManager-Personal` | private | 验证不到（匿名不可读）；按 11-Bot 实测 = 被 billing 拦（3-4 秒失败） |
| `PGameAPI` / `PigeonRPG` 等 | private | 被拦（11-Bot 实测） |

⇒ 结论：

1. **公开仓库的 Actions 不受该 billing 拦截影响**（本仓库已无构建，但 `prune.yml` 的每日/自举清理**仍然可用**）；
2. 旁证：本仓库在 public 状态下曾堆到 **23.5GB artifacts** 也没触发计费拦截 —— 拦截来自**私有**仓库那 500MB 私有存储；
3. 因此"**把私有仓库转 public**"是一条与"付款 / 调整 spending limit"并列的**真实可行的止血手段**（公开库 Actions 免费）；
4. 私有库（`-Personal` / `PGameAPI` / `PigeonRPG`）在用户处理完 Billing 前，**任何 run 都不会启动**；
   这段期间要个人版产物，直接用本地 `scripts\Build-Local.ps1`（23 秒、0 成本、不经过 GitHub Actions）。
