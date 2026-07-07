. "$PSScriptRoot\..\Common.ps1"
try{
    $p='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
    $v=Get-ItemProperty -Path $p -Name InactivityTimeoutSecs -ErrorAction SilentlyContinue
    New-Result -Step '07_AutoLock' -Enabled ($v.InactivityTimeoutSecs -gt 0) -Status '检查完成' -Evidence @{InactivityTimeoutSecs=$v.InactivityTimeoutSecs}
}catch{ New-Result -Step '07_AutoLock' -Success $false -Status '检查失败' -ErrorMessage $_.Exception.Message }
