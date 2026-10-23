using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var options = Options.Parse(args);
var token = Environment.GetEnvironmentVariable("GH_TOKEN");
var repo = Environment.GetEnvironmentVariable("REPO");
if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(repo))
{
    Console.Error.WriteLine("GH_TOKEN and REPO are required.");
    return 2;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
http.DefaultRequestHeaders.UserAgent.ParseAdd("pgame-tsmanager-prune");

var api = $"https://api.github.com/repos/{repo}";
var summary = new Summary();
var now = DateTimeOffset.UtcNow;

await ForEachPage($"{api}/actions/artifacts", "artifacts", async item =>
{
    var createdAt = item.GetProperty("created_at").GetDateTimeOffset();
    if (!ShouldDrop(createdAt, options.ArtifactDays, now)) return;
    var size = item.GetProperty("size_in_bytes").GetInt64() / 1024d / 1024d;
    var name = item.GetProperty("name").GetString() ?? "";
    Console.WriteLine($"ARTIFACT {size,10:N2} MB  {createdAt:O}  {name}");
    summary.Artifacts++;
    summary.ArtifactMb += size;
    if (!options.DryRun)
        await Delete($"{api}/actions/artifacts/{item.GetProperty("id").GetInt64()}");
});

await ForEachPage($"{api}/actions/caches", "actions_caches", async item =>
{
    var createdAt = item.GetProperty("created_at").GetDateTimeOffset();
    if (!ShouldDrop(createdAt, options.CacheDays, now)) return;
    var size = item.GetProperty("size_in_bytes").GetInt64() / 1024d / 1024d;
    var key = item.GetProperty("key").GetString() ?? "";
    var reference = item.GetProperty("ref").GetString() ?? "";
    Console.WriteLine($"CACHE    {size,10:N2} MB  {createdAt:O}  {reference}  key={key}");
    summary.Caches++;
    summary.CacheMb += size;
    if (!options.DryRun)
        await Delete($"{api}/actions/caches/{item.GetProperty("id").GetInt64()}");
});

var releases = await ReadAllPages($"{api}/releases");
var orderedReleases = releases.OrderByDescending(r => r.GetProperty("created_at").GetDateTimeOffset()).ToArray();
foreach (var release in orderedReleases.Skip(options.ReleaseKeep))
{
    var assets = release.TryGetProperty("assets", out var assetArray) ? assetArray.GetArrayLength() : 0;
    var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
    Console.WriteLine($"RELEASE  {release.GetProperty("created_at").GetDateTimeOffset():O}  {tag}  (assets={assets})");
    summary.Releases++;
    if (!options.DryRun)
        await Delete($"{api}/releases/{release.GetProperty("id").GetInt64()}");
}

if (options.RunDays > 0)
{
    await ForEachPage($"{api}/actions/runs", "workflow_runs", async item =>
    {
        if (item.GetProperty("status").GetString() != "completed") return;
        var createdAt = item.GetProperty("created_at").GetDateTimeOffset();
        if (!ShouldDrop(createdAt, options.RunDays, now)) return;
        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var branch = item.TryGetProperty("head_branch", out var b) ? b.GetString() ?? "" : "";
        Console.WriteLine($"RUN      {createdAt:O}  #{item.GetProperty("run_number").GetInt64()}  {name}  {branch}");
        summary.Runs++;
        if (!options.DryRun)
            await Delete($"{api}/actions/runs/{item.GetProperty("id").GetInt64()}");
    });
}

Console.WriteLine();
Console.WriteLine($"汇总：artifacts={summary.Artifacts} ({summary.ArtifactMb:N2} MB) / caches={summary.Caches} ({summary.CacheMb:N2} MB) / releases={summary.Releases} / runs={summary.Runs} / dryRun={options.DryRun}");
var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
if (!string.IsNullOrWhiteSpace(summaryPath))
{
    var text = new StringBuilder()
        .AppendLine($"### Prune 汇总 (dryRun={options.DryRun})")
        .AppendLine()
        .AppendLine("| 对象 | 数量 | 体积(MB) |")
        .AppendLine("|---|---:|---:|")
        .AppendLine($"| Artifacts | {summary.Artifacts} | {summary.ArtifactMb:N2} |")
        .AppendLine($"| Caches | {summary.Caches} | {summary.CacheMb:N2} |")
        .AppendLine($"| Releases | {summary.Releases} | - |")
        .AppendLine($"| Workflow runs | {summary.Runs} | - |")
        .ToString();
    await File.AppendAllTextAsync(summaryPath, text, new UTF8Encoding(false));
}
return 0;

async Task ForEachPage(string url, string arrayName, Func<JsonElement, Task> action)
{
    var page = 1;
    while (true)
    {
        using var response = await http.GetAsync($"{url}?per_page=100&page={page}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!document.RootElement.TryGetProperty(arrayName, out var array) || array.GetArrayLength() == 0)
            return;
        foreach (var item in array.EnumerateArray()) await action(item.Clone());
        if (array.GetArrayLength() < 100) return;
        page++;
    }
}

async Task<JsonElement[]> ReadAllPages(string url)
{
    var result = new List<JsonElement>();
    var page = 1;
    while (true)
    {
        using var response = await http.GetAsync($"{url}?per_page=100&page={page}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var array = document.RootElement;
        if (array.ValueKind != JsonValueKind.Array) return result.ToArray();
        foreach (var item in array.EnumerateArray()) result.Add(item.Clone());
        if (array.GetArrayLength() < 100) return result.ToArray();
        page++;
    }
}

async Task Delete(string url)
{
    using var response = await http.DeleteAsync(url);
    if (!response.IsSuccessStatusCode)
        Console.Error.WriteLine($"DELETE failed {(int)response.StatusCode}: {url}");
}

static bool ShouldDrop(DateTimeOffset createdAt, int days, DateTimeOffset now)
    => days <= 0 || (now - createdAt).TotalDays > days;

sealed class Summary
{
    public int Artifacts { get; set; }
    public double ArtifactMb { get; set; }
    public int Caches { get; set; }
    public double CacheMb { get; set; }
    public int Releases { get; set; }
    public int Runs { get; set; }
}

sealed class Options
{
    public int ArtifactDays { get; private init; }
    public int CacheDays { get; private init; }
    public int ReleaseKeep { get; private init; }
    public int RunDays { get; private init; }
    public bool DryRun { get; private init; }

    public static Options Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        return new Options
        {
            ArtifactDays = int.TryParse(Value("--artifact-days"), out var a) ? a : 0,
            CacheDays = int.TryParse(Value("--cache-days"), out var c) ? c : 0,
            ReleaseKeep = int.TryParse(Value("--release-keep"), out var r) ? r : 0,
            RunDays = int.TryParse(Value("--run-days"), out var d) ? d : 0,
            DryRun = string.Equals(Value("--dry-run"), "true", StringComparison.OrdinalIgnoreCase)
        };
    }
}
