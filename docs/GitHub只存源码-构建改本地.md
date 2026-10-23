# 策略：GitHub 只存上游源码，构建改本地（2026-10-11，用户决定）

## 决定

```text
本仓库（PGame-TSManager / PGame-TSManager-Personal）**不再在 GitHub 上构建**：
  · 只保存上游最新源代码（Git 仓库本身）；
  · 不再产生 Artifacts / Release 资产；历史构建产物全部清空；
  · 构建一律在本地做，产物直接部署到远程服务器使用（"本地构建远程使用"）。
```

## 为什么

1. GitHub 组织 Free 计划的 **Actions 存储配额 0.5GB**（Artifacts + Caches + Release 资产共用），
   而本仓库单次构建产出的 artifact 约 270MB、默认保留 90 天 —— 实测累积到 **98 个 ≈ 24GB**；
   Release 侧另有 10 个 × 182MB ≈ 1.8GB（`-Keep 10`）。**纯属浪费**。
2. 构建产物**本来就只在本地/远程使用**：远程是测试源与配置更新源，本地产物直接部署，
   GitHub 上的包基本没人下载 —— CI 的价值只有"验证能编译"，而本地一条命令同样能做到（23 秒）。
3. 让仓库回归"上游代码存档"这个唯一职责，仓库体积与权限面都更小。

2026-10-23 起通用源码仓库迁至 `CNS-Team/PGame-TSManager`，本策略继续有效。

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
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root .
#   --skip-runtime  已有 Core 运行时 / 离线时跳过下载
#   --out D:\out    换输出目录

# 个人库（Core + 个人 overlay → 个人版包）
dotnet run --project PGame-TSManager/PGame-TSManager.csproj -c Release -- --tool package --root "<PGame-TSManager 仓库路径>" --out "<个人产物目录>"
```

主库命令**等价于原 CI 的打包流程**（`dotnet publish` 自包含单文件 → 注入官方 TShock 运行时 →
生成 `universal` / `template` / `source` 三套包 → 塞入使用说明与更新说明 → 仓库布局自检），
2026-10-11 本地实测：**23 秒**跑完，产出 `source 0.7MB` / `template 90.7MB` / `universal 90.7MB`。

## 恢复方式（万一以后要重开 CI）

流水线定义仍在 Git 历史里（删除它们的提交之前），`git show <commit>^:.github/workflows/build.yml` 可取回；
Release 清理已内置为 `--tool prune-releases`。

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
   这段期间要个人版产物，直接用本地 `--tool package`（0 成本、不经过 GitHub Actions）。

## 当前状态与约定（2026-10-11 23:3x 定，双方已对齐）

```text
① 私有库 CI 一律【不要触发】—— 在用户处理完 Billing（付款方式 / spending limit / 或转 public）之前
   任何 run 都是 3-4 秒 failure 的失败记录，只会刷垃圾；公开库（PGame-TSManager）不受影响，prune.yml 照常可用。
② 个人版产物的主路径 = 本地打包（Billing 未修好期间的唯一路径）：
     PGame-TSManager.exe --tool package --root . --skip-runtime
   11-Bot 实测：9.5 秒 → artifacts\PGame-TSManager-personal.zip 82.8 MB / 336 条目；
   TSM 侧核对同一文件：86,811,188 B（≈82.8 MB）。这个体积/条目数即日后 CI 恢复时的验收基线（产物 ≤82MB、artifact 数 ≤1）。
③ -SkipSync 的语义：不重新 clone 公共 Core，用本地 core 克隆 + D:\59934\Desktop\PGame-TSManager 同步 overlay
   ⇒ **离线可用**（不需要 Clash）；只有需要真正 `Sync-Core.ps1` 从 github.com 拉取时，本机才必须挂代理 7890。
④ 个人库 .gitignore 已含 artifacts/ / core/ / *.zip ⇒ 构建后工作区保持干净（TSM 侧已复核）。
```

## 用户定案（2026-10-11 23:5x，11-Bot 登记）：个人版走**本地构建**

```text
1) 个人版产物唯一渠道 = 本地 `PGame-TSManager.exe --tool package`
   （11-Bot 实测：9.5 秒 → 82.8MB / 336 条目 / 解压 219.2MB；
     解压后布局 Core(27) / Servers(269) / Plugins(32) / Tools(5) + exe + config + personal-overlay.json，解压即用）
2) PGame-TSManager-Personal/.github/workflows/build-personal.yml 保留但不触发
   （Billing 修好前不刷失败记录；等用户处理完 Billing 再由 11-Bot 触发一次验证：
     产物 ≤82MB、artifact 数 ≤1）
3) 私有库（-Personal / PGameAPI / PigeonRPG）继续维持零产物 / 零 Release
   —— 这是 billing 拦截的根因（Free 计划 500MB 私有存储），不要再堆积
4) public 的 PGame-TSManager 的 prune.yml 照旧（公开库免费、一直正常）
```

**TSM 侧承诺**：不再对任何私有库 `workflow_dispatch` / 手动触发；除非必要不改私有库里的 workflow 文件
（`prune.yml` 的 push 自举只在本文件被改时才会跑一次）。

**关于个人库 `prune.yml` 的每日 `schedule`（UTC18:30 = 北京 02:30）**：保留。
理由：它现在是"预期失败"（Billing 拦截，3-4 秒），**不占资源、不算我方配置问题**，而且 Billing 一修好当天就自动恢复清理；
相比之下先 disable 再记得重新启用，漏掉反而可能让产物重新堆积。若今后要"零噪音"，只需把 `schedule:` 段注释掉（保留 push + 手动）。
