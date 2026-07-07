#requires -version 5.1
<# Common.ps1 - V4 Integrated #>
try {
    $script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $script:Utf8NoBom
    [Console]::InputEncoding  = $script:Utf8NoBom
    $OutputEncoding = $script:Utf8NoBom
} catch {}

function New-Result {
    param(
        [string]$Step,
        [bool]$Success=$true,
        [bool]$Enabled=$false,
        [string]$Status='OK',
        [object]$Evidence=$null,
        [string]$ErrorMessage=''
    )
    [PSCustomObject]@{
        Step=$Step
        Success=$Success
        Enabled=$Enabled
        Status=$Status
        Evidence=$Evidence
        Error=$ErrorMessage
        ComputerName=$env:COMPUTERNAME
        User=$env:USERNAME
        Time=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    } | ConvertTo-Json -Depth 12
}
function Test-IsAdmin {
    $id=[Security.Principal.WindowsIdentity]::GetCurrent()
    $p=New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Require-Admin {
    if(-not (Test-IsAdmin)){ throw '需要管理员权限运行。请右键以管理员身份运行主程序或 Visual Studio。' }
}
function Get-AppRoot {
    $dir = Get-Item -LiteralPath $PSScriptRoot
    while($null -ne $dir){
        if((Test-Path (Join-Path $dir.FullName 'Config\settings.json')) -and (Test-Path (Join-Path $dir.FullName 'Scripts'))){ return $dir.FullName }
        $dir=$dir.Parent
    }
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
function Read-Settings {
    $root=Get-AppRoot
    $p=Join-Path $root 'Config\settings.json'
    if(!(Test-Path -LiteralPath $p)){ throw "settings.json not found: $p" }
    return Get-Content -LiteralPath $p -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Get-ArchiveRoot {
    $s=Read-Settings
    if([string]::IsNullOrWhiteSpace($s.ArchiveRoot)){ return (Join-Path (Get-AppRoot) 'Reports') }
    return $s.ArchiveRoot
}
function Ensure-Directory([string]$Path){ if(!(Test-Path -LiteralPath $Path)){ New-Item -ItemType Directory -Path $Path -Force | Out-Null } }
function Set-AuditPolSafe([string]$Subcategory,[string]$Success='enable',[string]$Failure='enable'){
    $out = auditpol /set /subcategory:"$Subcategory" /success:$Success /failure:$Failure 2>&1
    return ($out -join "`n")
}
function Get-AuditPolText {
    return ((auditpol /get /category:* 2>&1) -join "`n")
}
function Enable-EventLogIfExists([string]$LogName){
    try { wevtutil sl $LogName /e:true | Out-Null; return $true } catch { return $false }
}
function Set-EventLogMaxIfExists([string]$LogName,[int64]$MaxBytes){
    try { wevtutil sl $LogName /ms:$MaxBytes | Out-Null; return $true } catch { return $false }
}
