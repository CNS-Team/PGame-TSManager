using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PGameTSManager
{
    /// <summary>
    /// 每台服务器的 TSM 配置（沿用旧版 TSManager 的 ServerConfig 字段）：
    /// 世界、语言、端口、人数、密码、额外参数，以及该服要加载的插件清单。
    /// 对应 1.PigeonServers\&lt;服&gt;\config.json。
    /// </summary>
    public class ServerManifest
    {
        [JsonProperty("服务器名称")] public string? Name { get; set; }
        [JsonProperty("启用")] public bool Enabled { get; set; } = true;

        /// <summary>世界文件名（相对 Worlds\，可不带 .wld）。</summary>
        [JsonProperty("世界")] public string? World { get; set; }
        /// <summary>世界不存在时用于 -autocreate 的尺寸；空则读取运行目录 server.properties 的 autocreate。</summary>
        [JsonProperty("自动建图")] public int? AutoCreate { get; set; }
        [JsonProperty("语言")] public int Language { get; set; } = 7;      // 7 = 中文
        [JsonProperty("端口")] public ushort Port { get; set; }
        [JsonProperty("REST端口")] public int RestPort { get; set; }
        [JsonProperty("最大玩家")] public int MaxPlayers { get; set; } = 252;
        [JsonProperty("IP")] public string Ip { get; set; } = "0.0.0.0";
        [JsonProperty("密码")] public string Password { get; set; } = string.Empty;

        /// <summary>额外启动参数（原样追加）。</summary>
        [JsonProperty("启动参数")] public string? Parameters { get; set; }

        /// <summary>该服要加载的插件文件名（相对 Plugins\ 总库，含 TShockAPI.dll）。</summary>
        [JsonProperty("插件")] public List<string>? Plugins { get; set; }

        [JsonProperty("备注")] public string? Remark { get; set; }

        [JsonProperty("插件总库")] public string? PluginLibrary { get; set; }
        [JsonProperty("覆盖插件目录")] public bool? PrunePlugins { get; set; }
    }

    /// <summary>单个受管服务器：指向 1.PigeonServers\&lt;服&gt;（只放 config.json + tshock\）。</summary>
    public class ServerProfile
    {
        [JsonProperty("服务器名称")] public string name = string.Empty;
        /// <summary>稳定机器标识；外部项目只用 id 映射服务器，不使用中文名。</summary>
        [JsonProperty("标识")] public string id = string.Empty;
        [JsonProperty("目录")] public string rootPath = string.Empty;
        [JsonProperty("可执行文件")] public string executable = "TShock.Server.exe";
        [JsonProperty("启动参数")] public string arguments = string.Empty;
        [JsonProperty("启用")] public bool enabled = true;
        [JsonProperty("备注")] public string remark = string.Empty;
        [JsonProperty("插件")] public List<string> plugins = new();
        [JsonProperty("插件总库")] public string pluginLibrary = string.Empty;

        public const string ManifestFileName = "config.json";

        [JsonIgnore]
        public bool IsProfileMode => !string.IsNullOrWhiteSpace(rootPath);

        [JsonIgnore]
        public string ResolvedRootPath => Path.GetFullPath(
            Path.IsPathRooted(rootPath) ? rootPath : Path.Combine(ManagerConfig.BaseDir, rootPath));

        [JsonIgnore]
        public string StableId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
                var source = (name ?? "").Trim();
                if (source.Length == 0) source = Path.GetFileName(ResolvedRootPath.TrimEnd('\\', '/'));
                var ordinal = ManagerConfig.OrdinalOf(source);
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source.ToLowerInvariant()));
                var suffix = Convert.ToHexString(hash).Substring(0, 8).ToLowerInvariant();
                return $"server-{ordinal}-{suffix}";
            }
        }

        public ServerManifest? LoadManifest()
        {
            if (!IsProfileMode) return null;
            var path = Path.Combine(ResolvedRootPath, ManifestFileName);
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<ServerManifest>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public string ResolveArguments(out ServerManifest? manifest)
        {
            manifest = LoadManifest();
            if (!string.IsNullOrWhiteSpace(arguments)) return arguments;
            return manifest?.Parameters ?? string.Empty;
        }

        public List<string> ResolvePlugins(ServerManifest? manifest)
        {
            if (plugins != null && plugins.Count > 0) return plugins;
            return manifest?.Plugins ?? new List<string>();
        }
    }

    public class ExternalProcessProfile
    {
        [JsonProperty("类型")] public string kind { get; set; } = "external-process";
        [JsonProperty("标识")] public string id { get; set; } = "";
        [JsonProperty("名称")] public string name { get; set; } = "";
        [JsonProperty("启用")] public bool enabled { get; set; } = true;
        [JsonProperty("工作目录")] public string workdir { get; set; } = "";
        [JsonProperty("可执行文件")] public string exe { get; set; } = "";
        [JsonProperty("进程名")] public string processName { get; set; } = "";
        [JsonProperty("启动参数")] public List<string> args { get; set; } = new();
        [JsonProperty("停止参数")] public List<string> stopArgs { get; set; } = new();
        [JsonProperty("健康检查方式")] public string healthMode { get; set; } = "process";
        [JsonProperty("健康端口")] public List<int> healthPorts { get; set; } = new();
        [JsonProperty("健康地址")] public string healthUrl { get; set; } = "";
        [JsonProperty("日志目录")] public string logDir { get; set; } = "";
        [JsonProperty("日志活跃判定秒")] public int logActiveSeconds { get; set; } = 300;
        [JsonProperty("看门狗")] public string watchdog { get; set; } = "none";
        [JsonProperty("自动重启")] public bool autoRestart { get; set; }
    }

    public class ManagerConfig
    {
        [JsonIgnore]
        public static string BaseDir => AppContext.BaseDirectory;

        /// <summary>命令行 --nowindow 时临时覆盖 showServerWindow（仅调试用）。</summary>
        [JsonIgnore]
        public static bool? ShowWindowOverride = null;

        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return BaseDir;
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(BaseDir, path));
        }

        // —— 旧版 TSManager 目录约定 ——
        /// <summary>世界文件目录（共享），相对程序目录。</summary>
        [JsonProperty("世界目录")] public string worldDir = "Servers\\Worlds";
        /// <summary>插件总库（共享），相对程序目录。</summary>
        [JsonProperty("插件总库")] public string pluginDir = "Plugins";
        /// <summary>各服运行沙箱目录（自动生成），相对程序目录。</summary>
        [JsonProperty("运行沙箱目录")] public string runtimeDir = "Core\\_runtime";
        [JsonProperty("服务器目录")] public string serverDir = "Servers\\Profiles";
        [JsonProperty("清单文件名")] public string configFile = "config.json";
        [JsonProperty("服务端可执行文件")] public string serverExecutable = "TShock.Server.exe";

        // —— 运行选项 ——
        [JsonProperty("启动前备份")] public bool backupBeforeStart = true;
        [JsonProperty("备份目录")] public string backupDir = "Core\\Backups";
        [JsonProperty("日志目录")] public string logDir = "Core\\Logs";
        [JsonProperty("数据目录")] public string dataDir = "Core\\Data";
        [JsonProperty("工具目录")] public string toolsDir = "Tools";
        [JsonProperty("备份保留数")] public int backupKeep = 10;

        /// <summary>
        /// 管理器**主程序**换版备份 `PGame-TSManager.exe.bak*` 的保留份数（默认 2）。
        /// ⚠ 与 <see cref="backupKeep"/> 不是一回事：那个管世界/配置备份（Core\Backups），这个只管主 exe 的换版备份。
        /// 主 exe 自包含单文件约 **147 MB/份**，无上限时一天能堆到几百 MB（实测 3 份 = 442MB）。
        /// </summary>
        [JsonProperty("主程序备份保留数")] public int exeBackupKeep = 2;

        /// <summary>
        /// 日志保留天数（默认 30）：超过这个天数的 `Core\Logs\*.log` 会被清理。
        /// 只删日志，不碰世界/备份（见 <see cref="backupKeep"/> 与 `Servers\Worlds\*.wld*`）。
        /// </summary>
        [JsonProperty("日志保留天数")] public int logRetentionDays = 30;

        /// <summary>
        /// 陈旧残留清理（默认开）：只清**可证明冗余**的两类 ——
        /// ① 沙箱 `ServerPlugins.disabled\*.dll` 中「插件总库还有同名」的停用副本（PluginSync 随时可恢复）；
        /// ② 单文件自包含部署下，程序目录里另一次发布模式残留的 `PGame-TSManager.dll/.deps.json/.runtimeconfig.json`。
        /// 总库没有同名的停用插件、以及非单文件部署，一律不删（详见 ManagerHousekeeping）。
        /// </summary>
        [JsonProperty("清理陈旧残留")] public bool cleanupStaleArtifacts = false; // 2026-10-15 起默认关闭（本舰队运维模型精简口径）
        [JsonProperty("服务器列表")] public List<ServerProfile> serverProfiles = new();
        [JsonProperty("外部进程")] public List<ExternalProcessProfile> externalProcesses = new();

        /// <summary>运行时母本目录；留空 = 程序目录（exe/bin/i18n/runtimes/x64/GeoIP.dat 所在处）。</summary>
        [JsonProperty("运行时母本目录")] public string sharedRuntimeDir = "Core";
        /// <summary>启动前按各服 config.json 从插件总库同步插件。</summary>
        [JsonProperty("启动前同步插件")] public bool syncPluginsOnStart = false; // 2026-10-15 起默认关闭：改插件=scp+reload/重启，无启动时自动搬运
        /// <summary>未列出的插件移入 ServerPlugins.disabled（不删除）。</summary>
        [JsonProperty("停用未列出插件")] public bool prunePlugins = false; // 2026-10-15 起默认关闭：不再自动搬 ServerPlugins.disabled
        /// <summary>被停用插件的子目录名。</summary>
        [JsonProperty("停用插件目录名")] public string disabledPluginDir = "ServerPlugins.disabled";
        /// <summary>true = 每台服务器另开独立可见控制台窗口；false（默认）= 在管理器内运行。</summary>
        [JsonProperty("每服独立窗口")] public bool showServerWindow = false;

        // —— 空服内存压缩：不关端口，只压缩工作集，玩家首次进入不受影响 ——
        /// <summary>是否在服务器无人时自动压缩工作集。</summary>
        [JsonProperty("空闲内存整理")] public bool idleMemoryTrimEnabled = true;
        /// <summary>连续无人多少分钟后执行第一次压缩。</summary>
        [JsonProperty("内存整理触发分钟")] public int idleMemoryTrimMinutes = 15;
        /// <summary>两次压缩之间的最小间隔分钟数。</summary>
        [JsonProperty("内存整理冷却分钟")] public int idleMemoryTrimCooldownMinutes = 30;
        /// <summary>工作集低于该值时不再压缩，避免无意义操作。</summary>
        [JsonProperty("内存整理最小工作集MB")] public int idleMemoryTrimMinWorkingSetMB = 256;

        /// <summary>
        /// 容量采样：每隔 capacitySampleSeconds 秒，把每服的 (人数, CPU%, 工作集, 私有) 追加到
        /// Core\Data\capacity-samples.csv —— 用于拟合「人数 × 单核占用」曲线。
        /// </summary>
        [JsonProperty("容量采样")] public bool capacitySampleEnabled = true;
        [JsonProperty("容量采样间隔秒")] public int capacitySampleSeconds = 60;

        /// <summary>
        /// 空服周期保存：TShock 自己的自动保存**只在有人在线时**才跑（实测：有人≈每 10 分钟；
        /// 0 人服完全没有自动存档）。但 0 人时插件仍会改世界（例：TileHelper 在世界加载后贴
        /// 出生点.sec3）→ 崩溃/断电后这些改动会丢。这里每隔 periodicSaveMinutes 分钟，
        /// 对「正在运行且当前 0 人在线」的服发一次 /save 兜底。
        /// </summary>
        [JsonProperty("周期保存")] public bool periodicSaveEnabled = true;
        [JsonProperty("周期保存间隔分钟")] public int periodicSaveMinutes = 180; // 2026-10-15 起默认 180（3 小时/次，仅空服补存）
        /// <summary>true = 只保存 0 人在线的服（有人在线时交给 TShock 自己的自动保存）。</summary>
        [JsonProperty("仅空服周期保存")] public bool periodicSaveOnlyWhenEmpty = true;
        /// <summary>
        /// 最后一名玩家离线时立即补存一次（默认开）。
        /// TShock 的自动保存只在有人在线时按 10 分钟跑，人一走就停 ——
        /// 「玩家刚走 → 下一个人进来之前」这段时间的世界改动否则不会落盘。
        /// </summary>
        [JsonProperty("离场保存")] public bool saveOnLastPlayerLeave = false; // 2026-10-15 起默认关闭（对齐本舰队 10-13 起的口径）

        // —— 本地控制 API（默认关闭；个人版 overlay 才开启） ——
        [JsonProperty("控制API开关")] public bool controlApiEnabled = false;
        [JsonProperty("控制API地址")] public string controlApiHost = "127.0.0.1";
        [JsonProperty("控制API端口")] public int controlApiPort = 8765;
        [JsonProperty("控制API令牌")] public string controlApiToken = "";
        [JsonProperty("控制API事件缓冲")] public int controlApiEventBuffer = 512;

        /// <summary>
        /// 「全部启动」是否按顺序逐台启动：上一台真正就绪（端口在监听）后再起下一台。
        /// 三台同时读世界会互相抢 CPU / 磁盘，表现就是界面卡很久才起来。
        /// </summary>
        [JsonProperty("顺序启动")] public bool startAllSequential = true;

        // —— 看门狗：服务器异常退出后自动拉起 ——
        /// <summary>是否开启看门狗（异常退出自动重启）。</summary>
        [JsonProperty("看门狗开关")] public bool watchdogEnabled = true;
        /// <summary>连续自动重启次数上限，超过就放弃并告警（防止无限崩溃循环）。</summary>
        [JsonProperty("看门狗最大重启次数")] public int watchdogMaxRestarts = 3;
        /// <summary>自动重启前等待秒数（给端口/文件句柄一点释放时间）。</summary>
        [JsonProperty("看门狗重启延迟秒")] public int watchdogRestartDelaySeconds = 8;
        /// <summary>运行超过这个秒数算「稳定」，重启计数清零（偶尔崩一次不会累积到上限）。</summary>
        [JsonProperty("看门狗稳定判定秒")] public int watchdogStableSeconds = 300;
        /// <summary>端口/REST 连续健康检查失败多少次后判定为死服并重启。</summary>
        [JsonProperty("看门狗健康失败阈值")] public int watchdogHealthFailureThreshold = 3;
        /// <summary>启动后的看门狗保护期，避免世界加载期间误判。</summary>
        [JsonProperty("看门狗启动宽限秒")] public int watchdogStartupGraceSeconds = 30;
        /// <summary>端口持续异常多少秒后才允许判定死服，避免热重载或换图时短暂断流误判。</summary>
        [JsonProperty("看门狗不健康判定秒")] public int watchdogUnhealthySeconds = 120;
        /// <summary>执行 /hr、/reload、/world 等维护命令后，暂停健康检查的秒数。</summary>
        [JsonProperty("看门狗维护抑制秒")] public int watchdogMaintenanceSuppressSeconds = 180;
        /// <summary>世界缺失触发自动建图时，看门狗保护窗口秒数；小/中/大图另有最低值。</summary>
        [JsonProperty("看门狗建图抑制秒")] public int watchdogWorldBuildSuppressSeconds = 600;
        /// <summary>是否把连续致命日志异常作为死服重启依据。</summary>
        [JsonProperty("看门狗日志报错重启")] public bool watchdogLogErrorRestartEnabled = true;
        /// <summary>窗口期内出现多少条致命日志后触发重启。</summary>
        [JsonProperty("日志报错阈值")] public int watchdogLogErrorThreshold = 3;
        /// <summary>致命日志统计窗口秒数。</summary>
        [JsonProperty("日志报错窗口秒")] public int watchdogLogErrorWindowSeconds = 60;

        // —— 告警：异常/掉线时由管理器进程内直接 POST Webhook ——
        // 不调用 PowerShell、bat 或 cmd；地址与群号留空 = 告警关闭。
        /// <summary>是否启用告警。</summary>
        [JsonProperty("告警开关")] public bool alertEnabled = false;
        /// <summary>告警 Webhook 地址；留空 = 不告警。</summary>
        [JsonProperty("告警Webhook")] public string alertWebhookUrl = "";
        /// <summary>兼容旧配置：单个上报群号。</summary>
        [JsonProperty("告警群号_旧")] public long alertGroupId = 0;
        /// <summary>上报到的群号列表；未配置时回退到 alertGroupId。</summary>
        [JsonProperty("告警群号列表")] public List<long> alertGroupIds = new();
        /// <summary>同类告警最小间隔秒数（防刷屏）。</summary>
        [JsonProperty("告警最小间隔秒")] public int alertMinIntervalSeconds = 60;
        /// <summary>顺序启动时，单台服务器等待「就绪」的最长秒数（超时就跳过，继续下一台）。</summary>
        [JsonProperty("就绪超时秒")] public int startReadyTimeoutSeconds = 240;

        // ————————————————————————————————————————————————————————————————
        // 旧版英文键 → 中文键 迁移（2026-10-12 汉化）
        //   为什么需要：老部署的 config.json 是英文键；改了 [JsonProperty] 之后若不做迁移，
        //   反序列化会全部落默认值 —— 尤其是 控制API令牌 会丢（Control API / Bot / FixTools 全依赖它）。
        //   策略：加载时先把旧键名改写为中文，再反序列化；改写成功就把原文件备份成
        //   config.json.bak-<时间戳> 并把新格式写回。**幂等**：已是中文键则原样通过。
        // ————————————————————————————————————————————————————————————————
        private static readonly Dictionary<string, string> LegacyManagerKeys = new(StringComparer.Ordinal)
        {
            ["worldDir"] = "世界目录",
            ["pluginDir"] = "插件总库",
            ["runtimeDir"] = "运行沙箱目录",
            ["serverDir"] = "服务器目录",
            ["configFile"] = "清单文件名",
            ["serverExecutable"] = "服务端可执行文件",
            ["backupBeforeStart"] = "启动前备份",
            ["backupDir"] = "备份目录",
            ["logDir"] = "日志目录",
            ["dataDir"] = "数据目录",
            ["toolsDir"] = "工具目录",
            ["backupKeep"] = "备份保留数",
            ["exeBackupKeep"] = "主程序备份保留数",
            ["logRetentionDays"] = "日志保留天数",
            ["cleanupStaleArtifacts"] = "清理陈旧残留",
            ["serverProfiles"] = "服务器列表",
            ["externalProcesses"] = "外部进程",
            ["sharedRuntimeDir"] = "运行时母本目录",
            ["syncPluginsOnStart"] = "启动前同步插件",
            ["prunePlugins"] = "停用未列出插件",
            ["disabledPluginDir"] = "停用插件目录名",
            ["showServerWindow"] = "每服独立窗口",
            ["idleMemoryTrimEnabled"] = "空闲内存整理",
            ["idleMemoryTrimMinutes"] = "内存整理触发分钟",
            ["idleMemoryTrimCooldownMinutes"] = "内存整理冷却分钟",
            ["idleMemoryTrimMinWorkingSetMB"] = "内存整理最小工作集MB",
            ["capacitySampleEnabled"] = "容量采样",
            ["capacitySampleSeconds"] = "容量采样间隔秒",
            ["periodicSaveEnabled"] = "周期保存",
            ["periodicSaveMinutes"] = "周期保存间隔分钟",
            ["periodicSaveOnlyWhenEmpty"] = "仅空服周期保存",
            ["saveOnLastPlayerLeave"] = "离场保存",
            ["controlApiEnabled"] = "控制API开关",
            ["controlApiHost"] = "控制API地址",
            ["controlApiPort"] = "控制API端口",
            ["controlApiToken"] = "控制API令牌",
            ["controlApiEventBuffer"] = "控制API事件缓冲",
            ["startAllSequential"] = "顺序启动",
            ["startReadyTimeoutSeconds"] = "就绪超时秒",
            ["watchdogEnabled"] = "看门狗开关",
            ["watchdogMaxRestarts"] = "看门狗最大重启次数",
            ["watchdogRestartDelaySeconds"] = "看门狗重启延迟秒",
            ["watchdogStableSeconds"] = "看门狗稳定判定秒",
            ["watchdogHealthFailureThreshold"] = "看门狗健康失败阈值",
            ["watchdogStartupGraceSeconds"] = "看门狗启动宽限秒",
            ["watchdogUnhealthySeconds"] = "看门狗不健康判定秒",
            ["watchdogMaintenanceSuppressSeconds"] = "看门狗维护抑制秒",
            ["watchdogWorldBuildSuppressSeconds"] = "看门狗建图抑制秒",
            ["watchdogLogErrorRestartEnabled"] = "看门狗日志报错重启",
            ["watchdogLogErrorThreshold"] = "日志报错阈值",
            ["watchdogLogErrorWindowSeconds"] = "日志报错窗口秒",
            ["alertEnabled"] = "告警开关",
            ["alertWebhookUrl"] = "告警Webhook",
            ["alertGroupId"] = "告警群号_旧",
            ["alertGroupIds"] = "告警群号列表",
            ["alertMinIntervalSeconds"] = "告警最小间隔秒"
        };

        /// <summary>服务器列表[] 里每条的老键名。</summary>
        private static readonly Dictionary<string, string> LegacyProfileKeys = new(StringComparer.Ordinal)
        {
            ["name"] = "服务器名称",
            ["id"] = "标识",
            ["rootPath"] = "目录",
            ["executable"] = "可执行文件",
            ["arguments"] = "启动参数",
            ["enabled"] = "启用",
            ["remark"] = "备注",
            ["plugins"] = "插件",
            ["pluginLibrary"] = "插件总库"
        };

        /// <summary>外部进程[] 里每条的老键名。</summary>
        private static readonly Dictionary<string, string> LegacyExternalKeys = new(StringComparer.Ordinal)
        {
            ["kind"] = "类型",
            ["id"] = "标识",
            ["name"] = "名称",
            ["enabled"] = "启用",
            ["workdir"] = "工作目录",
            ["exe"] = "可执行文件",
            ["processName"] = "进程名",
            ["args"] = "启动参数",
            ["stopArgs"] = "停止参数",
            ["healthMode"] = "健康检查方式",
            ["healthPorts"] = "健康端口",
            ["healthUrl"] = "健康地址",
            ["logDir"] = "日志目录",
            ["logActiveSeconds"] = "日志活跃判定秒",
            ["watchdog"] = "看门狗",
            ["autoRestart"] = "自动重启"
        };

        /// <summary>
        /// 把旧版英文键名的 config.json 文本改写成中文键名；已是中文（或无法解析）时原样返回。
        /// </summary>
        public static string MigrateLegacyKeys(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch { return json; }

            var changed = false;
            var result = new JObject();
            foreach (var prop in root.Properties().ToList())
            {
                var name = prop.Name;
                if (LegacyManagerKeys.TryGetValue(name, out var cn)) { name = cn; changed = true; }

                if (name == "服务器列表" && prop.Value is JArray profiles)
                    foreach (var item in profiles.OfType<JObject>().ToList()) RenameKeys(item, LegacyProfileKeys, ref changed);
                else if (name == "外部进程" && prop.Value is JArray externals)
                    foreach (var item in externals.OfType<JObject>().ToList()) RenameKeys(item, LegacyExternalKeys, ref changed);

                result[name] = prop.Value;
            }
            return changed ? result.ToString(Formatting.Indented) : json;
        }

        private static void RenameKeys(JObject obj, Dictionary<string, string> map, ref bool changed)
        {
            foreach (var p in obj.Properties().ToList())
            {
                if (!map.TryGetValue(p.Name, out var cn)) continue;
                var value = p.Value;
                p.Remove();
                obj[cn] = value;
                changed = true;
            }
        }

        private static ManagerConfig? _instance;
        public static ManagerConfig Instance => _instance ??= LoadConfig() ?? new ManagerConfig();

        private static string ConfigFilePath => Path.Combine(BaseDir, "config.json");

        private static ManagerConfig? LoadConfig()
        {
            var path = ConfigFilePath;
            if (File.Exists(path))
            {
                try
                {
                    var raw = File.ReadAllText(path);
                    var migrated = MigrateLegacyKeys(raw);      // 旧英文键 → 中文键（幂等）
                    var cfg = JsonConvert.DeserializeObject<ManagerConfig>(migrated);
                    if (cfg == null) return new ManagerConfig();

                    // 需要落盘的两种情况：
                    //   ① 键名刚从英文迁移过来；
                    //   ② 磁盘上的键集合 ≠ 当前版本的键集合（缺新字段 / 有已删字段）——
                    //      补全后 config.json 就是"当前版本全部可调项"的中文清单，用户能直接看见每个开关。
                    var needRewrite = false;
                    var canonical = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                    try
                    {
                        if (!string.Equals(migrated, raw, StringComparison.Ordinal)) needRewrite = true;
                        else
                        {
                            var diskKeys = JObject.Parse(raw).Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                            var mineKeys = JObject.Parse(canonical).Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                            if (!diskKeys.SetEquals(mineKeys)) needRewrite = true;
                        }
                    }
                    catch { }

                    if (needRewrite)
                    {
                        try { File.Copy(path, path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
                        try { File.WriteAllText(path, canonical); } catch { }   // 全量中文键（含当前版本所有字段）
                    }
                    return cfg;
                }
                catch { return new ManagerConfig(); }
            }
            var res = new ManagerConfig();
            res.Save();
            return res;
        }

        public void Save()
        {
            try { File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch { }
        }

        public IEnumerable<ServerProfile> LoadProfiles()
        {
            List<ServerProfile> list;
            if (serverProfiles != null && serverProfiles.Count > 0)
            {
                list = serverProfiles.Where(p => p != null && p.enabled).ToList();
            }
            else
            {
                var legacyDir = Resolve(serverDir);
                list = Directory.Exists(legacyDir)
                    ? Directory.GetDirectories(legacyDir).Select(d => new ServerProfile { name = Path.GetFileName(d) }).ToList()
                    : new List<ServerProfile>();
            }

            // ★ 自动发现：1.PigeonServers 下存在、但没写进 serverProfiles 的服务器目录（例如手动新增的 4.xxx）。
            //   只要目录里有 TSM 清单 config.json 且写了端口，就当一台服务器接管 —— 新增只要建目录，不用改管理器配置。
            try
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (serverProfiles != null)
                {
                    foreach (var p in serverProfiles.Where(x => x != null))
                    {
                        var f = SafeFullPath(p!.ResolvedRootPath);
                        if (f != null) known.Add(f);
                    }
                }

                var dir = Resolve(serverDir);
                if (Directory.Exists(dir))
                {
                    foreach (var d in Directory.GetDirectories(dir))
                    {
                        // ★ 跳过备份/临时目录：曾因 0.xxx.bak-* 被自动发现 → 同一台服出现两个实例、
                        //   且备份实例抢占端口。约定：profile 备份放 Backups\，不要留在 Servers\Profiles\。
                        var leaf = Path.GetFileName(d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (leaf.Contains(".bak", StringComparison.OrdinalIgnoreCase) ||
                            leaf.Contains(".old", StringComparison.OrdinalIgnoreCase) ||
                            leaf.Contains(".tmp", StringComparison.OrdinalIgnoreCase) ||
                            leaf.StartsWith("~", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var manifestPath = Path.Combine(d, ServerProfile.ManifestFileName);
                        if (!File.Exists(manifestPath)) continue;

                        ServerManifest? mf = null;
                        try { mf = JsonConvert.DeserializeObject<ServerManifest>(File.ReadAllText(manifestPath)); } catch { }
                        if (mf == null || mf.Port == 0) continue;      // 没有端口 = 不是一台服务器

                        var full = SafeFullPath(d);
                        if (full == null || known.Contains(full)) continue;

                        list.Add(new ServerProfile
                        {
                            name = string.IsNullOrWhiteSpace(mf.Name) ? StripOrdinal(Path.GetFileName(d)) : mf.Name!,
                            rootPath = d,
                            enabled = true,
                            remark = "自动发现"
                        });
                    }
                }
            }
            catch { }

            // ★ 按「服务器目录名前面的序号」排序：1.生存 → 2.生存2 → 3.xxx
            //   新增服务器只要把目录叫 4.xxx / 5.xxx，就会自动排在后面，不用改配置里的顺序。
            return list
                .OrderBy(OrdinalKey)
                .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>排序用：优先取目录名开头的数字；没有就取显示名开头的数字；都没有排到最后。</summary>
        private static int OrdinalKey(ServerProfile p)
        {
            try
            {
                var folder = OrdinalOf(Path.GetFileName(p.ResolvedRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                if (folder > 0) return folder;
            }
            catch { }

            var byName = OrdinalOf(p.name);
            return byName > 0 ? byName : int.MaxValue;
        }

        private static string? SafeFullPath(string? path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path); }
            catch { return null; }
        }

        /// <summary>"4.生存服" → "生存服"（去掉开头序号与分隔符）。</summary>
        public static string StripOrdinal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var s = text.Trim();
            var i = 0;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 0) return s;
            var rest = s.Substring(i).TrimStart('.', '、', '-', '_', ' ');
            return rest.Length > 0 ? rest : s;
        }
        /// <summary>取名字开头的序号（"1.生存" → 1，"10.xxx" → 10）。没有序号返回 0。</summary>
        public static int OrdinalOf(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var s = text.Trim();
            var i = 0;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 0) return 0;
            return int.TryParse(s.Substring(0, i), out var n) ? n : 0;
        }

        public string ResolvePluginLibrary(ServerProfile profile, ServerManifest? manifest)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.pluginLibrary))
                return Resolve(profile.pluginLibrary);
            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.PluginLibrary))
            {
                var p = manifest.PluginLibrary;
                if (Path.IsPathRooted(p)) return Path.GetFullPath(p);
                var byBase = Path.Combine(BaseDir, p);
                if (Directory.Exists(byBase)) return Path.GetFullPath(byBase);
                return Path.GetFullPath(Path.Combine(profile!.ResolvedRootPath, p));
            }
            return Resolve(pluginDir);
        }

        /// <summary>该服的运行沙箱目录（exe/bin/ServerPlugins/server.properties/Logs 都在这里）。</summary>
        public string ResolveRuntimeDir(ServerProfile profile)
        {
            var safe = string.Join("_", profile.name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Resolve(runtimeDir), safe);
        }

        public void MakeDirectories()
        {
            foreach (var d in new[] { worldDir, pluginDir, runtimeDir, backupDir, logDir, dataDir, toolsDir, serverDir })
            {
                try { var full = Resolve(d); if (!Directory.Exists(full)) Directory.CreateDirectory(full); }
                catch { }
            }
        }
    }
}
