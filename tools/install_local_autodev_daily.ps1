# Install once from Windows PowerShell as the normal logged-on user.
# The installer runs read-only preflight, but never starts Codex.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskName = 'Footprint Local AutoDev Daily'
$entry = '/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6/tools/run_local_autodev_daily.sh'
$dryScript = '/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6/tools/local_autodev_daily.py'
$repo = '/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6'
$wsl = Join-Path $env:SystemRoot 'System32\wsl.exe'
if ((Get-TimeZone).Id -ne 'Tokyo Standard Time') {
    throw 'Windows timezone must be Tokyo Standard Time for daily JST slots.'
}
if (-not (Test-Path -LiteralPath $wsl)) {
    throw 'wsl.exe was not found.'
}

# Bash --login provides the same CLI PATH used by the WSL login account.
& $wsl -d Ubuntu -u higuchi --exec /bin/bash --login -c "python3 -B '$dryScript' --repo-path '$repo'"
if ($LASTEXITCODE -ne 0) {
    throw "WSL dry-run failed (exit $LASTEXITCODE). The task was not registered."
}
$action = New-ScheduledTaskAction -Execute $wsl -Argument ("-d Ubuntu -u higuchi --exec /bin/bash --login " + $entry)
$triggers = @('00:05', '03:05', '06:05', '09:05', '12:05', '15:05', '18:05', '21:05') | ForEach-Object { New-ScheduledTaskTrigger -Daily -At $_ }
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 40) -StartWhenAvailable:$false -WakeToRun:$false
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited

# Leaves the existing read-only 'Footprint Local AutoDev Monitor' untouched.
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $triggers -Settings $settings -Principal $principal -Description 'Owner-authorized, eight three-hour JST slots per day, one ChatGPT-plan Codex Issue per slot; no auto-merge' -Force | Out-Null

$task = Get-ScheduledTask -TaskName $taskName
$info = Get-ScheduledTaskInfo -TaskName $taskName
Write-Host "Registered: $($task.TaskName), state: $($task.State)"
Write-Host "Next run: $($info.NextRunTime)"
Write-Host "Daily slots JST: 00:05, 03:05, 06:05, 09:05, 12:05, 15:05, 18:05, 21:05"
Write-Host 'No AI call was started by this installer.'
Write-Host 'To disable: Disable-ScheduledTask -TaskName "Footprint Local AutoDev Daily"'
