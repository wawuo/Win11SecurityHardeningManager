# ===== UTF-8 输出修复：避免 C# 读取 PowerShell 中文乱码 =====
try {
    $script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $script:Utf8NoBom
    [Console]::InputEncoding  = $script:Utf8NoBom
    $OutputEncoding = $script:Utf8NoBom
} catch {}
# ==========================================================
#requires -version 5.1
<#
通用函数库 - Win11SecurityHardeningManager V1
注意：请以管理员运行。所有脚本输出统一 JSON，方便 C# 界面解析。
#>
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
    } | ConvertTo-Json -Depth 8
}
function Test-IsAdmin {
    $id=[Security.Principal.WindowsIdentity]::GetCurrent()
    $p=New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Read-Settings {
    $p=Join-Path $PSScriptRoot '..\..\Config\settings.json'
    if(!(Test-Path $p)){ throw "settings.json not found: $p" }
    return Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Require-Admin {
    if(-not (Test-IsAdmin)){ throw '需要管理员权限运行。请右键以管理员身份运行主程序。' }
}

