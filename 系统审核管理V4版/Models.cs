using System.Text.Json.Serialization;

namespace Win11SecurityHardeningManager;

public sealed class AppSettings
{
    public string AppName { get; set; } = "Windows 11 单机安全合规加固管理器 V5";
    public int AuditRetentionDays { get; set; } = 90;
    public string ArchiveRoot { get; set; } = @"C:\AuditArchive";
    public long SecurityLogMaxBytes { get; set; } = 1073741824;
    public long SystemLogMaxBytes { get; set; } = 268435456;
    public long ApplicationLogMaxBytes { get; set; } = 268435456;
    public List<string> AuditDirectories { get; set; } = new();
    public string FileAuditIdentity { get; set; } = "Everyone";
    public bool EnableHandleManipulationAudit { get; set; } = false;
    public int ExportLookbackDays { get; set; } = 1;
    public string ScheduledTaskName { get; set; } = "Win11AuditV5-DailyExport";
    public string ScheduledTaskTime { get; set; } = "23:30";
    public string AllowedDomain { get; set; } = "YOURDOMAIN";
    public List<string> AllowedDomainUsers { get; set; } = new();
    public int ScreenLockTimeoutSeconds { get; set; } = 900;
    public List<string> KeepLocalAdmins { get; set; } = new();
    public string BitLockerRecoveryKeyBackupPath { get; set; } = @"C:\BitLockerKeys";
    public string VulnerabilityScannerInstallerPath { get; set; } = @"C:\Tools\ScannerAgentSetup.exe";
    public string VulnerabilityScannerServiceName { get; set; } = "ScannerAgent";
}

public sealed class StepItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public bool HighRisk { get; init; }
}

public sealed class ActionResultEx
{
    public string Step { get; set; } = "";
    public bool Success { get; set; }
    public bool Enabled { get; set; }
    public string Status { get; set; } = "";
    public object? Evidence { get; set; }
    public string Error { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
}

public sealed class LogEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Level { get; set; } = "信息";
    public string Step { get; set; } = "";
    public string Message { get; set; } = "";
    public string Detail { get; set; } = "";
}
