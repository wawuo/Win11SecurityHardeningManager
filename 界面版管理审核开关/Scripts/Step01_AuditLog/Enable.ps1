. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    $s=Read-Settings
    # 安全日志 1GB，保留模式为覆盖旧事件；真正 90 天保留建议配合计划任务导出归档。
    wevtutil sl Security /ms:1073741824
    wevtutil sl Security /rt:false
    auditpol /set /subcategory:"Logon" /success:enable /failure:enable | Out-Null
    auditpol /set /subcategory:"Logoff" /success:enable /failure:enable | Out-Null
    auditpol /set /subcategory:"Account Lockout" /success:enable /failure:enable | Out-Null
    auditpol /set /subcategory:"User Account Management" /success:enable /failure:enable | Out-Null
    auditpol /set /subcategory:"File System" /success:enable /failure:enable | Out-Null
    auditpol /set /subcategory:"Handle Manipulation" /success:disable /failure:disable | Out-Null
    foreach($dir in $s.AuditDirectories){
        if(!(Test-Path $dir)){ New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $acl=Get-Acl $dir
        $rule=New-Object System.Security.AccessControl.FileSystemAuditRule('Everyone','CreateFiles,CreateDirectories,Write,Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership','ContainerInherit,ObjectInherit','None','Success,Failure')
        $acl.AddAuditRule($rule)
        Set-Acl -Path $dir -AclObject $acl
    }
    New-Result -Step '01_AuditLog' -Enabled $true -Status '已启用审计策略和目录 SACL' -Evidence @{AuditDirectories=$s.AuditDirectories}
}catch{ New-Result -Step '01_AuditLog' -Success $false -Status '启用失败' -ErrorMessage $_.Exception.Message }
