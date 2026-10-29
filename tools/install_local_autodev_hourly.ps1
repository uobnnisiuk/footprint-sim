# Run only after this revision is independently reviewed, merged and deployed.
# Replaces the existing Daily task; no model call or task start is performed here.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskName = 'Footprint Local AutoDev Daily'
$repo = '/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6'
$entry = "$repo/tools/run_local_autodev_hourly.sh"
$dryScript = "$repo/tools/local_autodev_hourly.py"
$wsl = Join-Path $env:SystemRoot 'System32\wsl.exe'
if ((Get-TimeZone).Id -ne 'Tokyo Standard Time') {
    throw 'Windows timezone must be Tokyo Standard Time.'
}
if (-not (Test-Path -LiteralPath $wsl)) { throw 'wsl.exe was not found.' }
$previous = Get-ScheduledTask -TaskName $taskName
if ($previous.State -eq 'Running') {
    throw 'Daily is running. Preserve the in-flight attempt; install after it finishes.'
}
$backupDirectory = Join-Path $env:LOCALAPPDATA 'FootprintLocalAutoDev\backups'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backupPath = Join-Path $backupDirectory ("daily-before-hourly-" + (Get-Date -Format 'yyyyMMddTHHmmssffff') + '.xml')
Export-ScheduledTask -TaskName $taskName | Set-Content -LiteralPath $backupPath -Encoding Unicode

& $wsl -d Ubuntu -u higuchi --exec /bin/bash --login -c "python3 -B '$dryScript' --repo-path '$repo'"
if ($LASTEXITCODE -ne 0) { throw "Hourly dry-run failed (exit $LASTEXITCODE); task unchanged." }
# Recheck the in-flight state immediately before replacing the existing definition.
if ((Get-ScheduledTask -TaskName $taskName).State -eq 'Running') {
    throw 'Daily started during preflight. Task unchanged; do not interrupt the attempt.'
}
$arguments = "-d Ubuntu -u higuchi --exec /bin/bash --login $entry"
$action = New-ScheduledTaskAction -Execute $wsl -Argument $arguments
$times = @(0..23 | ForEach-Object { '{0:00}:05' -f $_ })
$triggers = @($times | ForEach-Object { New-ScheduledTaskTrigger -Daily -At $_ })
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 40) -StartWhenAvailable:$false -WakeToRun:$false
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $triggers -Settings $settings -Principal $principal -Description 'Hourly approved-work polling; hard cap 8 attempted Issues per JST day; one per hour; no retry' -Force | Out-Null

$task = Get-ScheduledTask -TaskName $taskName
$observed = @($task.Triggers | Where-Object { $_.Enabled } | ForEach-Object {
    ([DateTimeOffset]::Parse($_.StartBoundary)).ToLocalTime().ToString('HH:mm')
} | Sort-Object)
if ($observed.Count -ne 24 -or (Compare-Object $times $observed) -or
    $task.Actions.Count -ne 1 -or $task.Actions[0].Arguments -ne $arguments) {
    Disable-ScheduledTask -TaskName $taskName | Out-Null
    throw "Hourly registration verification failed; task disabled. Preserve backup: $backupPath"
}
$info = Get-ScheduledTaskInfo -TaskName $taskName
Write-Host "Registered: $($task.TaskName), state: $($task.State)"
Write-Host 'Hourly checks: 00:05 through 23:05 JST; at most 8 ATTEMPTS per JST day.'
Write-Host "Next run (observed): $($info.NextRunTime)"
Write-Host "Previous definition: $backupPath"
Write-Host 'No AI call, manual task start, retry, wake, account purchase or Monitor change.'
Write-Host 'Do not restore the old controller within a day containing hourly attempts.'
