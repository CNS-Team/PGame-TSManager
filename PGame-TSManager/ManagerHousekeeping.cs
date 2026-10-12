using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PGameTSManager
{
    /// <summary>
    /// 管理器**自身**的占用清理（不动世界与备份）：
    ///   ① 主程序换版备份 `PGame-TSManager.exe.bak*` —— 按 <see cref="ManagerConfig.exeBackupKeep"/> 只留最近 N 份；
    ///   ② `Core\Logs\*.log` —— 删除超过 <see cref="ManagerConfig.logRetentionDays"/> 天的。
    ///
    /// 背景（2026-10-11/12 实测）：主 exe 是自包含单文件，**147.6 MB/份**，一天换 3 次版就 442 MB，没有上限；
    /// `Core\Logs` 也按天增长（≈0.4 MB/天，25 个文件/6.4 MB），同样没人清理。
    ///
    /// 边界（硬约束）：**只删这两类**；`Core\Backups`（世界/配置备份，归 backupKeep）与
    /// `Servers\Worlds\*.wld*` 一律不碰。
    /// </summary>
    internal static class ManagerHousekeeping
    {
        /// <summary>管理器自身日志文件名（清理记录写这里，便于事后 grep）。</summary>
        private static string ManagerLogPath => Path.Combine(
            ManagerConfig.Resolve(ManagerConfig.Instance.logDir),
            "PGame-TSManager-" + DateTime.Now.ToString("yyyyMMdd") + ".log");

        /// <summary>把一行写进管理器自身日志（失败静默，绝不影响主流程）。</summary>
        public static void Log(string line)
        {
            try
            {
                var path = ManagerLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + line + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>
        /// 跑一遍清理，返回给界面显示的一行摘要（同时写进管理器日志）。
        /// </summary>
        public static string RunOnce()
        {
            ManagerConfig cfg;
            try { cfg = ManagerConfig.Instance; }
            catch (Exception ex) { return "清理跳过（配置读取失败：" + ex.Message + "）"; }

            var parts = new List<string>();
            try { parts.Add(TrimExeBackups(cfg.exeBackupKeep)); } catch (Exception ex) { parts.Add("主程序备份清理失败：" + ex.Message); }
            try { parts.Add(TrimLogs(cfg.logRetentionDays)); } catch (Exception ex) { parts.Add("日志清理失败：" + ex.Message); }
            try { parts.Add(TrimSandboxLogs(cfg.logRetentionDays)); } catch (Exception ex) { parts.Add("沙箱日志清理失败：" + ex.Message); }
            if (cfg.cleanupStaleArtifacts)
            {
                try { parts.Add(TrimDisabledPluginLeftovers()); } catch (Exception ex) { parts.Add("停用插件残留清理失败：" + ex.Message); }
                try { parts.Add(TrimAppPublishLeftovers()); } catch (Exception ex) { parts.Add("发布残留清理失败：" + ex.Message); }
            }

            var summary = "[清理] " + string.Join("；", parts);
            Log(summary);
            return summary;
        }

        /// <summary>主程序换版备份：保留最近 keep 份（按修改时间），其余删除。keep&lt;=0 时全删。</summary>
        private static string TrimExeBackups(int keep)
        {
            var dir = ManagerConfig.BaseDir;
            var files = new DirectoryInfo(dir)
                .GetFiles("PGame-TSManager.exe.bak*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            if (files.Count == 0) return "主程序备份 0 份（无需裁剪）";

            var limit = keep < 0 ? 0 : keep;
            var drop = files.Skip(limit).ToList();
            if (drop.Count == 0)
                return $"主程序备份 {files.Count} 份 ≤ 上限 {limit}（未删）";

            long freed = 0;
            var names = new List<string>();
            foreach (var f in drop)
            {
                try
                {
                    var size = f.Length;
                    f.Delete();
                    freed += size;
                    names.Add(f.Name);
                }
                catch { }
            }
            return $"主程序备份 {files.Count} → 保留 {files.Count - drop.Count} 份（上限 {limit}），删除 {names.Count} 份 / " +
                   $"{freed / 1024.0 / 1024.0:F1} MB" + (names.Count > 0 ? "：" + string.Join(", ", names) : "");
        }

        /// <summary>日志：删除修改时间早于 N 天的 `*.log`（当天/未来文件永不动）。</summary>
        private static string TrimLogs(int days)
        {
            var dir = ManagerConfig.Resolve(ManagerConfig.Instance.logDir);
            if (!Directory.Exists(dir)) return "日志目录不存在（无需清理）";
            if (days <= 0) return "日志保留天数 ≤ 0（按配置不清理）";

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var files = new DirectoryInfo(dir).GetFiles("*.log", SearchOption.TopDirectoryOnly);
            long freed = 0;
            var n = 0;
            foreach (var f in files)
            {
                try
                {
                    if (f.LastWriteTimeUtc >= cutoff) continue;
                    var size = f.Length;
                    f.Delete();
                    freed += size;
                    n++;
                }
                catch { }
            }
            return n == 0
                ? $"日志 {files.Length} 个（保留 {days} 天，无过期）"
                : $"日志删除 {n} 个 / {freed / 1024.0 / 1024.0:F2} MB（保留 {days} 天，剩 {files.Length - n} 个）";
        }

        /// <summary>
        /// 各服运行沙箱里的 TShock 自己写的日志（`Core\_runtime\&lt;服&gt;\Logs\*.log`）。
        /// 管理器日志有保留策略，但沙箱日志此前没人清 —— 沙箱里那些文件会一直涨。
        /// 同样只删 `*.log`，不碰沙箱里其它任何东西。
        /// </summary>
        private static string TrimSandboxLogs(int days)
        {
            if (days <= 0) return "沙箱日志保留天数 ≤ 0（按配置不清理）";
            var runtime = ManagerConfig.Resolve(ManagerConfig.Instance.runtimeDir);
            if (!Directory.Exists(runtime)) return "沙箱目录不存在（无需清理）";

            var cutoff = DateTime.UtcNow.AddDays(-days);
            long freed = 0;
            var deleted = 0;
            var scanned = 0;
            foreach (var dir in Directory.GetDirectories(runtime))
            {
                var logDir = Path.Combine(dir, "Logs");
                if (!Directory.Exists(logDir)) continue;
                foreach (var f in new DirectoryInfo(logDir).GetFiles("*.log", SearchOption.TopDirectoryOnly))
                {
                    scanned++;
                    try
                    {
                        if (f.LastWriteTimeUtc >= cutoff) continue;
                        var size = f.Length;
                        f.Delete();
                        freed += size;
                        deleted++;
                    }
                    catch { }
                }
            }
            return deleted == 0
                ? $"沙箱日志 {scanned} 个（保留 {days} 天，无过期）"
                : $"沙箱日志删除 {deleted} 个 / {freed / 1024.0 / 1024.0:F2} MB（保留 {days} 天，剩 {scanned - deleted} 个）";
        }

        /// <summary>
        /// `Core\_runtime\&lt;服&gt;\ServerPlugins.disabled\*.dll` 的**冗余**残留。
        ///
        /// 规则（保守）：**只删"插件总库里还有同名 DLL"的那些** —— 它们随时可由 PluginSync 恢复，
        /// 留着纯粹占用与迷惑（沙箱里那份停用副本毫无信息量）；总库里没有同名的（或 `.bak-*`、
        /// `server.properties` 之类非 dll）一律**保留**并在清理行里报数，绝不丢唯一副本。
        /// </summary>
        private static string TrimDisabledPluginLeftovers()
        {
            var cfg = ManagerConfig.Instance;
            var runtime = ManagerConfig.Resolve(cfg.runtimeDir);
            var library = ManagerConfig.Resolve(cfg.pluginDir);
            if (!Directory.Exists(runtime)) return "停用插件残留 0 个（无沙箱目录）";

            var deleted = 0; var kept = 0; long freed = 0;
            foreach (var dir in Directory.GetDirectories(runtime))
            {
                var dis = Path.Combine(dir, "ServerPlugins.disabled");
                if (!Directory.Exists(dis)) continue;
                foreach (var f in new DirectoryInfo(dis).GetFiles("*.dll", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (!File.Exists(Path.Combine(library, f.Name))) { kept++; continue; }   // 总库没有 → 保留（唯一副本）
                        var size = f.Length;
                        f.Delete();
                        freed += size;
                        deleted++;
                    }
                    catch { kept++; }
                }
            }
            return deleted == 0
                ? $"停用插件残留 0 个可清（保留 {kept} 个总库无同名的）"
                : $"停用插件残留删除 {deleted} 个 / {freed / 1024.0:F1} KB（总库可恢复；保留 {kept} 个总库无同名的）";
        }

        /// <summary>
        /// 程序目录里**另一次发布模式**的残留：`PGame-TSManager.dll` / `.deps.json` / `.runtimeconfig.json`。
        ///
        /// 安全性靠**发布形态判定**：只有当前 exe 是**单文件自包含**（&gt;50MB）时才删 ——
        /// 框架依赖发布的小 exe + dll 组合绝不会被误删（那种情况下 exe 只有几百 KB）。
        /// </summary>
        private static string TrimAppPublishLeftovers()
        {
            var dir = ManagerConfig.BaseDir;
            var exe = Path.Combine(dir, "PGame-TSManager.exe");
            if (!File.Exists(exe)) return "发布残留 0 个（无 exe）";

            var exeSize = new FileInfo(exe).Length;
            if (exeSize < 50L * 1024 * 1024)
                return "发布残留未清（当前 exe 不是单文件自包含，dll 可能是运行必需，跳过）";

            var names = new[] { "PGame-TSManager.dll", "PGame-TSManager.deps.json", "PGame-TSManager.runtimeconfig.json" };
            var deleted = 0; long freed = 0;
            foreach (var n in names)
            {
                var p = Path.Combine(dir, n);
                if (!File.Exists(p)) continue;
                try
                {
                    var fi = new FileInfo(p);
                    if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours < 24) continue;   // 24h 内不动（可能正在部署）
                    var size = fi.Length;
                    fi.Delete();
                    freed += size;
                    deleted++;
                }
                catch { }
            }
            return deleted == 0
                ? "发布残留 0 个可清（单文件自包含，无遗留 dll/deps/runtimeconfig）"
                : $"发布残留删除 {deleted} 个 / {freed / 1024.0:F1} KB（单文件自包含部署下的另一次发布残留）";
        }
    }
}
