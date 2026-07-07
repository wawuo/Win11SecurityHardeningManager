. "$PSScriptRoot\..\Common.ps1"
try{
    $tmp=Join-Path $env:TEMP 'secpol_check.inf'
    secedit /export /cfg $tmp | Out-Null
    $txt=Get-Content $tmp -Raw -Encoding Unicode
    New-Result -Step '02_LoginRestriction' -Enabled ($txt -match 'SeDenyInteractiveLogonRight') -Status '检查完成' -Evidence $txt
}catch{ New-Result -Step '02_LoginRestriction' -Success $false -Status '检查失败' -ErrorMessage $_.Exception.Message }
