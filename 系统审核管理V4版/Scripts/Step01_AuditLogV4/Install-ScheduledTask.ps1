. "$PSScriptRoot\..\Common.ps1"
try {
    Require-Admin
    $s=Read-Settings
    $script=Join-Path $PSScriptRoot 'Export-AuditEvents.ps1'
    $action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
    $time=[DateTime]::Parse($s.ScheduledTaskTime)
    $trigger=New-ScheduledTaskTrigger -Daily -At $time.TimeOfDay
    $principal=New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest
    $settings=New-ScheduledTaskSettingsSet -Compatibility Win8 -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $s.ScheduledTaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
    New-Result -Step '01_AuditLogV4' -Enabled $true -Status '已安装每日审计采集计划任务' -Evidence @{TaskName=$s.ScheduledTaskName; Time=$s.ScheduledTaskTime; Script=$script}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '安装计划任务失败' -ErrorMessage $_.Exception.Message }
