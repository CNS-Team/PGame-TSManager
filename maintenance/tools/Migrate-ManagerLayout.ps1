[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManagerDir,
    [switch]$IncludeSourceDirectories,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ManagerDir).Path

function Ensure-Directory([string]$Path) {
    if ($DryRun) { return }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Move-Into([string]$Source, [string]$DestinationParent) {
    if (-not (Test-Path -LiteralPath $Source)) { return }
    Ensure-Directory $DestinationParent
    $destination = Join-Path $DestinationParent (Split-Path -Leaf $Source)
    if (Test-Path -LiteralPath $destination) {
        $sourceItem = Get-Item -LiteralPath $Source -Force
        if ($sourceItem.PSIsContainer) {
            foreach ($child in Get-ChildItem -LiteralPath $Source -Force) {
                Move-Into $child.FullName $destination
            }
            if (-not $DryRun) {
                $remaining = @(Get-ChildItem -LiteralPath $Source -Force)
                if ($remaining.Count -eq 0) { Remove-Item -LiteralPath $Source -Recurse -Force }
            }
        } else {
            if ($DryRun) {
                Write-Host "[dry-run] keep existing file and remove old copy: $destination"
            } else {
                Remove-Item -LiteralPath $Source -Force
            }
        }
        return
    }
    if ($DryRun) {
        Write-Host "[dry-run] $Source -> $destination"
        return
    }
    Move-Item -LiteralPath $Source -Destination $DestinationParent -Force
    Write-Host "Moved: $Source -> $destination"
}

function Move-DirectoryContents-Into([string]$SourceDirectory, [string]$DestinationDirectory) {
    if (-not (Test-Path -LiteralPath $SourceDirectory)) { return }

    Ensure-Directory $DestinationDirectory
    foreach ($child in @(Get-ChildItem -LiteralPath $SourceDirectory -Force)) {
        Move-Into $child.FullName $DestinationDirectory
    }

    if (-not $DryRun) {
        $remaining = @(Get-ChildItem -LiteralPath $SourceDirectory -Force)
        if ($remaining.Count -eq 0) {
            Remove-Item -LiteralPath $SourceDirectory -Recurse -Force
            Write-Host "Removed empty legacy directory: $SourceDirectory"
        }
    }
}

function Set-Property([object]$Object, [string]$Name, [object]$Value) {
    if ($null -ne $Object.PSObject.Properties[$Name]) {
        $Object.$Name = $Value
    } else {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
    }
}

$core = Join-Path $root 'Core'
$servers = Join-Path $root 'Servers'
$tools = Join-Path $root 'Tools'
$sourceArchive = Join-Path $tools 'Source'
$artifactsArchive = Join-Path $tools 'Artifacts'

Ensure-Directory $core
Ensure-Directory $servers
Ensure-Directory $tools

foreach ($name in @(
    'TShock.Server.exe',
    'GeoIP.dat',
    'bin',
    'i18n',
    'runtimes',
    'x64',
    'Data',
    'Logs',
    'Backups',
    '_runtime'
)) {
    Move-Into (Join-Path $root $name) $core
}

Ensure-Directory (Join-Path $servers 'Profiles')
$profiles = Join-Path $servers 'Profiles'

# Legacy layout: 1.PigeonServers\<server>. The profile directories belong directly
# under Servers\Profiles, not inside another 1.PigeonServers directory.
Move-DirectoryContents-Into (Join-Path $root '1.PigeonServers') $profiles

# Repair packages produced by the earlier migration script, which nested the
# legacy container as Servers\Profiles\1.PigeonServers.
Move-DirectoryContents-Into (Join-Path $profiles '1.PigeonServers') $profiles

Move-Into (Join-Path $root 'Worlds') $servers

Move-Into (Join-Path $root 'maintenance') $tools

if ($IncludeSourceDirectories) {
    foreach ($name in @('PGame-TSManager', 'TerrariaServerAPI', 'package-check', 'personal-overlay', '.github', '_tmp_pgame_package_test')) {
        Move-Into (Join-Path $root $name) $sourceArchive
    }
    Move-Into (Join-Path $root 'artifacts') $artifactsArchive
}

$configPath = Join-Path $root 'config.json'
if (Test-Path -LiteralPath $configPath) {
    $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    # ⚠ 2026-10-12 起管理器 config.json 使用**中文键**（旧英文键由管理器启动时自动迁移，见 ManagerConfig.MigrateLegacyKeys）
    Set-Property $config '运行时母本目录' 'Core'
    Set-Property $config '世界目录' 'Servers\Worlds'
    Set-Property $config '插件总库' 'Plugins'
    Set-Property $config '运行沙箱目录' 'Core\_runtime'
    Set-Property $config '服务器目录' 'Servers\Profiles'
    Set-Property $config '备份目录' 'Core\Backups'
    Set-Property $config '日志目录' 'Core\Logs'
    Set-Property $config '数据目录' 'Core\Data'
    Set-Property $config '工具目录' 'Tools'

    foreach ($profile in @($config.服务器列表)) {
        if ($null -eq $profile) { continue }
        $profile.目录 = [string]$profile.目录 -replace '1\.PigeonServers', 'Servers\Profiles'
    }

    if ($DryRun) {
        Write-Host '[dry-run] config.json would be updated to Core/Servers/Plugins/Tools.'
    } else {
        $config | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $configPath -Encoding UTF8
        Write-Host "Updated: $configPath"
    }
}

Write-Host 'Manager layout migration completed. Visible folders: Core, Servers, Plugins, Tools.' -ForegroundColor Green
