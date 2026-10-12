[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$RuntimeDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$OutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) { [IO.Path]::GetFullPath($OutputPath) } else { [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputPath)) }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('PGame-TSManager-release-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

try {
    Copy-DirectoryContents $publish $stage
    if (-not [string]::IsNullOrWhiteSpace($RuntimeDirectory) -and
        (Test-Path -LiteralPath $RuntimeDirectory -PathType Container)) {
        Copy-DirectoryContents $RuntimeDirectory (Join-Path $stage 'Core')
    }
    foreach ($relative in @('maintenance\tools', 'maintenance\templates', 'README.md')) {
        $source = Join-Path $root $relative
        if (Test-Path -LiteralPath $source) {
            $destination = Join-Path $stage $relative
            if ((Get-Item -LiteralPath $source).PSIsContainer) {
                Copy-DirectoryContents $source $destination
            } else {
                Copy-Item -LiteralPath $source -Destination $destination -Force
            }
        }
    }
    $privateMigrationTool = Join-Path $stage 'maintenance\tools\New-PigeonServersLayout.ps1'
    if (Test-Path -LiteralPath $privateMigrationTool) {
        Remove-Item -LiteralPath $privateMigrationTool -Force
    }
    $template = [ordered]@{
        # ⚠ 2026-10-12 起管理器 config.json 使用**中文键**（打包出的模板同样）
        '启动前备份' = $true
        '备份目录' = 'Core\Backups'
        '备份保留数' = 10
        '服务器目录' = 'Servers\Profiles'
        '插件总库' = 'Plugins'
        '运行沙箱目录' = 'Core\_runtime'
        '运行时母本目录' = 'Core'
        '世界目录' = 'Servers\Worlds'
        '日志目录' = 'Core\Logs'
        '数据目录' = 'Core\Data'
        '工具目录' = 'Tools'
        '顺序启动' = $true
        '看门狗开关' = $true
        '告警开关' = $true
        '服务器列表' = @()
    }
    $template | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'config.json') -Encoding UTF8
    $readme = Join-Path $stage '部署说明.txt'
    @'
PGame-TSManager release template

This package contains the manager, maintenance tools and two TShock templates:
  1.生存  : game 7777 / REST 7878
  2.生存2 : game 7778 / REST 7879
Both templates require TShockAPI.dll and HotReload.dll by default.
The Core TShock runtime is prepared automatically during GitHub builds.
Place your world files in Servers\Worlds, then start PGame-TSManager.exe.
Running servers, worlds, databases, logs, _runtime and personal plugins are excluded from GitHub bundles.
'@ | Set-Content -LiteralPath $readme -Encoding UTF8
    & (Join-Path $root 'maintenance\tools\Migrate-ManagerLayout.ps1') `
        -ManagerDir $stage -IncludeSourceDirectories
    & (Join-Path $root 'maintenance\tools\Add-TShockTemplates.ps1') `
        -ManagerDir $stage
    $parent = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-Path -LiteralPath $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }
    $archiveFiles = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    if ($archiveFiles.Count -eq 0) { throw 'No release files selected for the bundle.' }
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $OutputPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) { throw "Archive was not created: $OutputPath" }
    Write-Host "Release template created: $OutputPath"
}
finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
