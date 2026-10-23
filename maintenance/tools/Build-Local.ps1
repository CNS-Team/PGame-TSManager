<#
.SYNOPSIS
    本地一键打包 PGame-TSManager（替代已移除的 GitHub Actions 流水线）。

.DESCRIPTION
    2026-10-11 起本仓库**不再在 GitHub 上构建**（只存上游最新源码，构建改本地）：
    原来的 .github\workflows\build.yml 已删除，构建产物（Artifacts / Release 资产）也已清空。
    本脚本把 CI 里那套打包步骤原样搬到本地，一条命令产出与以前完全相同的三套包。

.PARAMETER OutputRoot
    产物输出目录，默认 <仓库根>\artifacts。

.PARAMETER SkipTShockRuntime
    已有 Core 运行时（或离线）时跳过下载官方 TShock Windows x64 运行时。

.PARAMETER SkipLayoutCheck
    跳过 Test-RepositoryLayout.ps1（仓库布局自检需要 git）。

.EXAMPLE
    ./maintenance/tools/Build-Local.ps1
    ./maintenance/tools/Build-Local.ps1 -SkipTShockRuntime -OutputRoot D:\build-out
#>
[CmdletBinding()]
param(
    [string]$OutputRoot,
    [switch]$SkipTShockRuntime,
    [switch]$SkipLayoutCheck
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts' }
$out = [IO.Path]::GetFullPath($OutputRoot)
$tools = $PSScriptRoot
$sw = [Diagnostics.Stopwatch]::StartNew()

function Step([string]$text) { Write-Host ""; Write-Host "=== $text ===" -ForegroundColor Cyan }

Step "读取版本 / 提交"
[xml]$proj = Get-Content -LiteralPath (Join-Path $root 'PGame-TSManager\PGame-TSManager.csproj')
$version = [string]($proj.Project.PropertyGroup | ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) { $version = '0.0.0' }
$commit = ''
try { $commit = (& git -C $root rev-parse HEAD 2>$null) } catch { }
if ([string]::IsNullOrWhiteSpace($commit)) { $commit = 'unknown' }
$runNumber = 'local-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
Write-Host ("版本 = $version  提交 = " + $commit.Substring(0, [Math]::Min(12, $commit.Length)) + "  标识 = $runNumber")

$managerDir = Join-Path $out 'manager'
$coreDir = Join-Path $managerDir 'Core'
$extrasDir = Join-Path $out 'package-extras'
$sourceZip = Join-Path $out 'PGame-TSManager-source.zip'
$templateZip = Join-Path $out 'PGame-TSManager-template.zip'
$universalZip = Join-Path $out 'PGame-TSManager-universal.zip'
New-Item -ItemType Directory -Force -Path $out | Out-Null

Step "1/6 dotnet publish（自包含单文件）"
& dotnet publish (Join-Path $root 'PGame-TSManager\PGame-TSManager.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -o $managerDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（exit=$LASTEXITCODE）" }

if ($SkipTShockRuntime) {
    Step "2/6 跳过 TShock 运行时下载（-SkipTShockRuntime）"
} else {
    Step "2/6 下载官方 TShock Windows x64 运行时并注入 Core"
    & (Join-Path $tools 'Prepare-TShockRuntime.ps1') -OutputDirectory $coreDir
}

Step "3/6 生成使用说明与更新说明"
& (Join-Path $tools 'New-PackageExtras.ps1') -OutputDirectory $extrasDir `
    -Version $version -Commit $commit -RunNumber $runNumber -Repository 'CNS-Team/PGame-TSManager'

Step "4/6 打包 source / template / universal"
& (Join-Path $tools 'New-SourceBundle.ps1') -RepositoryRoot $root -OutputPath $sourceZip
& (Join-Path $tools 'New-ReleaseBundle.ps1') -RepositoryRoot $root -PublishDirectory $managerDir `
    -RuntimeDirectory $coreDir -OutputPath $templateZip
& (Join-Path $tools 'New-UniversalBundle.ps1') -RepositoryRoot $root -PublishDirectory $managerDir `
    -RuntimeDirectory $coreDir -OutputPath $universalZip

Step "5/6 把说明塞进三个 zip"
$extras = @(Get-ChildItem -LiteralPath $extrasDir -Filter '*.txt' |
    Where-Object { $_.Name -notin @('package-usage.zh-CN.txt', 'update-notes.txt') } |
    ForEach-Object { $_.FullName })
if ($extras.Count -gt 0) {
    & (Join-Path $tools 'Add-ZipEntries.ps1') -ArchivePath @($sourceZip, $templateZip, $universalZip) -FilePath $extras
}

if ($SkipLayoutCheck) {
    Step "6/6 跳过仓库布局自检（-SkipLayoutCheck）"
} else {
    Step "6/6 仓库布局自检"
    & (Join-Path $tools 'Test-RepositoryLayout.ps1') -RepositoryRoot $root
}

$sw.Stop()
Step "完成（用时 $([int]$sw.Elapsed.TotalSeconds) 秒）"
foreach ($f in @($sourceZip, $templateZip, $universalZip) + (Get-ChildItem -LiteralPath $extrasDir -File)) {
    if (Test-Path -LiteralPath $f) {
        $fi = Get-Item -LiteralPath $f
        Write-Host ("  {0,10:N1} MB  {1}" -f ($fi.Length / 1MB), $fi.FullName)
    }
}
Write-Host ""
Write-Host "产物目录: $out" -ForegroundColor Green
Write-Host "提示：GitHub 上已不再构建；把 artifacts\ 下的包直接部署到服务器即可。" -ForegroundColor Green
