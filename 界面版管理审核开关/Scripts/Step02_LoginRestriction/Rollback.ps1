. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    if(Get-LocalGroup -Name 'AllowInteractiveLogon' -ErrorAction SilentlyContinue){ Remove-LocalGroup -Name 'AllowInteractiveLogon' }
    New-Result -Step '02_LoginRestriction' -Enabled $false -Status '已删除本地登录白名单组'
}catch{ New-Result -Step '02_LoginRestriction' -Success $false -Status '回滚失败' -ErrorMessage $_.Exception.Message }
