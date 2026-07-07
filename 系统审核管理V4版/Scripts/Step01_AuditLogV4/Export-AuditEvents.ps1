. "$PSScriptRoot\..\Common.ps1"
try {
    $s=Read-Settings
    $root=Get-ArchiveRoot
    Ensure-Directory $root
    $run=Get-Date -Format 'yyyyMMdd_HHmmss'
    $outDir=Join-Path $root $run
    Ensure-Directory $outDir
    $start=(Get-Date).AddDays(-[int]$s.ExportLookbackDays)

    function Export-Events($Name,$LogName,$Ids,$TargetSubDir,$ExtraFilter=$null){
        $dir=Join-Path $outDir $TargetSubDir; Ensure-Directory $dir
        $events=@()
        try {
            if($Ids){ $events=Get-WinEvent -FilterHashtable @{LogName=$LogName; Id=$Ids; StartTime=$start} -ErrorAction SilentlyContinue }
            else { $events=Get-WinEvent -FilterHashtable @{LogName=$LogName; StartTime=$start} -ErrorAction SilentlyContinue }
            if($ExtraFilter){ $events=$events | Where-Object $ExtraFilter }
        } catch { $events=@() }
        $rows=$events | ForEach-Object {
            [PSCustomObject]@{
                TimeCreated=$_.TimeCreated
                Id=$_.Id
                ProviderName=$_.ProviderName
                LogName=$_.LogName
                MachineName=$_.MachineName
                LevelDisplayName=$_.LevelDisplayName
                Message=($_.Message -replace "`r|`n",' ')
            }
        }
        $csv=Join-Path $dir "$Name.csv"
        $html=Join-Path $dir "$Name.html"
        $rows | Export-Csv -LiteralPath $csv -Encoding UTF8 -NoTypeInformation
        $rows | ConvertTo-Html -Title $Name -PreContent "<h1>$Name</h1><p>StartTime: $start</p>" | Out-File -LiteralPath $html -Encoding UTF8
        return [PSCustomObject]@{Name=$Name; Count=@($rows).Count; Csv=$csv; Html=$html}
    }

    $summary=@()
    $summary += Export-Events '11.1_Logon_Logoff' 'Security' @(4624,4625,4634,4647,4800,4801,4740) 'LogonLogoff'
    $summary += Export-Events '11.2_File_Access' 'Security' @(4663,4660,4670,4659,5140,5145) 'FileAccess'
    $summary += Export-Events '11.3_Remote_Access_RDP' 'Security' @(4624,4625,4634,4647) 'RemoteAccess' { $_.Message -match 'Logon Type:\s*(10|7|3)' }
    $summary += Export-Events '11.3_TerminalServices_LocalSession' 'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational' @(21,22,23,24,25,39,40) 'RemoteAccess'
    $summary += Export-Events '11.3_TerminalServices_RemoteConnection' 'Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational' @(1149) 'RemoteAccess'
    $summary += Export-Events '11.4_Account_Management' 'Security' @(4720,4722,4723,4724,4725,4726,4727,4728,4729,4730,4731,4732,4733,4734,4735,4737,4738,4740,4767,4781) 'AccountManagement'
    $summary += Export-Events '11.5_USB_RemovableStorage' 'Security' @(6416,4663,4656) 'USB' { $_.Message -match 'Removable|USB|可移动|USBSTOR|Device' }
    $summary += Export-Events '11.5_USB_DriverFrameworks' 'Microsoft-Windows-DriverFrameworks-UserMode/Operational' $null 'USB'
    $summary += Export-Events '11.6_PrintService' 'Microsoft-Windows-PrintService/Operational' @(307,805,842,843) 'Print'
    $summary += Export-Events '11.7_AuditPolicy_System' 'Security' @(4719,1102,4616,4608,4609,4611,4621) 'Security'

    $summaryCsv=Join-Path $outDir 'AuditSummary.csv'
    $summary | Export-Csv -LiteralPath $summaryCsv -Encoding UTF8 -NoTypeInformation

    & "$PSScriptRoot\Generate-ComplianceMapping.ps1" -InputRunDirectory $outDir | Out-Null

    New-Result -Step '01_AuditLogV4' -Enabled $true -Status '已完成审计事件采集和报告生成' -Evidence @{OutputDirectory=$outDir; Summary=$summary; SummaryCsv=$summaryCsv}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '采集失败' -ErrorMessage $_.Exception.Message }
