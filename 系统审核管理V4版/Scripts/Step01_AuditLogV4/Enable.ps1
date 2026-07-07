. "$PSScriptRoot\..\Common.ps1"
try {
    Require-Admin
    $s=Read-Settings
    $archive=Get-ArchiveRoot
    Ensure-Directory $archive
    Ensure-Directory (Join-Path $archive 'Security')
    Ensure-Directory (Join-Path $archive 'FileAccess')
    Ensure-Directory (Join-Path $archive 'LogonLogoff')
    Ensure-Directory (Join-Path $archive 'RemoteAccess')
    Ensure-Directory (Join-Path $archive 'AccountManagement')
    Ensure-Directory (Join-Path $archive 'USB')
    Ensure-Directory (Join-Path $archive 'Print')
    Ensure-Directory (Join-Path $archive 'Reports')

    wevtutil sl Security /ms:$($s.SecurityLogMaxBytes) | Out-Null
    wevtutil sl Security /rt:false | Out-Null
    Set-EventLogMaxIfExists 'System' $s.SystemLogMaxBytes | Out-Null
    Set-EventLogMaxIfExists 'Application' $s.ApplicationLogMaxBytes | Out-Null

    $pol=@()
    $pol += Set-AuditPolSafe 'Logon' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Logoff' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Account Lockout' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'User Account Management' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Security Group Management' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Computer Account Management' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Audit Policy Change' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Security State Change' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Security System Extension' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'System Integrity' 'enable' 'enable'
    $pol += Set-AuditPolSafe 'Removable Storage' 'enable' 'enable'
    if($s.EnableObjectAccessAudit){ $pol += Set-AuditPolSafe 'File System' 'enable' 'enable' }
    if($s.EnableFileShareAudit){
        $pol += Set-AuditPolSafe 'File Share' 'enable' 'enable'
        $pol += Set-AuditPolSafe 'Detailed File Share' 'enable' 'enable'
    }
    if($s.EnableHandleManipulationAudit){ $pol += Set-AuditPolSafe 'Handle Manipulation' 'enable' 'enable' }
    else { $pol += Set-AuditPolSafe 'Handle Manipulation' 'disable' 'disable' }

    if($s.EnablePrintServiceOperationalLog){ Enable-EventLogIfExists 'Microsoft-Windows-PrintService/Operational' | Out-Null }
    if($s.EnablePowerShellOperationalLog){ Enable-EventLogIfExists 'Microsoft-Windows-PowerShell/Operational' | Out-Null }
    Enable-EventLogIfExists 'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational' | Out-Null
    Enable-EventLogIfExists 'Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational' | Out-Null
    Enable-EventLogIfExists 'Microsoft-Windows-DriverFrameworks-UserMode/Operational' | Out-Null

    $saclResults=@()
    foreach($dir in @($s.AuditDirectories)){
        if([string]::IsNullOrWhiteSpace($dir)){ continue }
        if(!(Test-Path -LiteralPath $dir)){ New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $acl=Get-Acl -LiteralPath $dir
        $rights=[System.Security.AccessControl.FileSystemRights]$s.FileAuditRights
        $rule=New-Object System.Security.AccessControl.FileSystemAuditRule($s.FileAuditIdentity,$rights,'ContainerInherit,ObjectInherit','None','Success,Failure')
        $acl.AddAuditRule($rule)
        Set-Acl -LiteralPath $dir -AclObject $acl
        $saclResults += [PSCustomObject]@{Path=$dir; Identity=$s.FileAuditIdentity; Rights=$s.FileAuditRights; AuditFlags='Success,Failure'}
    }

    New-Result -Step '01_AuditLogV4' -Enabled $true -Status '已启用 V4 审计策略、事件日志、文件目录 SACL 和归档目录' -Evidence @{ArchiveRoot=$archive; AuditDirectories=$saclResults; AuditPolicyResult=$pol}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '启用失败' -ErrorMessage $_.Exception.Message }
