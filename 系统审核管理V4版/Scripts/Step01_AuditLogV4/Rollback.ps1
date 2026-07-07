. "$PSScriptRoot\..\Common.ps1"
try {
    Require-Admin
    $s=Read-Settings
    Set-AuditPolSafe 'File System' 'disable' 'disable' | Out-Null
    Set-AuditPolSafe 'File Share' 'disable' 'disable' | Out-Null
    Set-AuditPolSafe 'Detailed File Share' 'disable' 'disable' | Out-Null
    Set-AuditPolSafe 'Handle Manipulation' 'disable' 'disable' | Out-Null
    Set-AuditPolSafe 'Removable Storage' 'disable' 'disable' | Out-Null
    foreach($dir in @($s.AuditDirectories)){
        if(Test-Path -LiteralPath $dir){
            $acl=Get-Acl -LiteralPath $dir
            foreach($r in @($acl.Audit)){
                if($r.IdentityReference -like $s.FileAuditIdentity -or $r.IdentityReference.Value -eq $s.FileAuditIdentity){ $acl.RemoveAuditRule($r) | Out-Null }
            }
            Set-Acl -LiteralPath $dir -AclObject $acl
        }
    }
    New-Result -Step '01_AuditLogV4' -Enabled $false -Status '已回滚文件/共享/可移动存储审计和配置目录 SACL；登录/账户管理基础审计未关闭，避免降低安全基线'
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '回滚失败' -ErrorMessage $_.Exception.Message }
