. "$PSScriptRoot\..\Common.ps1"
try {
    $s=Read-Settings
    $archive=Get-ArchiveRoot
    $sec=Get-WinEvent -ListLog Security -ErrorAction Stop
    $task=Get-ScheduledTask -TaskName $s.ScheduledTaskName -ErrorAction SilentlyContinue
    $auditText=Get-AuditPolText
    $dirs=@()
    foreach($dir in @($s.AuditDirectories)){
        if(Test-Path -LiteralPath $dir){
            $acl=Get-Acl -LiteralPath $dir
            $dirs += [PSCustomObject]@{Path=$dir; Exists=$true; AuditRuleCount=@($acl.Audit).Count; AuditRules=($acl.Audit | Select-Object IdentityReference,FileSystemRights,AuditFlags,InheritanceFlags,PropagationFlags)}
        } else { $dirs += [PSCustomObject]@{Path=$dir; Exists=$false; AuditRuleCount=0} }
    }
    $enabled = ($sec.MaximumSizeInBytes -ge [int64]$s.SecurityLogMaxBytes) -and ($auditText -match 'Logon') -and ($auditText -match 'File System')
    New-Result -Step '01_AuditLogV4' -Enabled $enabled -Status '检查完成' -Evidence @{SecurityLogMaxBytes=$sec.MaximumSizeInBytes; ArchiveRoot=$archive; ScheduledTaskExists=($null -ne $task); AuditDirectories=$dirs; AuditPolicy=$auditText}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '检查失败' -ErrorMessage $_.Exception.Message }
