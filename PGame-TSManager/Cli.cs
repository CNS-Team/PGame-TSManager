using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PGameTSManager
{
    /// <summary>
    /// 无界面指令通道（给远程运维 / 自检用）。用法：
    ///   PGame-TSManager.exe --send &lt;服务器名&gt; &lt;指令&gt;
    ///   例：PGame-TSManager.exe --send 生存 "/help"
    /// 走 TShock REST 的 rawcmd，结果同时写入 sendresult.txt。
    /// </summary>
    internal static class Cli
    {
        public static int Send(string[] args)
        {
            var sb = new StringBuilder();
            var ok = false;
            try
            {
                var list = new List<string>(args);
                var i = list.FindIndex(a => a.Equals("--send", StringComparison.OrdinalIgnoreCase));
                if (i < 0 || list.Count < i + 3)
                {
                    sb.AppendLine("用法: PGame-TSManager.exe --send <服务器名> <指令>");
                    return Write(sb, 2);
                }

                var name = list[i + 1];
                var cmd = string.Join(" ", list.Skip(i + 2)).Trim();
                if (cmd.Length > 0 && cmd[0] != '/') cmd = "/" + cmd;

                var cfg = ManagerConfig.Instance;
                var profiles = new List<ServerProfile>(cfg.LoadProfiles());
                var profile = profiles.FirstOrDefault(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    sb.AppendLine("找不到服务器：" + name);
                    sb.AppendLine("可用服务器：" + string.Join(" / ", profiles.Select(p => p.name)));
                    return Write(sb, 3);
                }

                var container = new ServerContainer(cfg, profile);
                var err = container.SendCommandViaRest(cmd, out var output);
                if (err == null)
                {
                    ok = true;
                    sb.AppendLine("服务器 : " + profile.name);
                    sb.AppendLine("指令   : " + cmd);
                    sb.AppendLine("回显   :");
                    sb.AppendLine(output);
                }
                else
                {
                    sb.AppendLine("发送失败：" + err);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("异常：" + ex.Message);
            }
            return Write(sb, ok ? 0 : 1);
        }

        /// <summary>
        /// `--dumpconfig`：无界面地把**加载后**的配置写成 configdump.txt（令牌只留前后 4 位），
        /// 用于核对「英文键 → 中文键」迁移后取值是否一致（部署前后各跑一次对比）。
        /// </summary>
        public static int DumpConfig()
        {
            var sb = new StringBuilder();
            var ok = false;
            try
            {
                var cfg = ManagerConfig.Instance;
                var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                var masked = MaskSecret(json, cfg.controlApiToken);
                sb.AppendLine("PGame-TSManager 配置自检（configdump）");
                sb.AppendLine("时间 : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("目录 : " + AppContext.BaseDirectory);
                sb.AppendLine("顶层键数 : " + (JObject.Parse(json).Properties().Count()));
                sb.AppendLine("服务器列表 : " + (cfg.serverProfiles?.Count ?? 0) + " 条");
                sb.AppendLine("外部进程   : " + (cfg.externalProcesses?.Count ?? 0) + " 条");
                sb.AppendLine("控制API : 开关=" + cfg.controlApiEnabled + " 地址=" + cfg.controlApiHost +
                              " 端口=" + cfg.controlApiPort + " 令牌=" + Describe(cfg.controlApiToken));
                sb.AppendLine("备份保留数=" + cfg.backupKeep + " 主程序备份保留数=" + cfg.exeBackupKeep +
                              " 日志保留天数=" + cfg.logRetentionDays);
                sb.AppendLine("看门狗 : 开关=" + cfg.watchdogEnabled + " 最大重启=" + cfg.watchdogMaxRestarts +
                              " 稳定判定秒=" + cfg.watchdogStableSeconds);
                sb.AppendLine("---- 迁移后的完整配置（令牌已打码）----");
                sb.AppendLine(masked);
                ok = true;
            }
            catch (Exception ex)
            {
                sb.AppendLine("异常：" + ex.Message);
            }

            var text = sb.ToString();
            try { Console.WriteLine(text); } catch { }
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "configdump.txt"), text, new UTF8Encoding(false)); } catch { }
            return ok ? 0 : 1;
        }

        private static string Describe(string? token) =>
            string.IsNullOrEmpty(token) ? "<空>" : $"<{token.Length} 位，前4={token.Substring(0, Math.Min(4, token.Length))}>";

        /// <summary>把序列化结果里的令牌值替换成打码形式（只留前 4 位）。</summary>
        private static string MaskSecret(string json, string? token)
        {
            if (string.IsNullOrEmpty(token)) return json;
            return json.Replace(token, token.Substring(0, Math.Min(4, token.Length)) + "****（已打码）");
        }

        private static int Write(StringBuilder sb, int code)
        {
            var text = sb.ToString();
            try { Console.WriteLine(text); } catch { }
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "sendresult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            return code;
        }
    }
}
