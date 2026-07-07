. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    # 本模块采用保守方案：只创建本地组 AllowInteractiveLogon，实际“允许/拒绝登录”建议由管理员在 secpol.msc 中确认 SID。
    # 防止误把管理员或服务账号拒绝导致无法登录。
    $group='AllowInteractiveLogon'
    if(-not (Get-LocalGroup -Name $group -ErrorAction SilentlyContinue)){ New-LocalGroup -Name $group -Description '允许本机交互式登录的白名单组' | Out-Null }
    $s=Read-Settings
    foreach($u in $s.AllowedDomainUsers){ try{ Add-LocalGroupMember -Group $group -Member $u -ErrorAction Stop }catch{} }
    New-Result -Step '02_LoginRestriction' -Enabled $true -Status '已创建/更新登录白名单组；请在 secpol.msc 用户权限分配中应用白名单策略' -Evidence @{LocalGroup=$group; Members=$s.AllowedDomainUsers}
}catch{ New-Result -Step '02_LoginRestriction' -Success $false -Status '启用失败' -ErrorMessage $_.Exception.Message }
