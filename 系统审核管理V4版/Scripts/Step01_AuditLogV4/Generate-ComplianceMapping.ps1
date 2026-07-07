param([string]$InputRunDirectory='')
. "$PSScriptRoot\..\Common.ps1"
try {
    $s=Read-Settings
    $root=Get-ArchiveRoot
    if([string]::IsNullOrWhiteSpace($InputRunDirectory)){ $InputRunDirectory=$root }
    Ensure-Directory $InputRunDirectory
    $auditDirs=@()
    foreach($dir in @($s.AuditDirectories)){
        $auditDirs += "<li><code>$([System.Net.WebUtility]::HtmlEncode($dir))</code></li>"
    }
    $summaryPath=Join-Path $InputRunDirectory 'AuditSummary.csv'
    $rows=@()
    if(Test-Path -LiteralPath $summaryPath){ $rows=Import-Csv -LiteralPath $summaryPath -Encoding UTF8 }
    $trs=''
    foreach($r in $rows){ $trs += "<tr><td>$($r.Name)</td><td>$($r.Count)</td><td><code>$([System.Net.WebUtility]::HtmlEncode($r.Csv))</code></td></tr>`n" }
    $html=@"
<html><head><meta charset='utf-8'><title>ComplianceMapping V4</title>
<style>body{font-family:'Segoe UI','Microsoft YaHei',sans-serif}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:6px}code{background:#f5f5f5;padding:2px}</style></head><body>
<h1>Windows 11 Pro 单机审计日志保留自动化工具 V4 - 合规映射</h1>
<p>计算机：$env:COMPUTERNAME　生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')　保留要求：$($s.AuditRetentionDays) 天</p>
<h2>11.1 系统登录/注销日志</h2>
<p>覆盖事件：4624 登录成功、4625 登录失败、4634/4647 注销、4800/4801 锁定/解锁、4740 账户锁定。</p>
<h2>11.2 文件访问日志</h2>
<p>覆盖事件：4663 文件访问、4660 删除、4670 权限变更、5140/5145 共享访问。</p>
<p><b>当前已配置文件访问审核的目录：</b></p><ul>$($auditDirs -join "`n")</ul>
<h2>11.3 远程访问日志</h2>
<p>覆盖 Security 4624/4625 中 LogonType 10/7/3，以及 TerminalServices 21/24/25/1149 等事件。</p>
<h2>11.4 账户管理日志</h2>
<p>覆盖 4720-4781 范围内用户、组、密码、启停用、锁定相关事件。</p>
<h2>11.5 USB / 可移动存储日志</h2>
<p>覆盖 Removable Storage 对象访问、设备安装 6416、DriverFrameworks-UserMode Operational 等。</p>
<h2>11.6 打印日志</h2>
<p>覆盖 Microsoft-Windows-PrintService/Operational，例如 307 打印文档事件。</p>
<h2>导出摘要</h2><table><tr><th>类别</th><th>记录数</th><th>CSV</th></tr>$trs</table>
</body></html>
"@
    $file=Join-Path $InputRunDirectory 'ComplianceMapping.html'
    $html | Out-File -LiteralPath $file -Encoding UTF8
    New-Result -Step '01_AuditLogV4' -Enabled $true -Status '已生成 ComplianceMapping.html' -Evidence @{File=$file; AuditDirectories=$s.AuditDirectories}
} catch { New-Result -Step '01_AuditLogV4' -Success $false -Enabled $false -Status '生成合规映射失败' -ErrorMessage $_.Exception.Message }
