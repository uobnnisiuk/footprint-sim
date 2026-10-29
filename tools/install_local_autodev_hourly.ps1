# Run only after this revision is independently reviewed, merged and deployed.
# The operator must save the original definition/enabled state, disable an idle
# Daily, and hold the WSL deployment locks. This installer stages it DISABLED.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskName = 'Footprint Local AutoDev Daily'
$taskPath = '\'
$repo = '/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6'
$entry = "$repo/tools/run_local_autodev_hourly.sh"
$dryScript = "$repo/tools/local_autodev_hourly.py"
$wsl = Join-Path $env:SystemRoot 'System32\wsl.exe'
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$windowsPrincipal = [System.Security.Principal.WindowsPrincipal]::new($identity)
if ($windowsPrincipal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this installer as the normal, non-elevated logged-on user.'
}
if ((Get-TimeZone).Id -ne 'Tokyo Standard Time') {
    throw 'Windows timezone must be Tokyo Standard Time.'
}
if (-not (Test-Path -LiteralPath $wsl)) { throw 'wsl.exe was not found.' }
function Get-UserSid([string]$UserId) {
    if ($UserId.StartsWith('S-1-')) {
        return ([System.Security.Principal.SecurityIdentifier]::new($UserId)).Value
    }
    return ([System.Security.Principal.NTAccount]::new($UserId)).Translate([System.Security.Principal.SecurityIdentifier]).Value
}
function Assert-DisabledDaily {
    $current = Get-ScheduledTask -TaskName $taskName -TaskPath $taskPath
    if ($current.State -ne 'Disabled' -or $current.Settings.Enabled) {
        throw 'Daily must already be disabled and idle; preserve its original XML/enabled state and acquire WSL deployment locks first.'
    }
}
Assert-DisabledDaily
$previous = Get-ScheduledTask -TaskName $taskName -TaskPath $taskPath
if ((Get-UserSid $previous.Principal.UserId) -ne $identity.User.Value -or
    $previous.Settings.ExecutionTimeLimit -ne 'PT40M') {
    throw 'Existing Daily user or execution time limit differs from the reviewed deployment contract.'
}
$backupDirectory = Join-Path $env:LOCALAPPDATA 'FootprintLocalAutoDev\backups'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backupPath = Join-Path $backupDirectory ("daily-disabled-before-hourly-" + (Get-Date -Format 'yyyyMMddTHHmmssffff') + '.xml')
Export-ScheduledTask -TaskName $taskName -TaskPath $taskPath | Set-Content -LiteralPath $backupPath -Encoding Unicode

& $wsl -d Ubuntu -u higuchi --exec /bin/bash --login -c "python3 -B '$dryScript' --repo-path '$repo'"
if ($LASTEXITCODE -ne 0) { throw "Hourly dry-run failed (exit $LASTEXITCODE); task remains disabled and unchanged." }
Assert-DisabledDaily
$arguments = "-d Ubuntu -u higuchi --exec /bin/bash --login $entry"
$action = New-ScheduledTaskAction -Execute $wsl -Argument $arguments
$times = @(0..23 | ForEach-Object { '{0:00}:05' -f $_ })
$triggers = @($times | ForEach-Object { New-ScheduledTaskTrigger -Daily -At $_ })
$settings = New-ScheduledTaskSettingsSet -Disable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 40) -StartWhenAvailable:$false -WakeToRun:$false -AllowStartIfOnBatteries:(-not $previous.Settings.DisallowStartIfOnBatteries) -DontStopIfGoingOnBatteries:(-not $previous.Settings.StopIfGoingOnBatteries)
$principal = New-ScheduledTaskPrincipal -UserId $identity.Name -LogonType Interactive -RunLevel Limited
try {
    Register-ScheduledTask -TaskName $taskName -TaskPath $taskPath -Action $action -Trigger $triggers -Settings $settings -Principal $principal -Description 'Hourly approved-work polling; hard cap 8 attempted Issues per JST day; one per hour; no retry' -Force | Out-Null
    $task = Get-ScheduledTask -TaskName $taskName -TaskPath $taskPath
    $observed = @($task.Triggers | ForEach-Object {
        if (-not $_.Enabled -or $_.CimClass.CimClassName -ne 'MSFT_TaskDailyTrigger' -or
            $_.DaysInterval -ne 1 -or $_.EndBoundary -or $_.Repetition.Interval -or
            ($_.RandomDelay -and $_.RandomDelay -ne 'PT0S')) {
            throw 'Unexpected hourly trigger type, interval, expiry, repetition, delay or enabled state.'
        }
        ([DateTimeOffset]::Parse($_.StartBoundary)).ToLocalTime().ToString('HH:mm:ss')
    } | Sort-Object)
    $expected = @($times | ForEach-Object { $_ + ':00' })
    if (@($task.Triggers).Count -ne 24 -or (Compare-Object $expected $observed) -or
        @($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne $wsl -or
        $task.Actions[0].Arguments -ne $arguments -or $task.Actions[0].WorkingDirectory -or
        (Get-UserSid $task.Principal.UserId) -ne $identity.User.Value -or
        $task.Principal.LogonType -ne 'Interactive' -or $task.Principal.RunLevel -ne 'Limited' -or
        $task.Settings.MultipleInstances -ne 'IgnoreNew' -or $task.Settings.RestartCount -ne 0 -or
        $task.Settings.WakeToRun -or $task.Settings.StartWhenAvailable -or
        $task.Settings.ExecutionTimeLimit -ne 'PT40M' -or $task.Settings.Enabled -or
        $task.State -ne 'Disabled' -or
        $task.Settings.DisallowStartIfOnBatteries -ne $previous.Settings.DisallowStartIfOnBatteries -or
        $task.Settings.StopIfGoingOnBatteries -ne $previous.Settings.StopIfGoingOnBatteries) {
        throw 'Hourly registration does not match the reviewed deployment contract.'
    }
} catch {
    Disable-ScheduledTask -TaskName $taskName -TaskPath $taskPath | Out-Null
    throw "Hourly registration/verification failed; task left disabled. Preserve backup: $backupPath. $($_.Exception.Message)"
}
Write-Host "Staged and verified: $($task.TaskName), state: $($task.State)"
Write-Host 'Hourly checks: 00:05 through 23:05 JST; at most 8 ATTEMPTS per JST day.'
Write-Host "Previous disabled definition: $backupPath"
Write-Host 'After deployment verification, the operator must restore the saved enabled state and read the actual NextRunTime.'
Write-Host 'No AI call, manual task start, retry, wake, account purchase or Monitor change.'
Write-Host 'Do not restore the old controller within a day containing hourly attempts.'
