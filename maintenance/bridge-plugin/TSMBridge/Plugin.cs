using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HarmonyLib;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace TSMBridge
{
    /// <summary>
    /// TSM ⇄ TShock 桥接：把 TShock 进程内的事件推给 TSM 的 Control API。
    ///
    /// 设计纪律（2026-10-12 定）：
    ///   · 能用 TShock 官方事件就用事件（玩家进出 / 世界保存 / 重载 / 每 tick）；
    ///   · Harmony **只**挂 TShockAPI / HotReload 自身的方法（它们没有被别人改写）；
    ///   · **绝不**挂 Terraria/OTAPI —— 那些方法已被 ModFramework 生成 2,722 个 mfwh_ 包装，
    ///     再叠 Harmony 就会变成"第二个改写者"，正是我们查过的 mfwh_ 自递归事故模式；
    ///   · 所有 patch 逐个 try/catch，失败只写日志，绝不让插件加载失败。
    ///
    /// 上报（POST JSON，全英文键，遵循两层口径）：
    ///   POST {TSM地址}/tsm/plugins/bridge/events
    ///   { "serverId": "0", "events": [ { "type": "...", "at": "...", "data": { ... } } ] }
    /// </summary>
    public class Plugin : TerrariaPlugin
    {
        public override string Name => "TSMBridge";
        public override Version Version => new Version(0, 1, 0);
        public override string Author => "Pigeon / TSM";
        public override string Description => "把 TShock 进程内事件（玩家进出/保存/tick/插件加载/慢指令/错误率）推给 PGame-TSManager";

        public Plugin(Main game) : base(game) { }

        // —— 配置（tshock\TSMBridge.json，中文键，与管理器配置口径一致）——
        private bool _enabled = true;
        private string _tsmBase = "http://127.0.0.1:8765";
        private string _serverId = "0";
        private string _token = "";
        private int _reportSeconds = 5;
        private int _slowCommandMs = 50;
        private int _tickAnomalyMs = 80;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        private static readonly ConcurrentQueue<string> Outbox = new ConcurrentQueue<string>();
        private static readonly Regex LoadedRx = new Regex(@"已加载\s+([^\s（(]+)", RegexOptions.Compiled);

        private Timer _flushTimer;

        // —— 统计窗口 ——
        private long _ticks;
        private double _tickMsSum, _tickMsMax;
        private long _tickGapMsSum, _tickGapMsMax;
        private long _lastTickStamp;
        private long _errorCount;
        private string _errorSample;
        private long _slowCommandCount;

        public override void Initialize()
        {
            LoadConfig();
            Instance = this;
            if (!_enabled)
            {
                TShock.Log.ConsoleInfo("[TSMBridge] 未启用（tshock\\TSMBridge.json 的 启用=false）");
                return;
            }

            // ① TShock 官方事件（零改写冲突）
            PlayerHooks.PlayerPostLogin += OnPostLogin;
            PlayerHooks.PlayerLogout += OnLogout;
            GeneralHooks.ReloadEvent += OnReload;
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            // ② Harmony：只挂 TShockAPI / HotReload 自己的方法（逐个容错）
            ApplyHarmonyPatches();

            // ③ 后台线程发送（绝不占用服务器线程）
            _flushTimer = new Timer(_ => Flush(), null, _reportSeconds * 1000, _reportSeconds * 1000);
            _ = System.Threading.Tasks.Task.Run(() => PumpLoop());

            Enqueue("bridge.hello", new Dictionary<string, object>
            {
                ["plugin"] = Name,
                ["version"] = Version.ToString(),
                ["serverId"] = _serverId,
                ["tsm"] = _tsmBase
            });
            TShock.Log.ConsoleInfo($"[TSMBridge] v{Version} 已启动（serverId={_serverId} → {_tsmBase}）");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { PlayerHooks.PlayerPostLogin -= OnPostLogin; } catch { }
                try { PlayerHooks.PlayerLogout -= OnLogout; } catch { }
                try { GeneralHooks.ReloadEvent -= OnReload; } catch { }
                try { ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate); } catch { }
                try { _flushTimer?.Dispose(); } catch { }
                // 热重载必须撤 patch，否则留下孤儿 hook（越热越坏）
                try { new Harmony("tsmbridge").UnpatchAll("tsmbridge"); } catch { }
                if (ReferenceEquals(Instance, this)) Instance = null;
            }
            base.Dispose(disposing);
        }

        // ————————————————————————— 配置 —————————————————————————

        private void LoadConfig()
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "tshock", "TSMBridge.json");
                if (!File.Exists(path)) path = Path.Combine("tshock", "TSMBridge.json");
                if (!File.Exists(path)) return;
                var text = File.ReadAllText(path, Encoding.UTF8);
                _enabled = ReadBool(text, "启用", true);
                _tsmBase = ReadStr(text, "TSM地址", _tsmBase);
                _serverId = ReadStr(text, "服务器标识", _serverId);
                _token = ReadStr(text, "控制API令牌", _token);
                _reportSeconds = Math.Max(2, ReadInt(text, "上报秒", _reportSeconds));
                _slowCommandMs = Math.Max(0, ReadInt(text, "慢指令毫秒", _slowCommandMs));
                _tickAnomalyMs = Math.Max(0, ReadInt(text, "tick异常毫秒", _tickAnomalyMs));
            }
            catch (Exception ex) { try { TShock.Log.ConsoleError("[TSMBridge] 读配置失败：" + ex.Message); } catch { } }
        }

        private static string ReadStr(string json, string key, string fallback)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : fallback;
        }
        private static bool ReadBool(string json, string key, bool fallback)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase) : fallback;
        }
        private static int ReadInt(string json, string key, int fallback)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
        }

        // ————————————————————————— TShock 事件 —————————————————————————

        private void OnPostLogin(PlayerPostLoginEventArgs args)
        {
            Enqueue("player.joined", new Dictionary<string, object>
            {
                ["name"] = args?.Player?.Name ?? "",
                ["index"] = args?.Player?.Index ?? -1,
                ["players"] = TShock.Players?.Count(p => p != null && p.Active) ?? 0
            });
        }

        private void OnLogout(PlayerLogoutEventArgs args)
        {
            Enqueue("player.left", new Dictionary<string, object>
            {
                ["name"] = args?.Player?.Name ?? "",
                ["index"] = args?.Player?.Index ?? -1,
                ["players"] = TShock.Players?.Count(p => p != null && p.Active) ?? 0
            });
        }

        private void OnReload(ReloadEventArgs args)
        {
            Enqueue("server.reload", new Dictionary<string, object> { ["players"] = TShock.Players?.Count(p => p != null && p.Active) ?? 0 });
        }

        private void OnGameUpdate(EventArgs args)
        {
            // 从 tick 间隔推断负载：间隔被拉长 = 服务器卡（不 hook Terraria，所以量的是间隔/抖动，不是时长）
            var now = Stopwatch.GetTimestamp();
            if (_lastTickStamp != 0)
            {
                var gapMs = (now - _lastTickStamp) * 1000.0 / Stopwatch.Frequency;
                _tickMsSum += gapMs;
                if (gapMs > _tickMsMax) _tickMsMax = gapMs;
                if (gapMs > 250 && _tickAnomalyMs > 0)
                {
                    // 单 tick 间隔 >250ms（≈掉到 4fps 以下）→ 立即上报一次卡顿
                    Enqueue("server.stall", new Dictionary<string, object> { ["gapMs"] = Math.Round(gapMs, 1) });
                }
            }
            _lastTickStamp = now;
            _ticks++;
        }

        // ————————————————————————— Harmony —————————————————————————

        private void ApplyHarmonyPatches()
        {
            var harmony = new Harmony("tsmbridge");

            // ① TShockAPI.Commands.HandleCommand —— 指令耗时（TShock 没有耗时事件）
            TryPatch(harmony, "TShockAPI.Commands", "HandleCommand",
                nameof(CommandPrefix), nameof(CommandPostfix));

            // ② TShockAPI.Log.ConsoleError —— 错误率（TSM 现在只能靠日志关键字猜）
            TryPatch(harmony, "TShockAPI.Log", "ConsoleError",
                null, nameof(ConsoleErrorPostfix));

            // ③ TShockAPI.Log.ConsoleInfo —— 插件加载（HotReload 会打「已加载 X」）
            TryPatch(harmony, "TShockAPI.Log", "ConsoleInfo",
                null, nameof(ConsoleInfoPostfix));
        }

        private void TryPatch(Harmony harmony, string typeName, string methodName, string prefix, string postfix)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) { LogPatchFail(typeName, methodName, "类型不存在"); return; }
                var method = AccessTools.Method(type, methodName);
                if (method == null) { LogPatchFail(typeName, methodName, "方法不存在"); return; }
                var hmPrefix = prefix == null ? null : new HarmonyMethod(typeof(Plugin).GetMethod(prefix, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
                var hmPostfix = postfix == null ? null : new HarmonyMethod(typeof(Plugin).GetMethod(postfix, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
                harmony.Patch(method, hmPrefix, hmPostfix);
                SafeLog($"[TSMBridge] patch √ {typeName}.{methodName}");
            }
            catch (Exception ex) { LogPatchFail(typeName, methodName, ex.Message); }
        }

        private void LogPatchFail(string t, string m, string why)
        {
            SafeLog($"[TSMBridge] patch × {t}.{m}（{why}）—— 该功能降级，其余照常");
        }

        private static void SafeLog(string line)
        {
            try { TShock.Log.ConsoleInfo(line); } catch { }
        }

        [HarmonyPrefix]
        private static void CommandPrefix(TSPlayer player, string cmd, out Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
        }

        [HarmonyPostfix]
        private static void CommandPostfix(TSPlayer player, string cmd, Stopwatch __state)
        {
            try
            {
                if (__state == null) return;
                __state.Stop();
                var ms = __state.Elapsed.TotalMilliseconds;
                var inst = Instance;
                if (inst == null) return;
                if (ms >= inst._slowCommandMs)
                {
                    Interlocked.Increment(ref inst._slowCommandCount);
                    inst.Enqueue("command.slow", new Dictionary<string, object>
                    {
                        ["player"] = player?.Name ?? "",
                        ["command"] = cmd ?? "",
                        ["ms"] = Math.Round(ms, 1)
                    });
                }
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void ConsoleErrorPostfix(string message)
        {
            try
            {
                var inst = Instance;
                if (inst == null) return;
                Interlocked.Increment(ref inst._errorCount);
                if (inst._errorSample == null && !string.IsNullOrWhiteSpace(message))
                    inst._errorSample = message.Length > 200 ? message.Substring(0, 200) : message;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void ConsoleInfoPostfix(string message)
        {
            try
            {
                var inst = Instance;
                if (inst == null || string.IsNullOrEmpty(message)) return;
                var m = LoadedRx.Match(message);
                if (m.Success)
                    inst.Enqueue("plugin.loaded", new Dictionary<string, object> { ["plugin"] = m.Groups[1].Value });
                // 世界保存：本 TShock 版本没有公开的保存事件（编译器已验证无 WorldHooks / GeneralHooks.WorldSave），
                // 改从 TShock 自己的保存日志识别 —— 与 TSM 现在的判定同源，但即时且带原文。
                if (message.Contains("世界已经保存到") || message.Contains("正在保存世界数据") ||
                    message.Contains("Saving world data"))
                {
                    var data = new Dictionary<string, object> { ["message"] = message };
                    try
                    {
                        if (!string.IsNullOrEmpty(Main.worldPathName) && File.Exists(Main.worldPathName))
                            data["size"] = new FileInfo(Main.worldPathName).Length;
                        data["world"] = Main.worldName ?? "";
                    }
                    catch { }
                    inst.Enqueue("world.saved", data);
                }
            }
            catch { }
        }

        private static Plugin Instance;

        // ————————————————————————— 上报 —————————————————————————

        private void Enqueue(string type, Dictionary<string, object> data)
        {
            try
            {
                var json = "{\"type\":" + Q(type) + ",\"at\":" + Q(DateTimeOffset.Now.ToString("o")) + ",\"data\":" + ToJson(data) + "}";
                Outbox.Enqueue(json);
            }
            catch { }
        }

        private void Flush()
        {
            try
            {
                // 统计窗口 → tick / 错误率
                var ticks = Interlocked.Exchange(ref _ticks, 0);
                if (ticks > 0)
                {
                    var avg = Math.Round(_tickMsSum / ticks, 2);
                    var max = Math.Round(_tickMsMax, 2);
                    var players = TShock.Players?.Count(p => p != null && p.Active) ?? 0;
                    // tick 只存不刷屏：TSM 侧按阈值决定是否推 SSE（这里始终上报数据）
                    Enqueue("server.tick", new Dictionary<string, object>
                    {
                        ["intervalAvgMs"] = avg,
                        ["intervalMaxMs"] = max,
                        ["samples"] = ticks,
                        ["players"] = players
                    });
                    _tickMsSum = 0; _tickMsMax = 0;
                }
                var errors = Interlocked.Exchange(ref _errorCount, 0);
                if (errors > 0)
                {
                    Enqueue("server.errors", new Dictionary<string, object>
                    {
                        ["count"] = errors,
                        ["sample"] = _errorSample ?? ""
                    });
                    _errorSample = null;
                }
                var slow = Interlocked.Exchange(ref _slowCommandCount, 0);
                if (slow > 0)
                    Enqueue("command.slow.count", new Dictionary<string, object> { ["count"] = slow });
            }
            catch { }
        }

        private void PumpLoop()
        {
            while (true)
            {
                try
                {
                    if (Outbox.IsEmpty) { Thread.Sleep(200); continue; }
                    var batch = new List<string>();
                    while (batch.Count < 200 && Outbox.TryDequeue(out var item)) batch.Add(item);
                    if (batch.Count == 0) continue;
                    var body = "{\"serverId\":" + Q(_serverId) + ",\"events\":[" + string.Join(",", batch) + "]}";
                    using (var req = new HttpRequestMessage(HttpMethod.Post, _tsmBase.TrimEnd('/') + "/tsm/plugins/bridge/events"))
                    {
                        req.Headers.TryAddWithoutValidation("X-TSM-Token", _token);
                        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        using (var resp = Http.SendAsync(req).GetAwaiter().GetResult())
                        {
                            // 即使失败也不重试堆积：桥接是"尽力送达"，TSM 侧有兜底轮询
                        }
                    }
                }
                catch { Thread.Sleep(1000); }
            }
        }

        private static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string ToJson(Dictionary<string, object> data)
        {
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in data)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Q(kv.Key)).Append(':');
                if (kv.Value == null) sb.Append("null");
                else if (kv.Value is string s) sb.Append(Q(s));
                else if (kv.Value is bool b) sb.Append(b ? "true" : "false");
                else sb.Append(Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
            }
            return sb.Append('}').ToString();
        }
    }
}
