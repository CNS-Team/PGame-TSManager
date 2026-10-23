using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PGameTSManager;

/// <summary>
/// 无脚本维护入口。所有维护功能由管理器 exe 自身执行，不调用 .ps1/.bat/.cmd。
/// 用法：PGame-TSManager.exe --tool &lt;command&gt; [options]
/// </summary>
internal static class AdminCli
{
    private static readonly HttpClient Http = CreateHttpClient();

    public static int Run(string[] args)
    {
        var index = Array.FindIndex(args, a =>
            string.Equals(a, "--tool", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
            return Usage();

        var command = args[index + 1].Trim().ToLowerInvariant();
        var rest = args.Skip(index + 2).ToArray();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var capture = new StringWriter();
        Console.SetOut(capture);
        Console.SetError(capture);
        try
        {
            var exitCode = command switch
            {
                "script-audit" => ScriptAudit(rest),
                "config-check" => ConfigCheck(rest),
                "new-server" => NewServer(rest),
                "add-templates" => AddTemplates(rest),
                "package" => Package(rest),
                "migrate-layout" => MigrateLayout(rest),
                "prune-releases" => PruneReleases(rest),
                "update-tshock" => UpdateTShock(rest),
                "housekeeping" => Housekeeping(),
                "help" or "--help" or "-h" => Usage(),
                _ => Fail("未知工具命令：" + command)
            };
            return exitCode;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            var text = capture.ToString();
            try
            {
                File.WriteAllText(Path.Combine(ManagerConfig.BaseDir, "tool-result.txt"), text, Utf8NoBom);
            }
            catch { }
            try { Console.Write(text); } catch { }
        }
    }

    private static int Usage()
    {
        Console.WriteLine(
            "PGame-TSManager 无脚本维护工具\n" +
            "  --tool script-audit [--root <dir>] [--repo]\n" +
            "  --tool config-check <serverPath> [--mapping <json>] [--json]\n" +
            "  --tool new-server --root <dir> --name <name> --port <n> --rest-port <n> [--world <file>] [--template <dir>] [--plugins-from-template] [--force]\n" +
            "  --tool add-templates --root <dir>\n" +
            "  --tool package --root <repo> [--out <dir>] [--skip-runtime] [--version <ver>]\n" +
            "  --tool migrate-layout --root <dir> [--apply] [--include-source]\n" +
            "  --tool prune-releases --repo <owner/name> --token <token> [--keep 10] [--dry-run]\n" +
            "  --tool update-tshock --manager <dir> --package <zip> [--apply]\n" +
            "  --tool housekeeping\n" +
            "\n所有命令均为管理器进程内 C# 实现；不启动 PowerShell、bat 或 cmd。");
        return 0;
    }

    private static int ScriptAudit(string[] args)
    {
        var root = Option(args, "--root") ?? ManagerConfig.BaseDir;
        var excludeBuild = !HasFlag(args, "--include-build");
        var scripts = ScriptPolicy.FindScripts(root, excludeBuild);
        var result = new
        {
            ok = scripts.Count == 0,
            root = Path.GetFullPath(root),
            count = scripts.Count,
            scripts
        };
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
        return scripts.Count == 0 ? 0 : 1;
    }

    private static int ConfigCheck(string[] args)
    {
        var serverPath = Positional(args, 0);
        if (string.IsNullOrWhiteSpace(serverPath))
            return Fail("config-check 需要服务端目录。");

        var server = Path.GetFullPath(serverPath);
        if (!Directory.Exists(server))
            return Fail("服务端目录不存在：" + server);

        var mappingPath = Option(args, "--mapping") ??
            FirstExisting(
                Path.Combine(AppContext.BaseDirectory, "maintenance", "config-translation", "TransferPatch.json"),
                Path.Combine(AppContext.BaseDirectory, "Tools", "config-translation", "TransferPatch.json"),
                Path.Combine(server, "..", "..", "..", "maintenance", "config-translation", "TransferPatch.json"));

        var checks = new List<object>();
        void Check(string name, bool ok, string detail)
            => checks.Add(new { check = name, ok, detail });

        var english = LoadEnglishMapping(mappingPath);
        Check("汉化映射文件", english.Count > 0,
            english.Count > 0 ? $"共 {english.Count} 条映射" : "未找到或为空：" + (mappingPath ?? "<未提供>"));

        var configPath = Path.Combine(server, "tshock", "config.json");
        if (!File.Exists(configPath))
        {
            Check("config.json 存在", false, configPath);
        }
        else
        {
            var cfg = ReadObject(configPath);
            var settings = cfg["Settings"] as JObject;
            if (settings == null)
            {
                Check("config.json 结构", false, "缺少 Settings 节点");
            }
            else
            {
                var keys = settings.Properties().Select(p => p.Name).ToArray();
                Check("config.json 结构", true, $"Settings 共 {keys.Length} 项");

                var present = keys.Where(english.ContainsKey).ToArray();
                Check("config.json 中文键", present.Length == 0,
                    present.Length == 0 ? "未发现英文键，汉化已应用" : "发现英文键: " + string.Join(", ", present));

                var missing = english.Values.Where(v => !keys.Contains(v, StringComparer.Ordinal)).ToArray();
                Check("config.json 覆盖度", true,
                    missing.Length == 0
                        ? $"已覆盖全部 {english.Count} 个映射字段"
                        : "新版本可能新增字段，需更新映射: " + string.Join(", ", missing));

                var rest = settings["Rest外部应用令牌字典"] ?? settings["ApplicationRestTokens"];
                if (rest is not JObject tokens)
                {
                    Check("REST 令牌字典", true, "未配置 REST 令牌（跳过）");
                }
                else
                {
                    var bad = new List<string>();
                    foreach (var token in tokens.Properties())
                    {
                        var fields = (token.Value as JObject)?.Properties().Select(p => p.Name).ToArray() ?? Array.Empty<string>();
                        if (fields.Contains("Username") || fields.Contains("UserGroupName"))
                            bad.Add(token.Name + ": 仍是 Username/UserGroupName");
                        else if (!fields.Contains("用户名") || !fields.Contains("用户组"))
                            bad.Add(token.Name + ": 缺少 用户名/用户组");
                    }
                    Check("REST 令牌字段", bad.Count == 0,
                        bad.Count == 0 ? $"OK（{tokens.Properties().Count()} 个令牌）" : string.Join("; ", bad));
                }
            }
        }

        var sscPath = Path.Combine(server, "tshock", "sscconfig.json");
        if (!File.Exists(sscPath))
        {
            Check("SSC 保持英文", true, "未启用 SSC（文件不存在，跳过）");
        }
        else
        {
            var ssc = ReadObject(sscPath);
            var names = AllPropertyNames(ssc);
            var chinese = names.Where(ContainsCjk).ToArray();
            Check("SSC 保持英文", chinese.Length == 0,
                chinese.Length == 0 ? $"OK（{names.Count} 项，全英文）" : "发现中文键: " + string.Join(", ", chinese));
        }

        var failed = checks.Count(x => !(bool)x.GetType().GetProperty("ok")!.GetValue(x)!);
        var payload = new { server, result = failed == 0 ? "OK" : "FAIL", checks };
        if (HasFlag(args, "--json"))
        {
            Console.WriteLine(JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
        else
        {
            Console.WriteLine("PGame-TSManager · TShock 配置校验");
            Console.WriteLine("服务端: " + server);
            Console.WriteLine(new string('-', 60));
            foreach (dynamic check in checks)
                Console.WriteLine((check.ok ? "[OK]   " : "[FAIL] ") + check.check + "  - " + check.detail);
            Console.WriteLine(new string('-', 60));
            Console.WriteLine("RESULT: " + payload.result);
        }
        return failed == 0 ? 0 : 1;
    }

    private static int NewServer(string[] args)
    {
        var root = Option(args, "--root") ?? ManagerConfig.BaseDir;
        var name = Option(args, "--name");
        var port = OptionInt(args, "--port");
        var restPort = OptionInt(args, "--rest-port");
        if (string.IsNullOrWhiteSpace(name) || port is null || restPort is null)
            return Fail("new-server 需要 --root、--name、--port、--rest-port。");

        var profiles = Path.Combine(root, "Servers", "Profiles");
        Directory.CreateDirectory(profiles);
        var dirs = Directory.GetDirectories(profiles)
            .Select(Path.GetFileName)
            .Where(x => x != null)
            .ToArray();
        var next = dirs.Select(OrdinalOf).DefaultIfEmpty(0).Max() + 1;
        var cleanName = Regex.Replace(name.Trim(), @"^\d+[\.\u3001\-_ ]*", "");
        var directoryName = next + "." + cleanName;
        var target = Path.Combine(profiles, directoryName);
        if (Directory.Exists(target) && !HasFlag(args, "--force"))
            return Fail("目标已存在：" + target);

        var template = Option(args, "--template");
        var templateDir = string.IsNullOrWhiteSpace(template)
            ? dirs.Select(d => Path.Combine(profiles, d!)).FirstOrDefault(Directory.Exists)
            : dirs.Select(d => Path.Combine(profiles, d!))
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), template, StringComparison.OrdinalIgnoreCase) ||
                                     Path.GetFileName(d).EndsWith("." + template, StringComparison.OrdinalIgnoreCase));
        Directory.CreateDirectory(target);
        if (!string.IsNullOrWhiteSpace(templateDir))
        {
            var sourceTshock = Path.Combine(templateDir, "tshock");
            if (Directory.Exists(sourceTshock))
                CopyDirectory(sourceTshock, Path.Combine(target, "tshock"), true);
        }

        var tshock = Path.Combine(target, "tshock");
        Directory.CreateDirectory(tshock);
        EnsureWhitelist(Path.Combine(tshock, "whitelist.txt"));

        var world = Option(args, "--world") ?? cleanName + ".wld";
        var plugins = new JArray();
        if (HasFlag(args, "--plugins-from-template") && !string.IsNullOrWhiteSpace(templateDir))
        {
            var templateManifest = Path.Combine(templateDir, "config.json");
            if (File.Exists(templateManifest))
            {
                var pluginValue = ReadObject(templateManifest)["插件"] as JArray;
                if (pluginValue != null) plugins = (JArray)pluginValue.DeepClone();
            }
        }

        var manifest = new JObject
        {
            ["服务器名称"] = cleanName,
            ["启用"] = true,
            ["世界"] = world,
            ["语言"] = 7,
            ["端口"] = port.Value,
            ["REST端口"] = restPort.Value,
            ["最大玩家"] = 252,
            ["IP"] = "0.0.0.0",
            ["密码"] = "",
            ["启动参数"] = "",
            ["插件"] = plugins,
            ["备注"] = $"端口 {port} / REST {restPort}"
        };
        WriteJson(Path.Combine(target, "config.json"), manifest);
        Console.WriteLine("已创建：" + target);
        Console.WriteLine($"世界：{world}；端口：{port}；REST：{restPort}；插件：{plugins.Count}");
        Console.WriteLine("下一步：按该服数据库、端口和 REST 令牌调整 tshock\\config.json，并把世界放到 Servers\\Worlds。");
        return 0;
    }

    private static int AddTemplates(string[] args)
    {
        var root = Option(args, "--root") ?? ManagerConfig.BaseDir;
        var templatePath = FirstExisting(
            Path.Combine(root, "maintenance", "templates", "tshock-generic.zh-CN.json"),
            Path.Combine(root, "Tools", "templates", "tshock-generic.zh-CN.json"),
            Path.Combine(AppContext.BaseDirectory, "maintenance", "templates", "tshock-generic.zh-CN.json"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "templates", "tshock-generic.zh-CN.json"));
        if (templatePath == null)
            return Fail("找不到 tshock-generic.zh-CN.json 模板。");

        var whitelistPath = FirstExisting(
            Path.Combine(root, "maintenance", "templates", "whitelist.txt"),
            Path.Combine(root, "Tools", "templates", "whitelist.txt"),
            Path.Combine(AppContext.BaseDirectory, "maintenance", "templates", "whitelist.txt"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "templates", "whitelist.txt"));

        var profiles = Path.Combine(root, "Servers", "Profiles");
        var worlds = Path.Combine(root, "Servers", "Worlds");
        var plugins = Path.Combine(root, "Plugins");
        foreach (var dir in new[] { profiles, worlds, plugins, Path.Combine(root, "Core"), Path.Combine(root, "Tools") })
            Directory.CreateDirectory(dir);

        CopyRuntimePlugins(root, plugins);
        var definitions = new[]
        {
            new { Name = "生存", Directory = "1.生存", Port = 7777, RestPort = 7878, World = "生存.wld" },
            new { Name = "生存2", Directory = "2.生存2", Port = 7778, RestPort = 7879, World = "生存2.wld" }
        };
        var managerProfiles = new JArray();
        foreach (var definition in definitions)
        {
            var profileDir = Path.Combine(profiles, definition.Directory);
            var tshockDir = Path.Combine(profileDir, "tshock");
            Directory.CreateDirectory(tshockDir);
            var tshock = ReadObject(templatePath);
            var settings = tshock["Settings"] as JObject ?? new JObject();
            settings["服务器名称"] = definition.Name;
            settings["服务器端口"] = definition.Port;
            settings["Rest的端口"] = definition.RestPort;
            tshock["Settings"] = settings;
            WriteJson(Path.Combine(tshockDir, "config.json"), tshock);
            if (whitelistPath != null)
                File.Copy(whitelistPath, Path.Combine(tshockDir, "whitelist.txt"), true);
            else
                EnsureWhitelist(Path.Combine(tshockDir, "whitelist.txt"));

            var manifest = new JObject
            {
                ["服务器名称"] = definition.Name,
                ["启用"] = true,
                ["世界"] = definition.World,
                ["语言"] = 7,
                ["端口"] = definition.Port,
                ["REST端口"] = definition.RestPort,
                ["最大玩家"] = 16,
                ["IP"] = "0.0.0.0",
                ["密码"] = "",
                ["启动参数"] = "",
                ["插件"] = new JArray("TShockAPI.dll", "HotReload.dll"),
                ["插件总库"] = "Plugins",
                ["覆盖插件目录"] = true,
                ["备注"] = $"通用模板 {definition.Port} / REST {definition.RestPort}"
            };
            WriteJson(Path.Combine(profileDir, "config.json"), manifest);
            managerProfiles.Add(new JObject
            {
                ["服务器名称"] = definition.Name,
                ["目录"] = "Servers\\Profiles\\" + definition.Directory,
                ["可执行文件"] = "TShock.Server.exe",
                ["启动参数"] = "",
                ["启用"] = true,
                ["备注"] = $"通用模板 {definition.Port} / REST {definition.RestPort}",
                ["插件"] = new JArray("TShockAPI.dll", "HotReload.dll"),
                ["插件总库"] = ""
            });
        }

        var configPath = Path.Combine(root, "config.json");
        var config = File.Exists(configPath) ? ReadObject(configPath) : new JObject();
        config["启动前备份"] = true;
        config["备份目录"] = "Core\\Backups";
        config["备份保留数"] = 10;
        config["插件总库"] = "Plugins";
        config["启动前同步插件"] = true;
        config["停用未列出插件"] = true;
        config["停用插件目录名"] = "ServerPlugins.disabled";
        config["服务器列表"] = managerProfiles;
        config["每服独立窗口"] = false;
        config["世界目录"] = "Servers\\Worlds";
        config["运行沙箱目录"] = "Core\\_runtime";
        config["服务器目录"] = "Servers\\Profiles";
        config["清单文件名"] = "config.json";
        config["服务端可执行文件"] = "TShock.Server.exe";
        config["运行时母本目录"] = "Core";
        config["日志目录"] = "Core\\Logs";
        config["数据目录"] = "Core\\Data";
        config["工具目录"] = "Tools";
        config["顺序启动"] = true;
        config["看门狗开关"] = true;
        config["告警开关"] = false;
        config["告警Webhook"] = "";
        WriteJson(configPath, config);
        File.WriteAllText(Path.Combine(worlds, ".gitkeep"), "", Utf8NoBom);
        File.WriteAllText(Path.Combine(plugins, ".gitkeep"), "", Utf8NoBom);
        File.WriteAllText(Path.Combine(root, "Core", ".gitkeep"), "", Utf8NoBom);
        Console.WriteLine("TShock 模板已加入：1.生存 (7777/7878)、2.生存2 (7778/7879)");
        return 0;
    }

    private static int Package(string[] args)
    {
        var root = Path.GetFullPath(Option(args, "--root") ?? Environment.CurrentDirectory);
        var output = Path.GetFullPath(Option(args, "--out") ?? Path.Combine(root, "artifacts"));
        var project = Path.Combine(root, "PGame-TSManager", "PGame-TSManager.csproj");
        if (!File.Exists(project))
            return Fail("找不到项目：" + project);

        Directory.CreateDirectory(output);
        var managerDir = Path.Combine(output, "manager");
        var coreDir = Path.Combine(managerDir, "Core");
        var version = Option(args, "--version") ?? ReadVersion(project);
        var commit = RunCapture("git", new[] { "-C", root, "rev-parse", "HEAD" }).Trim();
        if (string.IsNullOrWhiteSpace(commit)) commit = "unknown";
        Console.WriteLine($"版本={version} 提交={commit[..Math.Min(12, commit.Length)]}");

        Console.WriteLine("1/5 dotnet publish（自包含单文件）");
        var publish = RunProcess("dotnet", new[]
        {
            "publish", project, "-c", "Release", "-r", "win-x64",
            "--self-contained", "true", "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:DebugType=None",
            "-o", managerDir
        });
        if (publish != 0) return Fail("dotnet publish 失败，exit=" + publish);

        if (!HasFlag(args, "--skip-runtime"))
        {
            Console.WriteLine("2/5 下载 TShock 运行时");
            PrepareRuntime(root, coreDir).GetAwaiter().GetResult();
        }
        else
        {
            Console.WriteLine("2/5 跳过 TShock 运行时");
        }

        Console.WriteLine("3/5 生成说明与模板");
        var templateZip = Path.Combine(output, "PGame-TSManager-template.zip");
        var universalZip = Path.Combine(output, "PGame-TSManager-universal.zip");
        var sourceZip = Path.Combine(output, "PGame-TSManager-source.zip");
        BuildTemplate(root, managerDir, coreDir, templateZip, version, commit, false);
        BuildTemplate(root, managerDir, coreDir, universalZip, version, commit, true);

        Console.WriteLine("4/5 打包源码");
        BuildSource(root, sourceZip);

        Console.WriteLine("5/5 产物脚本审计");
        var scripts = ScriptPolicy.FindScripts(output, false);
        if (scripts.Count > 0)
            return Fail("产物中发现脚本，拒绝交付：\n" + string.Join("\n", scripts));

        foreach (var file in new[] { templateZip, universalZip, sourceZip })
        {
            var item = new FileInfo(file);
            Console.WriteLine($"{item.Length / 1024d / 1024d:N1} MB  {item.FullName}");
        }
        return 0;
    }

    private static int MigrateLayout(string[] args)
    {
        var root = Path.GetFullPath(Option(args, "--root") ?? ManagerConfig.BaseDir);
        if (!Directory.Exists(root)) return Fail("目录不存在：" + root);
        var apply = HasFlag(args, "--apply");
        var moves = new List<(string Source, string Destination)>();
        var core = Path.Combine(root, "Core");
        var servers = Path.Combine(root, "Servers");
        var profiles = Path.Combine(servers, "Profiles");
        var tools = Path.Combine(root, "Tools");
        foreach (var name in new[] { "TShock.Server.exe", "GeoIP.dat", "bin", "i18n", "runtimes", "x64", "Data", "Logs", "Backups", "_runtime" })
            moves.Add((Path.Combine(root, name), Path.Combine(core, name)));
        moves.Add((Path.Combine(root, "Worlds"), Path.Combine(servers, "Worlds")));
        moves.Add((Path.Combine(root, "maintenance"), tools));

        foreach (var move in moves)
        {
            if (!File.Exists(move.Source) && !Directory.Exists(move.Source)) continue;
            Console.WriteLine((apply ? "[移动] " : "[预览] ") + move.Source + " -> " + move.Destination);
            if (apply) MoveInto(move.Source, move.Destination);
        }
        var legacy = Path.Combine(root, "1.PigeonServers");
        if (Directory.Exists(legacy))
        {
            foreach (var child in Directory.GetFileSystemEntries(legacy))
            {
                var destination = Path.Combine(profiles, Path.GetFileName(child));
                Console.WriteLine((apply ? "[移动] " : "[预览] ") + child + " -> " + destination);
                if (apply) MoveInto(child, destination);
            }
        }
        if (HasFlag(args, "--include-source") && apply)
        {
            var sourceArchive = Path.Combine(tools, "Source");
            foreach (var name in new[] { "PGame-TSManager", "TerrariaServerAPI", "personal-overlay", ".github" })
            {
                var source = Path.Combine(root, name);
                if (Directory.Exists(source)) MoveInto(source, Path.Combine(sourceArchive, name));
            }
        }
        if (apply)
        {
            var configPath = Path.Combine(root, "config.json");
            if (File.Exists(configPath))
            {
                var config = ReadObject(configPath);
                config["运行时母本目录"] = "Core";
                config["世界目录"] = "Servers\\Worlds";
                config["插件总库"] = "Plugins";
                config["运行沙箱目录"] = "Core\\_runtime";
                config["服务器目录"] = "Servers\\Profiles";
                config["备份目录"] = "Core\\Backups";
                config["日志目录"] = "Core\\Logs";
                config["数据目录"] = "Core\\Data";
                config["工具目录"] = "Tools";
                if (config["服务器列表"] is JArray list)
                {
                    foreach (var profile in list.OfType<JObject>())
                    {
                        var dir = profile["目录"]?.ToString() ?? "";
                        profile["目录"] = dir.Replace("1.PigeonServers", "Servers\\Profiles", StringComparison.OrdinalIgnoreCase);
                    }
                }
                WriteJson(configPath, config);
            }
            foreach (var dir in new[] { core, profiles, Path.Combine(servers, "Worlds"), Path.Combine(root, "Plugins"), tools })
                Directory.CreateDirectory(dir);
        }
        Console.WriteLine(apply ? "布局迁移完成。" : "预览完成；加 --apply 执行。");
        return 0;
    }

    private static int PruneReleases(string[] args)
    {
        var repo = Option(args, "--repo");
        var token = Option(args, "--token");
        var keep = OptionInt(args, "--keep") ?? 10;
        var dryRun = HasFlag(args, "--dry-run");
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(token) || keep < 1)
            return Fail("prune-releases 需要 --repo、--token，且 --keep >= 1。");

        var releases = new List<JObject>();
        for (var page = 1; page <= 100; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}");
            AddGitHubHeaders(request, token);
            using var response = Http.Send(request);
            response.EnsureSuccessStatusCode();
            var batch = JArray.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            releases.AddRange(batch.OfType<JObject>());
            if (batch.Count < 100) break;
        }
        var ordered = releases.OrderByDescending(r => r["created_at"]?.Value<DateTime>() ?? DateTime.MinValue).ToArray();
        var remove = ordered.Skip(keep).ToArray();
        Console.WriteLine($"Release 总数：{ordered.Length}，保留：{Math.Min(keep, ordered.Length)}，清理：{remove.Length}");
        foreach (var release in remove)
        {
            var tag = release["tag_name"]?.ToString() ?? "";
            Console.WriteLine((dryRun ? "[预览] 删除 Release：" : "删除 Release：") + tag);
            if (dryRun) continue;
            using var deleteRelease = new HttpRequestMessage(HttpMethod.Delete,
                $"https://api.github.com/repos/{repo}/releases/{release["id"]}");
            AddGitHubHeaders(deleteRelease, token);
            using var response = Http.Send(deleteRelease);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("删除 Release 失败：" + tag + " / " + response.StatusCode);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(tag))
            {
                using var deleteTag = new HttpRequestMessage(HttpMethod.Delete,
                    $"https://api.github.com/repos/{repo}/git/refs/tags/{Uri.EscapeDataString(tag)}");
                AddGitHubHeaders(deleteTag, token);
                using var tagResponse = Http.Send(deleteTag);
                Console.WriteLine(tagResponse.IsSuccessStatusCode ? "已删除 Tag：" + tag : "Tag 不存在或无需删除：" + tag);
            }
        }
        return 0;
    }

    private static int UpdateTShock(string[] args)
    {
        var manager = Path.GetFullPath(Option(args, "--manager") ?? ManagerConfig.BaseDir);
        var package = Option(args, "--package");
        var apply = HasFlag(args, "--apply");
        if (string.IsNullOrWhiteSpace(package) || !File.Exists(package))
            return Fail("update-tshock 需要 --package <zip>。");
        if (!apply && Process.GetProcessesByName("TShock.Server").Length > 0)
            return Fail("检测到 TShock.Server 正在运行；先由 TSM 停止服务器，再加 --apply。");

        var stage = Path.Combine(Path.GetTempPath(), "pgame-tshock-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            ZipFile.ExtractToDirectory(package, stage, true);
            var server = Directory.EnumerateFiles(stage, "TShock.Server.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (server == null) return Fail("压缩包中缺少 TShock.Server.exe。");
            var sourceRoot = Path.GetDirectoryName(server)!;
            var payload = Directory.GetFiles(sourceRoot)
                .Where(f => Path.GetFileName(f) is "TShock.Server.exe" or "TShockAPI.dll" or "GeoIP.dat" ||
                            Path.GetExtension(f).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var backup = Path.Combine(manager, "Backups", "tshock-update-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            foreach (var source in payload)
            {
                var relative = Path.GetRelativePath(sourceRoot, source);
                var target = Path.Combine(manager, relative);
                Console.WriteLine((apply ? "[替换] " : "[预览] ") + target);
                if (!apply) continue;
                if (File.Exists(target))
                {
                    var backupTarget = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupTarget)!);
                    File.Copy(target, backupTarget, true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target + ".pgame-new", true);
                File.Move(target + ".pgame-new", target, true);
            }
            if (apply)
            {
                var localized = FirstExisting(
                    Path.Combine(manager, "maintenance", "runtime", "TShockAPI.dll"),
                    Path.Combine(manager, "Tools", "runtime", "TShockAPI.dll"));
                if (localized != null)
                {
                    var pluginDir = Path.Combine(manager, "ServerPlugins");
                    Directory.CreateDirectory(pluginDir);
                    File.Copy(localized, Path.Combine(pluginDir, "TShockAPI.dll"), true);
                }
            }
            Console.WriteLine(apply ? "TShock 运行时已替换；请运行 --selfcheck 并按服启动验证。" : "预览完成；确认停机后加 --apply。");
            return 0;
        }
        finally
        {
            try { Directory.Delete(stage, true); } catch { }
        }
    }

    private static int Housekeeping()
    {
        Console.WriteLine(ManagerHousekeeping.RunOnce());
        return 0;
    }

    private static async Task PrepareRuntime(string repoRoot, string coreDir)
    {
        Directory.CreateDirectory(coreDir);
        var temp = Path.Combine(Path.GetTempPath(), "pgame-tshock-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/Pryaxis/TShock/releases/latest");
            request.Headers.UserAgent.ParseAdd("PGame-TSManager");
            using var response = Http.Send(request);
            response.EnsureSuccessStatusCode();
            var release = JObject.Parse(await response.Content.ReadAsStringAsync());
            var asset = (release["assets"] as JArray)?.OfType<JObject>()
                .FirstOrDefault(a => Regex.IsMatch(a["name"]?.ToString() ?? "", @"win-x64.*Release\.zip$"));
            if (asset == null) throw new InvalidOperationException("未找到 TShock Windows x64 发布资产。");
            var archive = Path.Combine(temp, asset["name"]!.ToString());
            using var download = await Http.GetAsync(asset["browser_download_url"]!.ToString());
            download.EnsureSuccessStatusCode();
            await using (var stream = File.Create(archive))
                await download.Content.CopyToAsync(stream);
            var extract = Path.Combine(temp, "extract");
            ZipFile.ExtractToDirectory(archive, extract, true);
            var server = Directory.EnumerateFiles(extract, "TShock.Server.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (server == null) throw new InvalidOperationException("TShock 包中缺少 TShock.Server.exe。");
            var runtimeRoot = Path.GetDirectoryName(server)!;
            foreach (var entry in Directory.GetFileSystemEntries(runtimeRoot))
            {
                if (Path.GetFileName(entry).Equals("TShock.Installer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var destination = Path.Combine(coreDir, Path.GetFileName(entry));
                if (Directory.Exists(entry)) CopyDirectory(entry, destination, true);
                else File.Copy(entry, destination, true);
            }
            var pluginDir = Path.Combine(coreDir, "ServerPlugins");
            Directory.CreateDirectory(pluginDir);
            var api = FirstExisting(
                Path.Combine(repoRoot, "maintenance", "runtime", "TShockAPI.dll"),
                Path.Combine(repoRoot, "maintenance", "tools", "runtime", "TShockAPI.dll"));
            var hotReload = FirstExisting(
                Path.Combine(repoRoot, "maintenance", "runtime", "HotReload.dll"),
                Path.Combine(repoRoot, "maintenance", "tools", "runtime", "HotReload.dll"));
            if (api == null || hotReload == null)
                throw new FileNotFoundException("缺少项目汉化 TShockAPI.dll 或 HotReload.dll。");
            File.Copy(api, Path.Combine(pluginDir, "TShockAPI.dll"), true);
            File.Copy(hotReload, Path.Combine(pluginDir, "HotReload.dll"), true);
            Console.WriteLine("TShock Core 运行时已准备：" + coreDir);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void BuildTemplate(string repoRoot, string managerDir, string coreDir, string outputZip,
        string version, string commit, bool universal)
    {
        var stage = Path.Combine(Path.GetTempPath(), "pgame-tsmgr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            CopyDirectory(managerDir, stage, true);
            if (Directory.Exists(coreDir)) CopyDirectory(coreDir, Path.Combine(stage, "Core"), true);
            foreach (var relative in new[] { "README.md", "maintenance/templates" })
            {
                var source = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(source)) File.Copy(source, Path.Combine(stage, Path.GetFileName(source)), true);
                else if (Directory.Exists(source))
                    CopyDirectory(source, Path.Combine(stage, "Tools", "templates"), true);
            }
            var usageTemplate = Path.Combine(repoRoot, "maintenance", "templates", "package-usage.zh-CN.txt");
            if (File.Exists(usageTemplate))
            {
                var usage = File.ReadAllText(usageTemplate)
                    .Replace("{{VERSION}}", version)
                    .Replace("{{COMMIT}}", commit)
                    .Replace("{{RUN_NUMBER}}", "local-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                File.WriteAllText(Path.Combine(stage, "使用说明.txt"), usage, Utf8NoBom);
                File.WriteAllText(Path.Combine(stage, "package-usage.zh-CN.txt"), usage, Utf8NoBom);
            }
            var config = new JObject
            {
                ["启动前备份"] = true,
                ["备份目录"] = "Core\\Backups",
                ["备份保留数"] = 10,
                ["服务器目录"] = "Servers\\Profiles",
                ["插件总库"] = "Plugins",
                ["运行沙箱目录"] = "Core\\_runtime",
                ["运行时母本目录"] = "Core",
                ["世界目录"] = "Servers\\Worlds",
                ["日志目录"] = "Core\\Logs",
                ["数据目录"] = "Core\\Data",
                ["工具目录"] = "Tools",
                ["顺序启动"] = true,
                ["看门狗开关"] = true,
                ["告警开关"] = false,
                ["服务器列表"] = new JArray()
            };
            WriteJson(Path.Combine(stage, "config.json"), config);
            if (universal)
            {
                var docs = Path.Combine(repoRoot, "maintenance");
                if (Directory.Exists(docs))
                    CopyDirectoryExcept(docs, Path.Combine(stage, "Tools", "maintenance"), true,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "staging", "runtime" });
            }
            AddTemplates(new[] { "--root", stage });
            var scripts = ScriptPolicy.FindScripts(stage, false);
            if (scripts.Count > 0)
                throw new InvalidOperationException("模板包中发现脚本：" + string.Join(", ", scripts));
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(stage, outputZip, CompressionLevel.Optimal, false);
            Console.WriteLine("已生成：" + outputZip);
        }
        finally
        {
            try { Directory.Delete(stage, true); } catch { }
        }
    }

    private static void BuildSource(string repoRoot, string outputZip)
    {
        var stage = Path.Combine(Path.GetTempPath(), "pgame-tsmgr-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", ".vs", "bin", "obj", "artifacts", "_runtime", "Worlds", "Plugins",
                "ServerPlugins", "Backups", "Logs", "1.PigeonServers"
            };
            foreach (var file in Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoRoot, file);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Any(excluded.Contains)) continue;
                var extension = Path.GetExtension(file);
                if (extension is ".dll" or ".exe" or ".pdb" or ".wld" or ".db" or ".sqlite" or ".zip") continue;
                var destination = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, true);
            }
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(stage, outputZip, CompressionLevel.Optimal, false);
            Console.WriteLine("已生成：" + outputZip);
        }
        finally
        {
            try { Directory.Delete(stage, true); } catch { }
        }
    }

    private static void CopyRuntimePlugins(string root, string plugins)
    {
        foreach (var name in new[] { "TShockAPI.dll", "HotReload.dll" })
        {
            var source = FirstExisting(
                Path.Combine(root, "Core", "ServerPlugins", name),
                Path.Combine(root, "Plugins", name),
                Path.Combine(root, "Core", name),
                Path.Combine(root, "maintenance", "runtime", name),
                Path.Combine(root, "Tools", "runtime", name),
                Path.Combine(AppContext.BaseDirectory, "maintenance", "runtime", name));
            if (source != null) File.Copy(source, Path.Combine(plugins, name), true);
        }
    }

    private static Dictionary<string, string> LoadEnglishMapping(string? path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return result;
        var root = ReadObject(path);
        if (root["执行列表"] is not JArray list) return result;
        foreach (var entry in list.OfType<JObject>())
        {
            if (entry["翻译列表"] is not JObject translations) continue;
            foreach (var property in translations.Properties())
            {
                if (property.Name.Contains("SscSettings", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("TokenData", StringComparison.OrdinalIgnoreCase))
                    continue;
                var english = property.Name.Split('.').LastOrDefault() ?? property.Name;
                result[english] = property.Value.ToString();
            }
        }
        return result;
    }

    private static List<string> AllPropertyNames(JToken token)
    {
        var names = new List<string>();
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                names.Add(property.Name);
                names.AddRange(AllPropertyNames(property.Value));
            }
        }
        else if (token is JArray array)
        {
            foreach (var child in array) names.AddRange(AllPropertyNames(child));
        }
        return names;
    }

    private static bool ContainsCjk(string value) => Regex.IsMatch(value, @"[\u4e00-\u9fff]");

    private static JObject ReadObject(string path)
        => JObject.Parse(File.ReadAllText(path, Encoding.UTF8));

    private static void WriteJson(string path, JToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, token.ToString(Formatting.Indented), Utf8NoBom);
    }

    private static void EnsureWhitelist(string path)
    {
        if (File.Exists(path) && File.ReadAllLines(path).Contains("127.0.0.1")) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        if (existing.Length > 0 && !existing.EndsWith("\n", StringComparison.Ordinal)) existing += "\r\n";
        File.WriteAllText(path, existing + "# Localhost\r\n127.0.0.1\r\n", Utf8NoBom);
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }

    private static void CopyDirectoryExcept(string source, string destination, bool overwrite,
        HashSet<string> excludedDirectories)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, overwrite);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (excludedDirectories.Contains(name)) continue;
            CopyDirectoryExcept(directory, Path.Combine(destination, name), overwrite, excludedDirectories);
        }
    }

    private static void MoveInto(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        if (Directory.Exists(source))
        {
            if (!Directory.Exists(destination)) Directory.Move(source, destination);
            else
            {
                foreach (var entry in Directory.GetFileSystemEntries(source))
                    MoveInto(entry, Path.Combine(destination, Path.GetFileName(entry)));
                if (!Directory.EnumerateFileSystemEntries(source).Any()) Directory.Delete(source);
            }
        }
        else if (File.Exists(source))
        {
            File.Move(source, destination, true);
        }
    }

    private static string ReadVersion(string project)
    {
        var match = Regex.Match(File.ReadAllText(project), @"<Version>([^<]+)</Version>");
        return match.Success ? match.Groups[1].Value : "0.0.0";
    }

    private static int RunProcess(string file, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动：" + file);
        process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string RunCapture(string file, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动：" + file);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static void AddGitHubHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.UserAgent.ParseAdd("PGame-TSManager");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PGame-TSManager");
        return client;
    }

    private static string? FirstExisting(params string?[] candidates)
        => candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

    private static string? Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, a =>
            string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length) return args[index + 1];
        var prefix = name + "=";
        return args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
    }

    private static int? OptionInt(string[] args, string name)
        => int.TryParse(Option(args, name), out var value) ? value : null;

    private static bool HasFlag(string[] args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? Positional(string[] args, int index)
        => args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ElementAtOrDefault(index);

    private static int OrdinalOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var match = Regex.Match(name.Trim(), @"^(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static int Fail(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        return 1;
    }
}
