# TSMBridge —— TSM ⇄ TShock 桥接插件（Harmony）

把 TShock **进程内**的事件推给 TSM 的 Control API，让 TSM 从"靠端口/日志猜"变成"知道"。

## 一、安全纪律（写死在代码注释里，改动前必读）

```text
✅ Harmony 只挂 TShockAPI / HotReload 自己的方法（它们没有被别人改写）
❌ 绝不挂 Terraria/OTAPI 的方法 —— 实测 OTAPI.dll 里有 2,722 个 mfwh_ 包装，
   ModFramework 已经改写了几乎整个服务端；再叠 Harmony 就是"第二个改写者"，
   正是 WorldGen.nextCount ↔ mfwh_nextCount 自递归事故的成因
✅ 每个 patch 单独 try/catch：失败只降级 + 打日志，绝不让插件加载失败
✅ Dispose 时 UnpatchAll —— 热重载不留孤儿 hook
✅ 上报走后台线程 + 队列：绝不占用服务器 tick
```

## 二、上报什么（当前实现）

| 事件 | 来源 | 说明 |
|---|---|---|
| `bridge.hello` | 插件启动 | 版本 / serverId / TSM 地址 |
| `player.joined` / `player.left` | TShock `PlayerHooks`（官方事件，非 Harmony） | 名字 / 槽位 / 当前人数 |
| `world.saved` | Harmony→`TShockAPI.Log.ConsoleInfo` | 保存原文 + 世界文件大小（本版 TShock 无公开保存事件，编译器已验证） |
| `plugin.loaded` | 同上，匹配「已加载 X」 | 热重载/加载了哪个插件 |
| `server.tick` | `ServerApi.Hooks.GameUpdate`（官方事件） | tick 间隔均值/最大值/样本数/人数（只存不刷屏） |
| `server.stall` | 同上 | 单 tick 间隔 >250ms（≈掉到 4fps 以下）时立即上报 |
| `server.errors` | Harmony→`TShockAPI.Log.ConsoleError` | 每窗口错误条数 + 首条样本 |
| `command.slow` / `command.slow.count` | Harmony→`TShockAPI.Commands.HandleCommand` | 超过阈值的指令耗时（默认 50ms）+ 计数 |
| `server.reload` | TShock `GeneralHooks.ReloadEvent` | /reload 类操作 |

## 三、配置（`tshock\TSMBridge.json`，中文键）

```json
{
  "启用": true,
  "TSM地址": "http://127.0.0.1:8765",
  "服务器标识": "0",
  "控制API令牌": "<TSM config.json 的 控制API令牌>",
  "上报秒": 5,
  "慢指令毫秒": 50,
  "tick异常毫秒": 80
}
```

## 四、部署（TSM 侧 3 个文件）

```text
Plugins\TSMBridge.dll        ← 插件本体（每服清单里勾上才启用）
Core\bin\Lib.Harmony.dll     ← 依赖（放 bin\，所有沙箱经联接共享；别放 ServerPlugins 免得被当插件加载）
Servers\Profiles\<服>\tshock\TSMBridge.json   ← 上面的配置（serverId + 令牌）
```

## 五、验证

```powershell
# 1) 服务器日志应出现（启动后）
#    [TSMBridge] v0.1.0 已启动（serverId=0 → http://127.0.0.1:8765）
#    [TSMBridge] patch √ TShockAPI.Commands.HandleCommand
# 2) TSM 侧状态
curl -H "X-TSM-Token: <token>" http://127.0.0.1:8765/tsm/bridge
# 3) 事件流（SSE）里出现 player.joined / world.saved 等
```

## 六、回滚

删掉 `Plugins\TSMBridge.dll`（或从该服清单里去掉）+ `syncplugins` + 重启该服即可；`Lib.Harmony.dll` 留着无害。
