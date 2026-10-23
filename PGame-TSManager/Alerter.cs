using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PGameTSManager
{
    /// <summary>
    /// 告警：把「服务器掉线 / 反复崩溃 / 日志出现致命异常」发到配置里指定的群。
    ///
    /// Core 不内置任何私人地址与群号：Webhook 和群号全部来自 config.json 的
    /// alertWebhookUrl / alertGroupId / alertGroupIds；未配置就等于关闭，发送失败静默处理，
    /// 绝不影响管理器本身。
    ///
    /// 上报由管理器进程内 HttpClient 直接完成，不调用 PowerShell、bat 或 cmd。
    /// </summary>
    internal static class Alerter
    {
        private static readonly object Gate = new();
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static DateTime _lastSentUtc = DateTime.MinValue;

        public static bool Enabled { get; set; }
        public static string WebhookUrl { get; set; } = "";
        public static long GroupId { get; set; }
        public static List<long> GroupIds { get; set; } = new();
        public static int MinIntervalSeconds { get; set; } = 60;

        /// <summary>最近一次发送结果（给界面显示用）。</summary>
        public static string LastResult { get; private set; } = "";

        /// <summary>按配置初始化（启动时调一次）。</summary>
        public static void Configure(ManagerConfig cfg)
        {
            try
            {
                Enabled = cfg.alertEnabled;
                MinIntervalSeconds = Math.Max(5, cfg.alertMinIntervalSeconds);
                GroupId = cfg.alertGroupId;
                GroupIds = (cfg.alertGroupIds != null && cfg.alertGroupIds.Count > 0)
                    ? cfg.alertGroupIds.Where(x => x > 0).Distinct().ToList()
                    : (GroupId > 0 ? new List<long> { GroupId } : new List<long>());

                WebhookUrl = (cfg.alertWebhookUrl ?? "").Trim();
            }
            catch { }
        }

        /// <summary>Webhook 是否可用（界面/日志里能提示）。</summary>
        public static bool Ready
        {
            get
            {
                try
                {
                    return Enabled &&
                           !string.IsNullOrWhiteSpace(WebhookUrl) &&
                           Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// 发一条告警。带最小间隔限流；真正发送放到后台线程，绝不阻塞界面。
        /// </summary>
        public static void Send(string title, string detail)
        {
            try
            {
                if (!Ready) { LastResult = "未启用或未配置有效告警 Webhook"; return; }

                lock (Gate)
                {
                    if ((DateTime.UtcNow - _lastSentUtc).TotalSeconds < MinIntervalSeconds)
                    {
                        LastResult = "被限流（距上次告警不足 " + MinIntervalSeconds + " 秒）";
                        return;
                    }
                    _lastSentUtc = DateTime.UtcNow;
                }

                var text = string.Join("\n", new[]
                {
                    "【PGame-TSManager 告警】",
                    "时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "主机：" + Environment.MachineName,
                    title,
                    detail
                });

                var webhook = WebhookUrl;
                var groups = GroupIds.Count > 0 ? GroupIds.ToList() : new List<long> { GroupId };
                Task.Run(async () =>
                {
                    try
                    {
                        var payload = JsonSerializer.Serialize(new
                        {
                            title,
                            text,
                            groups,
                            source = "PGame-TSManager",
                            status = "警告",
                            at = DateTimeOffset.Now
                        });
                        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                        using var response = await Http.PostAsync(webhook, content).ConfigureAwait(false);
                        LastResult = response.IsSuccessStatusCode
                            ? "已通过 Webhook 发送"
                            : "Webhook 返回 HTTP " + (int)response.StatusCode;
                    }
                    catch (Exception ex) { LastResult = "发送失败：" + ex.Message; }
                });
            }
            catch { }
        }
    }
}
