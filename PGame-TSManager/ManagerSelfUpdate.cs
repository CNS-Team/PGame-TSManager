using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PGameTSManager;

/// <summary>
/// 管理器自恢复：由同一 exe 的临时副本执行，不依赖 PowerShell / bat / cmd。
/// </summary>
internal static class ManagerSelfUpdate
{
    public static int Run(string[] args)
    {
        if (args.Length < 4) return 2;
        if (!int.TryParse(args[3], out var parentPid)) return 2;

        var backup = Path.GetFullPath(args[1]);
        var root = Path.GetFullPath(args[2]).TrimEnd('\\');
        var log = Path.Combine(root, "manager-restore.log");

        try
        {
            WaitForExit(parentPid, TimeSpan.FromSeconds(30));
            if (!Directory.Exists(backup)) throw new DirectoryNotFoundException(backup);

            foreach (var file in Directory.GetFiles(backup, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(backup, file);
                var target = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                CopyWithRetry(file, target);
            }

            var exe = Path.Combine(root, "PGame-TSManager.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException(exe);
            Process.Start(new ProcessStartInfo(exe, "--startall")
            {
                WorkingDirectory = root,
                UseShellExecute = true
            });
            Append(log, "OK " + DateTime.Now.ToString("O"));
            return 0;
        }
        catch (Exception ex)
        {
            Append(log, "FAIL " + DateTime.Now.ToString("O") + Environment.NewLine + ex);
            return 1;
        }
    }

    private static void WaitForExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
        }
    }

    private static void CopyWithRetry(string source, string target)
    {
        Exception? last = null;
        for (var i = 0; i < 20; i++)
        {
            try
            {
                File.Copy(source, target, true);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(500);
            }
        }
        throw last ?? new IOException("copy failed: " + target);
    }

    private static void Append(string path, string text)
    {
        try
        {
            File.AppendAllText(path, text + Environment.NewLine, new UTF8Encoding(false));
        }
        catch
        {
        }
    }
}
