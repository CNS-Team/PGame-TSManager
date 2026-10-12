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
    }
}
