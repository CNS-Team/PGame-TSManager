using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PGameTSManager;

/// <summary>
/// 运行期脚本禁令的唯一判定入口。
/// 管理器本身、外部进程、打包产物和自检都使用同一套扩展名规则。
/// </summary>
internal static class ScriptPolicy
{
    private static readonly string[] ScriptExtensions = { ".ps1", ".bat", ".cmd", ".sh" };
    private static readonly string[] BuildDirectories =
        { ".git", "bin", "obj", "node_modules", "artifacts", "out", "publish", "test-packages" };

    public static bool IsScript(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var extension = Path.GetExtension(path);
        return ScriptExtensions.Any(x => x.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> FindScripts(string root, bool excludeBuildDirectories = true)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return Array.Empty<string>();

        var fullRoot = Path.GetFullPath(root);
        var scripts = new List<string>();
        var pending = new Stack<string>();
        pending.Push(fullRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                    if (IsScript(file)) scripts.Add(file);

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var name = Path.GetFileName(child);
                    if (excludeBuildDirectories &&
                        BuildDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        // 不跟随 junction / symlink，避免运行沙箱链接导致递归。
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch { continue; }
                    pending.Push(child);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        scripts.Sort(StringComparer.OrdinalIgnoreCase);
        return scripts;
    }

    public static void ThrowIfScript(string path, string context)
    {
        if (IsScript(path))
            throw new InvalidOperationException(
                $"{context} 不允许脚本：{Path.GetFileName(path)}。请改为原生 exe 或 C# 底层实现。");
    }

}
