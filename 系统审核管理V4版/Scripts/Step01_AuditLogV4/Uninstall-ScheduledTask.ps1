. "$PSScriptRoot\..\Common.ps1"
try {
    Require-Admin
    $s=Read-Settings
    Unregister-ScheduledTask -TaskName $s.ScheduledTaskName -Confirm:$false -ErrorAction SilentlyContinue
    New-Result -Step '01_AuditLogV4' -Enabled $false -Status '已卸载每日审计采集计划任务' -Evidence @{TaskName=$s.ScheduledTaskName}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '卸载计划任务失败' -ErrorMessage $_.Exception.Message }
