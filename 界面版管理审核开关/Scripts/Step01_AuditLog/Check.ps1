. "$PSScriptRoot\..\Common.ps1"
try{
    $s=Read-Settings
    $sec=Get-WinEvent -ListLog Security -ErrorAction Stop
    $audit=(auditpol /get /subcategory:"File System","Handle Manipulation","Logon","Logoff","Account Lockout","User Account Management" 2>$null) -join "`n"
    $sacl=@()
    foreach($dir in $s.AuditDirectories){
        if(Test-Path $dir){
            $acl=Get-Acl $dir
            $sacl += [PSCustomObject]@{Path=$dir; AuditRules=($acl.Audit | Select-Object IdentityReference,FileSystemRights,AuditFlags,InheritanceFlags,PropagationFlags)}
        } else { $sacl += [PSCustomObject]@{Path=$dir; Missing=$true} }
    }
    New-Result -Step '01_AuditLog' -Enabled ($sec.MaximumSizeInBytes -ge 1073741824) -Status '检查完成' -Evidence @{SecurityLogMaxBytes=$sec.MaximumSizeInBytes; RetentionDays=$s.AuditRetentionDays; AuditPol=$audit; SACL=$sacl}
}catch{ New-Result -Step '01_AuditLog' -Success $false -Status '检查失败' -ErrorMessage $_.Exception.Message }
