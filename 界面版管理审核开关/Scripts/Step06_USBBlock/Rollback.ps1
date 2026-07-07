. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices' -Name Deny_All -ErrorAction SilentlyContinue
    gpupdate /target:computer /force | Out-Null
    New-Result -Step '06_USBBlock' -Enabled $false -Status '已恢复可移动存储访问策略'
}catch{ New-Result -Step '06_USBBlock' -Success $false -Status '回滚失败' -ErrorMessage $_.Exception.Message }
