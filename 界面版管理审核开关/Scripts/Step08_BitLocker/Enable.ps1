. "$PSScriptRoot\..\Common.ps1"
try{
    Require-Admin
    $s=Read-Settings
    if(!(Test-Path $s.BitLockerRecoveryKeyBackupPath)){ New-Item -ItemType Directory -Path $s.BitLockerRecoveryKeyBackupPath -Force | Out-Null }
    $bl=Get-BitLockerVolume -MountPoint 'C:'
    if($bl.ProtectionStatus -ne 'On'){
        Enable-BitLocker -MountPoint 'C:' -RecoveryPasswordProtector -UsedSpaceOnly -SkipHardwareTest -ErrorAction Stop
    }
    $bl=Get-BitLockerVolume -MountPoint 'C:'
    $file=Join-Path $s.BitLockerRecoveryKeyBackupPath "$env:COMPUTERNAME-C-BitLockerRecovery.txt"
    $bl.KeyProtector | Where-Object {$_.KeyProtectorType -eq 'RecoveryPassword'} | Format-List * | Out-File $file -Encoding UTF8
    New-Result -Step '08_BitLocker' -Enabled $true -Status '已启用/检查 BitLocker，并导出恢复密码信息' -Evidence @{BackupFile=$file; ProtectionStatus=$bl.ProtectionStatus}
}catch{ New-Result -Step '08_BitLocker' -Success $false -Status '启用失败' -ErrorMessage $_.Exception.Message }
