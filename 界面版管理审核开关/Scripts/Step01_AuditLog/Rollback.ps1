. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    $s=Read-Settings
    auditpol /set /subcategory:"File System" /success:disable /failure:disable | Out-Null
    auditpol /set /subcategory:"Handle Manipulation" /success:disable /failure:disable | Out-Null
    foreach($dir in $s.AuditDirectories){
        if(Test-Path $dir){
            $acl=Get-Acl $dir
            $acl.SetAuditRuleProtection($false,$false)
            foreach($r in @($acl.Audit)){ $acl.RemoveAuditRule($r) | Out-Null }
            Set-Acl -Path $dir -AclObject $acl
        }
    }
    New-Result -Step '01_AuditLog' -Enabled $false -Status '已回滚文件系统审计和 SACL'
}catch{ New-Result -Step '01_AuditLog' -Success $false -Status '回滚失败' -ErrorMessage $_.Exception.Message }
