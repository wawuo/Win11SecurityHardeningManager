. "$PSScriptRoot\..\Common.ps1"
try{
    $p='HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters'
    $v=Get-ItemProperty -Path $p -Name AutoShareWks -ErrorAction SilentlyContinue
    $shares=Get-SmbShare | Where-Object {$_.Name -match '^[A-Z]\$$|^ADMIN\$$|^IPC\$$'} | Select-Object Name,Path,Description
    New-Result -Step '03_DisableAdminShares' -Enabled ($v.AutoShareWks -eq 0) -Status '检查完成' -Evidence @{AutoShareWks=$v.AutoShareWks; DefaultShares=$shares}
}catch{ New-Result -Step '03_DisableAdminShares' -Success $false -Status '检查失败' -ErrorMessage $_.Exception.Message }
