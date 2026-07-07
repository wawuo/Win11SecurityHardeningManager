. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    $p='HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters'
    New-Item -Path $p -Force | Out-Null
    New-ItemProperty -Path $p -Name AutoShareWks -Value 0 -PropertyType DWord -Force | Out-Null
    foreach($s in Get-SmbShare | Where-Object {$_.Name -match '^[A-Z]\$$'}){ Remove-SmbShare -Name $s.Name -Force -ErrorAction SilentlyContinue }
    New-Result -Step '03_DisableAdminShares' -Enabled $true -Status '已禁用磁盘默认管理共享；ADMIN$/IPC$ 可能仍由系统维护，重启 Server 服务或系统后生效更完整'
}catch{ New-Result -Step '03_DisableAdminShares' -Success $false -Status '启用失败' -ErrorMessage $_.Exception.Message }
