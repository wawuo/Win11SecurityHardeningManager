using Microsoft.Win32;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Win11SecurityHardeningManager;

public static class ResultFactory
{
    public static ActionResultEx Ok(string step, bool enabled, string status, object? evidence = null) => new() { Step = step, Success = true, Enabled = enabled, Status = status, Evidence = evidence };
    public static ActionResultEx Fail(string step, string status, Exception ex, object? evidence = null) => new() { Step = step, Success = false, Enabled = false, Status = status, Error = ex.Message, Evidence = evidence };
}

public static class AdminShareManager
{
    private const string Step = "03_DisableAdminShares";
    public static async Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters");
            var v = key?.GetValue("AutoShareWks");
            var shares = await ProcessRunner.RunAsync("net.exe", "share");
            return ResultFactory.Ok(Step, Convert.ToString(v) == "0", "检查完成；AutoShareWks=0 表示持久禁止 C$/D$ 等盘符默认管理共享", new { AutoShareWks = v, NetShare = shares.Output + shares.Error });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "检查失败", ex); }
    }
    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters");
            key.SetValue("AutoShareWks", 0, RegistryValueKind.DWord);
            var before = await ProcessRunner.RunAsync("net.exe", "share");
            await RemoveDriveShareAsync("C$");
            await RemoveDriveShareAsync("D$");
            await RemoveDriveShareAsync("E$");
            var after = await ProcessRunner.RunAsync("net.exe", "share");
            return ResultFactory.Ok(Step, true, "已持久禁止 C$/D$/E$ 等盘符默认管理共享；ADMIN$/IPC$ 可能仍由系统维护", new { Before = before.Output, After = after.Output });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "启用失败", ex); }
    }
    public static Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters");
            key.DeleteValue("AutoShareWks", false);
            return Task.FromResult(ResultFactory.Ok(Step, false, "已删除 AutoShareWks；重启后 Windows 可重新创建盘符默认管理共享"));
        }
        catch (Exception ex) { return Task.FromResult(ResultFactory.Fail(Step, "回滚失败", ex)); }
    }
    private static Task RemoveDriveShareAsync(string name) => ProcessRunner.RunAsync("net.exe", $"share {name} /delete /y");
}

public static class UsbPolicyManager
{
    private const string Step = "06_USBBlock";
    private const string PathKey = @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices";
    public static Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.OpenSubKey(PathKey); var v = key?.GetValue("Deny_All"); return Task.FromResult(ResultFactory.Ok(Step, Convert.ToString(v) == "1", "检查完成", new { RegPath = @"HKLM\" + PathKey, Deny_All = v })); }
        catch (Exception ex) { return Task.FromResult(ResultFactory.Fail(Step, "检查失败", ex)); }
    }
    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.CreateSubKey(PathKey); key.SetValue("Deny_All", 1, RegistryValueKind.DWord); var gp = await ProcessRunner.RunAsync("gpupdate.exe", "/target:computer /force", 120000); return ResultFactory.Ok(Step, true, "已禁用所有可移动存储类访问", new { gpupdate = gp.Output + gp.Error }); }
        catch (Exception ex) { return ResultFactory.Fail(Step, "启用失败", ex); }
    }
    public static async Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.CreateSubKey(PathKey); key.DeleteValue("Deny_All", false); var gp = await ProcessRunner.RunAsync("gpupdate.exe", "/target:computer /force", 120000); return ResultFactory.Ok(Step, false, "已恢复可移动存储访问策略", new { gpupdate = gp.Output + gp.Error }); }
        catch (Exception ex) { return ResultFactory.Fail(Step, "回滚失败", ex); }
    }
}

public static class AutoLockManager
{
    private const string Step = "07_AutoLock";
    private const string PathKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    public static Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.OpenSubKey(PathKey); var v = key?.GetValue("InactivityTimeoutSecs"); return Task.FromResult(ResultFactory.Ok(Step, v is int i && i > 0, "检查完成", new { InactivityTimeoutSecs = v })); }
        catch (Exception ex) { return Task.FromResult(ResultFactory.Fail(Step, "检查失败", ex)); }
    }
    public static Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.CreateSubKey(PathKey); key.SetValue("InactivityTimeoutSecs", s.ScreenLockTimeoutSeconds, RegistryValueKind.DWord); return Task.FromResult(ResultFactory.Ok(Step, true, $"已设置空闲 {s.ScreenLockTimeoutSeconds} 秒自动锁屏", new { s.ScreenLockTimeoutSeconds })); }
        catch (Exception ex) { return Task.FromResult(ResultFactory.Fail(Step, "启用失败", ex)); }
    }
    public static Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try { using var key = Registry.LocalMachine.CreateSubKey(PathKey); key.DeleteValue("InactivityTimeoutSecs", false); return Task.FromResult(ResultFactory.Ok(Step, false, "已删除自动锁屏策略")); }
        catch (Exception ex) { return Task.FromResult(ResultFactory.Fail(Step, "回滚失败", ex)); }
    }
}

public static class LocalAdminManager
{
    private const string Step = "09_LocalAdmin";
    public static async Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try
        {
            var group = await GetAdministratorsGroupNameAsync();
            var members = await GetLocalGroupMembersAsync(group);
            var rows = new List<object>();
            var nonCompliant = 0;
            foreach (var m in members)
            {
                var shortName = ShortName(m);
                var isLocal = !m.Contains('\\') && await LocalUserExistsAsync(shortName);
                var enabled = isLocal ? await IsLocalUserEnabledAsync(shortName) : null;
                var keep = IsInKeep(s, m, shortName);
                if (isLocal && enabled == true && !keep) nonCompliant++;
                rows.Add(new { Name = m, ShortName = shortName, IsLocalUser = isLocal, Enabled = enabled, InKeepWhitelist = keep, WillDisableIfEnableClicked = isLocal && enabled == true && !keep });
            }
            return ResultFactory.Ok(Step, nonCompliant == 0, $"检查完成；发现 {nonCompliant} 个仍启用的非白名单本地管理员", new { AdministratorsGroup = group, KeepLocalAdmins = s.KeepLocalAdmins, Administrators = rows });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "检查失败", ex); }
    }
    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try
        {
            var group = await GetAdministratorsGroupNameAsync();
            var members = await GetLocalGroupMembersAsync(group);
            var disabled = new List<object>(); var kept = new List<object>(); var skipped = new List<object>(); var failed = new List<object>();
            foreach (var m in members)
            {
                var shortName = ShortName(m);
                var keep = IsInKeep(s, m, shortName);
                if (keep) { kept.Add(new { Name = m, Reason = "在 KeepLocalAdmins 白名单中" }); continue; }
                if (m.Contains('\\') || !await LocalUserExistsAsync(shortName)) { skipped.Add(new { Name = m, Reason = "不是本地用户；跳过域用户/域组/AzureAD/MicrosoftAccount" }); continue; }
                var enabled = await IsLocalUserEnabledAsync(shortName);
                if (enabled == false) { skipped.Add(new { Name = m, LocalUser = shortName, Reason = "本地用户已经禁用" }); continue; }
                var r = await ProcessRunner.RunAsync("net.exe", $"user \"{shortName}\" /active:no");
                if (r.ExitCode == 0) disabled.Add(new { Name = m, LocalUser = shortName, Result = "已禁用" });
                else failed.Add(new { Name = m, LocalUser = shortName, Error = r.Output + r.Error });
            }
            var check = await CheckAsync(s);
            return ResultFactory.Ok(Step, check.Enabled && failed.Count == 0, $"本地管理员处理完成；成功禁用 {disabled.Count} 个，保留 {kept.Count} 个，跳过 {skipped.Count} 个，失败 {failed.Count} 个", new { AdministratorsGroup = group, KeepLocalAdmins = s.KeepLocalAdmins, Disabled = disabled, Kept = kept, Skipped = skipped, Failed = failed, Verify = check.Evidence });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "启用失败", ex); }
    }
    public static Task<ActionResultEx> RollbackAsync(AppSettings s) => Task.FromResult(ResultFactory.Ok(Step, false, "本模块不自动重新启用账号；如需恢复请手动执行：net user 用户名 /active:yes", new { Example = "net user admin /active:yes" }));

    private static bool IsInKeep(AppSettings s, string full, string shortName) => s.KeepLocalAdmins.Any(k => string.Equals(k, shortName, StringComparison.OrdinalIgnoreCase) || string.Equals(k, full, StringComparison.OrdinalIgnoreCase));
    private static string ShortName(string name) => name.Split('\\').Last().Trim();
    private static async Task<string> GetAdministratorsGroupNameAsync()
    {
        var r = await ProcessRunner.RunAsync("wmic.exe", "group where sid='S-1-5-32-544' get name /value");
        foreach (var line in (r.Output + r.Error).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)) return line[5..].Trim();
        return "Administrators";
    }
    private static async Task<List<string>> GetLocalGroupMembersAsync(string group)
    {
        var r = await ProcessRunner.RunAsync("net.exe", $"localgroup \"{group}\"");
        var result = new List<string>(); var inList = false;
        foreach (var raw in (r.Output + r.Error).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("---")) { inList = true; continue; }
            if (!inList) continue;
            if (line.Contains("command completed", StringComparison.OrdinalIgnoreCase) || line.Contains("命令成功完成") || line.Contains("成功完成")) break;
            if (!string.IsNullOrWhiteSpace(line)) result.Add(line);
        }
        return result;
    }
    private static async Task<bool> LocalUserExistsAsync(string name)
    {
        var r = await ProcessRunner.RunAsync("net.exe", $"user \"{name}\"");
        return r.ExitCode == 0;
    }
    private static async Task<bool?> IsLocalUserEnabledAsync(string name)
    {
        var r = await ProcessRunner.RunAsync("net.exe", $"user \"{name}\"");
        if (r.ExitCode != 0) return null;
        var text = r.Output + r.Error;
        if (text.Contains("Account active", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (line.Contains("Account active", StringComparison.OrdinalIgnoreCase)) return line.Contains("Yes", StringComparison.OrdinalIgnoreCase);
        }
        if (text.Contains("帐户启用") || text.Contains("帐户活动") || text.Contains("账户启用") || text.Contains("账户活动"))
        {
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (line.Contains("帐户") || line.Contains("账户"))
                {
                    if (line.Contains("Yes", StringComparison.OrdinalIgnoreCase) || line.Contains("是")) return true;
                    if (line.Contains("No", StringComparison.OrdinalIgnoreCase) || line.Contains("否")) return false;
                }
        }
        return null;
    }
}

public static class AuditV5Manager
{
    private const string Step = "01_AuditLogV5";
    public static async Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try
        {
            var sec = await ProcessRunner.RunAsync("wevtutil.exe", "gl Security");
            var audit = await ProcessRunner.RunAsync("auditpol.exe", "/get /category:*");
            var dirs = s.AuditDirectories.Select(d => new
            {
                Path = d,
                Exists = Directory.Exists(d),
                AuditRuleCount = Directory.Exists(d) ? (new DirectoryInfo(d)).GetAccessControl()
    .GetAuditRules(true, true, typeof(NTAccount)).Count : 0
            }).ToList();
            var archive = Directory.Exists(s.ArchiveRoot);
            var enabled = archive && dirs.Any(x => x.Exists && x.AuditRuleCount > 0);
            return ResultFactory.Ok(Step, enabled, enabled ? "检查完成；V5 审计基础配置已启用" : "检查完成；未完全启用，请先执行 01 启用", new { SecurityLog = sec.Output + sec.Error, AuditPolicy = audit.Output + audit.Error, ArchiveRoot = s.ArchiveRoot, ArchiveRootExists = archive, AuditDirectories = dirs });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "检查失败", ex); }
    }
    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(s.ArchiveRoot);
            await ProcessRunner.RunAsync("wevtutil.exe", $"sl Security /ms:{s.SecurityLogMaxBytes}");
            await ProcessRunner.RunAsync("wevtutil.exe", $"sl System /ms:{s.SystemLogMaxBytes}");
            await ProcessRunner.RunAsync("wevtutil.exe", $"sl Application /ms:{s.ApplicationLogMaxBytes}");
            var cmds = new List<string> { "Logon", "Logoff", "Account Lockout", "User Account Management", "Security Group Management", "Audit Policy Change", "File System", "File Share", "Detailed File Share", "Removable Storage" };
            var auditOut = new List<object>();
            foreach (var c in cmds) auditOut.Add(new { Subcategory = c, Result = await ProcessRunner.RunAsync("auditpol.exe", $"/set /subcategory:\"{c}\" /success:enable /failure:enable") });
            var hm = s.EnableHandleManipulationAudit ? "enable" : "disable";
            auditOut.Add(new { Subcategory = "Handle Manipulation", Result = await ProcessRunner.RunAsync("auditpol.exe", $"/set /subcategory:\"Handle Manipulation\" /success:{hm} /failure:{hm}") });
            await ProcessRunner.RunAsync("wevtutil.exe", "sl Microsoft-Windows-PrintService/Operational /e:true");
            await ProcessRunner.RunAsync("wevtutil.exe", "sl Microsoft-Windows-TerminalServices-LocalSessionManager/Operational /e:true");
            await ProcessRunner.RunAsync("wevtutil.exe", "sl Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational /e:true");
            var sacl = new List<object>();
            foreach (var d in s.AuditDirectories.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                Directory.CreateDirectory(d);
                var di = new DirectoryInfo(d);
                var ds = di.GetAccessControl();
                var rule = new FileSystemAuditRule(new NTAccount(s.FileAuditIdentity), FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AuditFlags.Success | AuditFlags.Failure);
                ds.AddAuditRule(rule);
                di.SetAccessControl(ds);
                sacl.Add(new { Path = d, Identity = s.FileAuditIdentity, Rights = rule.FileSystemRights.ToString() });
            }
            return ResultFactory.Ok(Step, true, "已启用 V5 审计策略、事件日志和文件目录 SACL", new { AuditPolicy = auditOut, SACL = sacl, ArchiveRoot = s.ArchiveRoot });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "启用失败", ex); }
    }
    public static async Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try
        {
            foreach (var c in new[] { "File System", "File Share", "Detailed File Share", "Handle Manipulation", "Removable Storage" })
                await ProcessRunner.RunAsync("auditpol.exe", $"/set /subcategory:\"{c}\" /success:disable /failure:disable");
            return ResultFactory.Ok(Step, false, "已关闭文件/共享/可移动存储/句柄审计；登录和账户管理审计未关闭，避免降低安全基线");
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "回滚失败", ex); }
    }
    public static async Task<ActionResultEx> ExportAsync(AppSettings s)
    {
        try
        {
            var runDir = Path.Combine(s.ArchiveRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(runDir);
            var start = DateTime.Now.AddDays(-s.ExportLookbackDays);
            var groups = new Dictionary<string, int[]>
            {
                ["11.1_LogonLogoff"] = new[] { 4624, 4625, 4634, 4647, 4800, 4801, 4740 },
                ["11.2_FileAccess"] = new[] { 4663, 4660, 4670, 4659, 5140, 5145 },
                ["11.4_AccountManagement"] = new[] { 4720, 4722, 4723, 4724, 4725, 4726, 4738, 4740, 4767, 4781 },
                ["11.7_SecurityPolicy"] = new[] { 4719, 1102, 4616 }
            };
            var summary = new List<object>();
            foreach (var g in groups)
            {
                var ids = string.Join(",", g.Value);
                var file = Path.Combine(runDir, g.Key + ".csv");
                var cmd = $"/c wevtutil qe Security /q:\"*[System[TimeCreated[timediff(@SystemTime) <= {s.ExportLookbackDays * 24 * 60 * 60 * 1000}] and ({string.Join(" or ", g.Value.Select(i => "EventID=" + i))})]]\" /f:text > \"{file}\"";
                var r = await ProcessRunner.RunAsync("cmd.exe", cmd, 180000);
                summary.Add(new { Name = g.Key, File = file, ExitCode = r.ExitCode });
            }
            var html = Path.Combine(runDir, "ComplianceMapping.html");
            var auditDirs = string.Join("", s.AuditDirectories.Select(x => $"<li><code>{System.Net.WebUtility.HtmlEncode(x)}</code></li>"));
            File.WriteAllText(html, $"<html><head><meta charset='utf-8'><style>body{{font-family:Segoe UI,Microsoft YaHei}}table{{border-collapse:collapse}}td,th{{border:1px solid #ccc;padding:6px}}</style></head><body><h1>V5 审计合规映射</h1><p>生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p><h2>11.2 文件访问日志</h2><p>当前审核目录：</p><ul>{auditDirs}</ul><p>输出目录：<code>{runDir}</code></p></body></html>", Encoding.UTF8);
            return ResultFactory.Ok(Step, true, "已完成审计采集并生成 ComplianceMapping.html", new { OutputDirectory = runDir, Summary = summary, ComplianceMapping = html });
        }
        catch (Exception ex) { return ResultFactory.Fail(Step, "采集失败", ex); }
    }
}

public static class BitLockerManager
{
    private const string Step = "08_BitLocker";
    public static async Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try { var r = await ProcessRunner.RunAsync("manage-bde.exe", "-status C:"); var enabled = r.Output.Contains("Protection On", StringComparison.OrdinalIgnoreCase) || r.Output.Contains("保护已启用"); return ResultFactory.Ok(Step, enabled, "检查完成", new { Output = r.Output + r.Error }); }
        catch (Exception ex) { return ResultFactory.Fail(Step, "检查失败", ex); }
    }
    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try { Directory.CreateDirectory(s.BitLockerRecoveryKeyBackupPath); var on = await ProcessRunner.RunAsync("manage-bde.exe", "-on C: -RecoveryPassword -UsedSpaceOnly"); var prot = await ProcessRunner.RunAsync("manage-bde.exe", "-protectors -get C:"); var file = Path.Combine(s.BitLockerRecoveryKeyBackupPath, Environment.MachineName + "-C-BitLockerRecovery.txt"); File.WriteAllText(file, prot.Output + prot.Error, Encoding.UTF8); return ResultFactory.Ok(Step, true, "已调用 manage-bde 启用/检查 BitLocker，并导出恢复密码信息", new { EnableOutput = on.Output + on.Error, BackupFile = file }); }
        catch (Exception ex) { return ResultFactory.Fail(Step, "启用失败", ex); }
    }
    public static async Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try { var r = await ProcessRunner.RunAsync("manage-bde.exe", "-off C:"); return ResultFactory.Ok(Step, false, "已发起 BitLocker 解密，完成需要时间", new { Output = r.Output + r.Error }); }
        catch (Exception ex) { return ResultFactory.Fail(Step, "回滚失败", ex); }
    }
}

public static class VulnerabilityScannerManager
{
    private const string Step = "05_VulnerabilityScanner";

    public static async Task<ActionResultEx> CheckAsync(AppSettings s)
    {
        try
        {
            var query = await ProcessRunner.RunAsync("sc.exe", $"query \"{s.VulnerabilityScannerServiceName}\"");
            var text = query.Output + query.Error;
            var exists = query.ExitCode == 0 && !text.Contains("FAILED", StringComparison.OrdinalIgnoreCase) && !text.Contains("失败");
            var running = exists && (text.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) || text.Contains("正在运行"));

            return ResultFactory.Ok(
                Step,
                running,
                exists ? (running ? "检查完成；服务正在运行" : "检查完成；服务存在但未运行") : "检查完成；服务不存在",
                new
                {
                    Service = s.VulnerabilityScannerServiceName,
                    Exists = exists,
                    Running = running,
                    Output = text
                });
        }
        catch (Exception ex)
        {
            return ResultFactory.Fail(Step, "检查失败", ex);
        }
    }

    public static async Task<ActionResultEx> EnableAsync(AppSettings s)
    {
        try
        {
            object? installerResult = null;

            if (File.Exists(s.VulnerabilityScannerInstallerPath))
            {
                var install = await ProcessRunner.RunAsync(s.VulnerabilityScannerInstallerPath, "/quiet /norestart", 300000);
                installerResult = new
                {
                    Installer = s.VulnerabilityScannerInstallerPath,
                    install.ExitCode,
                    Output = install.Output + install.Error
                };
            }

            var start = await ProcessRunner.RunAsync("sc.exe", $"start \"{s.VulnerabilityScannerServiceName}\"", 120000);
            var check = await CheckAsync(s);

            return ResultFactory.Ok(
                Step,
                check.Enabled,
                check.Enabled ? "安装/启动动作已执行；服务正在运行" : "安装/启动动作已执行；但服务未确认运行，请查看详细输出",
                new
                {
                    InstallerResult = installerResult,
                    StartOutput = start.Output + start.Error,
                    Verify = check.Evidence
                });
        }
        catch (Exception ex)
        {
            return ResultFactory.Fail(Step, "启用失败", ex);
        }
    }

    public static async Task<ActionResultEx> RollbackAsync(AppSettings s)
    {
        try
        {
            var stop = await ProcessRunner.RunAsync("sc.exe", $"stop \"{s.VulnerabilityScannerServiceName}\"", 120000);
            return ResultFactory.Ok(
                Step,
                false,
                "已尝试停止漏洞扫描服务；卸载需补充厂商静默卸载参数",
                new
                {
                    Service = s.VulnerabilityScannerServiceName,
                    StopOutput = stop.Output + stop.Error
                });
        }
        catch (Exception ex)
        {
            return ResultFactory.Fail(Step, "回滚失败", ex);
        }
    }
}

