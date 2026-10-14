param(
  [Parameter(Mandatory=$true)][string]$Text,
  [long]$GroupId = 0,
  [switch]$DryRun
)
# TSM 告警 → Bot 统一上报 转发桥（10-TSM/11-Bot 联合口径 2026-10-15）
# 契约（TSM Alerter.cs）：powershell -File 本脚本 -Text "<单行正文>" -GroupId <群号>
# 说明：Alerter 对每个告警群各调一次本脚本；同一文本 180s 内只实际转发一次（去重）。
# 退出码：0=已发送/预演成功；1=发送失败（TSM 侧会记"部分发送失败"）。
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }
$hash = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-','').Substring(0,16)
$marker = Join-Path $env:TEMP ('pgtsm_alert_sent_' + $hash + '.marker')
if (-not $DryRun) {
  if (Test-Path -LiteralPath $marker) {
    $age = (Get-Date) - (Get-Item -LiteralPath $marker).LastWriteTime
    if ($age.TotalSeconds -lt 180) { exit 0 }
  }
  Set-Content -LiteralPath $marker -Value (Get-Date -Format o) -Encoding UTF8
}
$body = Join-Path $env:TEMP ('pgtsm_alert_body_' + $hash + '.txt')
[IO.File]::WriteAllText($body, $Text, [Text.UTF8Encoding]::new($false))
$exe = 'C:\Users\Administrator\Desktop\PigeonTR.Bot\PigeonTR.Bot.exe'
$arg = 'report --title "TSM 告警" --source TSM --status 警告 --file "' + $body + '"'
if ($DryRun) { $arg += ' --dry-run' }
$code = 1
try {
  $p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -ArgumentList $arg -WindowStyle Hidden -PassThru -Wait
  $code = $p.ExitCode
} catch { $code = 1 }
Remove-Item -LiteralPath $body -Force -ErrorAction SilentlyContinue
exit $code