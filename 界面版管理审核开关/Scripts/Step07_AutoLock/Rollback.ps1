. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name InactivityTimeoutSecs -ErrorAction SilentlyContinue
    New-Result -Step '07_AutoLock' -Enabled $false -Status '已删除自动锁屏策略'
}catch{ New-Result -Step '07_AutoLock' -Success $false -Status '回滚失败' -ErrorMessage $_.Exception.Message }
