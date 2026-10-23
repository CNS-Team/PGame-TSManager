using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PGameTSManager
{
    /// <summary>
    /// 桥接插件（TSMBridge，装在 TShock 里、Harmony 只挂 TShockAPI）推上来的进程内事件。
    ///
    /// 协议（英文键，遵循"配置文件中文 / API 英文"两层口径）：
    ///   POST /tsm/plugins/bridge/events
    ///   { "serverId": "0", "events": [ { "type": "player.joined", "at": "...", "data": { ... } } ] }
    ///
    /// 为什么需要它：TSM 是进程外管理器，此前只能靠"端口/REST/日志关键字"猜服务器内部状态；
    /// 有了桥接，玩家进出、保存、插件加载、慢指令、错误率、tick 抖动都是**权威信号**。
    /// 【边界】桥接只"上报"，不做任何控制；TSM 也不因此改变启停/重建等主流程。
    /// </summary>
    internal static class BridgeState
    {
        internal sealed class ServerBridge
        {
            public string ServerId = "";
            public DateTimeOffset At;
            public double TickAvgMs;
            public double TickMaxMs;
            public int Players = -1;
            public DateTimeOffset? LastSaveAt;
            public long LastSaveSize;
            public string LastSaveMessage = "";
            public long ErrorCount;
            public string ErrorSample = "";
            public long SlowCommandCount;
            public string LastPluginLoaded = "";
            public int EventsReceived;
        }

        private static readonly Dictionary<string, ServerBridge> Map = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new();

        /// <summary>处理一批桥接事件：更新状态 + 按规则转发到 ControlEventHub（避免 SSE 刷屏）。</summary>
        public static int Ingest(string serverId, JArray? events)
        {
            if (events == null || events.Count == 0) return 0;
            var n = 0;
            lock (Gate)
            {
                if (!Map.TryGetValue(serverId, out var st)) { st = new ServerBridge { ServerId = serverId }; Map[serverId] = st; }
                foreach (var e in events.OfType<JObject>())
                {
                    var type = e["type"]?.ToString() ?? "";
                    if (type.Length == 0) continue;
                    var data = e["data"] as JObject ?? new JObject();
                    st.EventsReceived++;
                    st.At = DateTimeOffset.Now;
                    n++;

                    switch (type)
                    {
                        case "server.tick":
                            st.TickAvgMs = data["intervalAvgMs"]?.ToObject<double>() ?? st.TickAvgMs;
                            st.TickMaxMs = data["intervalMaxMs"]?.ToObject<double>() ?? st.TickMaxMs;
                            st.Players = data["players"]?.ToObject<int>() ?? st.Players;
                            // tick 常态不推 SSE（每服每 5 秒一条会淹没事件流）——只在异常时推
                            if (st.TickMaxMs >= 250)
                                ControlEventHub.Publish("server.tick.high", serverId, new { tickAvgMs = st.TickAvgMs, tickMaxMs = st.TickMaxMs, players = st.Players });
                            continue;
                        case "world.saved":
                            st.LastSaveAt = DateTimeOffset.Now;
                            st.LastSaveSize = data["size"]?.ToObject<long>() ?? 0;
                            st.LastSaveMessage = data["message"]?.ToString() ?? "";
                            break;
                        case "player.joined":
                        case "player.left":
                            st.Players = data["players"]?.ToObject<int>() ?? st.Players;
                            break;
                        case "server.errors":
                            st.ErrorCount += data["count"]?.ToObject<long>() ?? 0;
                            var sample = data["sample"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(sample)) st.ErrorSample = sample;
                            break;
                        case "command.slow.count":
                            st.SlowCommandCount += data["count"]?.ToObject<long>() ?? 0;
                            break;
                        case "plugin.loaded":
                            st.LastPluginLoaded = data["plugin"]?.ToString() ?? "";
                            break;
                    }

                    ControlEventHub.Publish(type, serverId, data);
                }
            }
            return n;
        }

        /// <summary>给 GET /tsm/bridge 用的快照（PascalCase 与 Control API 其它响应一致）。</summary>
        public static object Snapshot()
        {
            lock (Gate)
            {
                return Map.Values
                    .OrderBy(x => x.ServerId, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new
                    {
                        ServerId = x.ServerId,
                        At = x.At.ToString("o"),
                        x.Players,
                        x.TickAvgMs,
                        x.TickMaxMs,
                        LastSaveAt = x.LastSaveAt?.ToString("o"),
                        x.LastSaveSize,
                        x.LastSaveMessage,
                        x.ErrorCount,
                        x.ErrorSample,
                        x.SlowCommandCount,
                        x.LastPluginLoaded,
                        x.EventsReceived
                    })
                    .ToList();
            }
        }
    }
}
