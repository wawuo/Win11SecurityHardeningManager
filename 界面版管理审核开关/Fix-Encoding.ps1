
# Fix-Encoding.ps1
# 用途：修复 Win11SecurityHardeningManager_V1 输出中文乱码问题
# 运行位置：请放在 Win11SecurityHardeningManager_V1 项目根目录执行
# 执行：powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Fix-Encoding.ps1

$ErrorActionPreference = 'Stop'

function Backup-File($Path) {
    if (Test-Path $Path) {
        $bak = "$Path.bak_$(Get-Date -Format yyyyMMdd_HHmmss)"
        Copy-Item $Path $bak -Force
        Write-Host "已备份: $bak" -ForegroundColor Cyan
    }
}

# 1) 修复 Scripts\Common.ps1：强制 PowerShell 输出 UTF-8
$commonPath = Join-Path $PSScriptRoot 'Scripts\Common.ps1'
if (!(Test-Path $commonPath)) { throw "找不到 $commonPath，请确认在项目根目录运行。" }
Backup-File $commonPath
$common = Get-Content $commonPath -Raw -Encoding UTF8
$encodingBlock = @'
# ===== UTF-8 输出修复：避免 C# 读取 PowerShell 中文乱码 =====
try {
    $script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $script:Utf8NoBom
    [Console]::InputEncoding  = $script:Utf8NoBom
    $OutputEncoding = $script:Utf8NoBom
} catch {}
# ==========================================================

'@
if ($common -notmatch 'UTF-8 输出修复') {
    $common = $encodingBlock + $common
    Set-Content -Path $commonPath -Value $common -Encoding UTF8
    Write-Host "已修复 Common.ps1 UTF-8 输出" -ForegroundColor Green
} else {
    Write-Host "Common.ps1 已包含 UTF-8 修复，跳过" -ForegroundColor Yellow
}

# 2) 替换 PowerShellRunner.cs：用 UTF-8 启动 PowerShell，并优先使用 pwsh，其次 powershell.exe
$runnerPath = Join-Path $PSScriptRoot 'PowerShellRunner.cs'
if (!(Test-Path $runnerPath)) { throw "找不到 $runnerPath，请确认在项目根目录运行。" }
Backup-File $runnerPath

$runner = @'
using System.Diagnostics;
using System.Text;

namespace Win11SecurityHardeningManager;

public static class PowerShellRunner
{
    public static async Task<(int ExitCode, string Output)> RunScriptAsync(string scriptPath, string actionName)
    {
        Directory.CreateDirectory("Logs");
        var logFile = Path.Combine("Logs", DateTime.Now.ToString("yyyyMMdd") + ".log");

        var psExe = FindPowerShell();
        var quotedScript = PsSingleQuote(scriptPath);

        // 关键：在 PowerShell 进程启动时就把控制台输出设置为 UTF-8。
        // 否则 Windows PowerShell 5.1 默认可能按 OEM/GBK 输出，C# 按 UTF-8 读取就会出现 ���。
        var command = "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "[Console]::InputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "$OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "& " + quotedScript;

        var psi = new ProcessStartInfo
        {
            FileName = psExe,
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + QuoteForCmdArg(command),
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        sb.AppendLine($"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {actionName} =====");
        sb.AppendLine(scriptPath);
        sb.AppendLine("PowerShell=" + psExe);

        p.Start();
        string stdout = await p.StandardOutput.ReadToEndAsync();
        string stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        sb.AppendLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine("[ERROR] " + stderr);
        sb.AppendLine($"ExitCode={p.ExitCode}");
        sb.AppendLine();
        await File.AppendAllTextAsync(logFile, sb.ToString(), new UTF8Encoding(false));

        return (p.ExitCode, stdout + Environment.NewLine + stderr);
    }

    private static string FindPowerShell()
    {
        // 优先 PowerShell 7，如果没有则使用 Windows PowerShell 5.1
        var pwsh = "pwsh.exe";
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = pwsh,
                Arguments = "-NoLogo -NoProfile -Command $PSVersionTable.PSVersion.ToString()",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p != null)
            {
                p.WaitForExit(1500);
                if (!p.HasExited || p.ExitCode == 0) return pwsh;
            }
        }
        catch { }

        return "powershell.exe";
    }

    private static string PsSingleQuote(string s)
    {
        return "'" + s.Replace("'", "''") + "'";
    }

    private static string QuoteForCmdArg(string s)
    {
        return "\"" + s.Replace("\"", "\\\"") + "\"";
    }
}
'@

Set-Content -Path $runnerPath -Value $runner -Encoding UTF8
Write-Host "已替换 PowerShellRunner.cs" -ForegroundColor Green

Write-Host "修复完成。请在 Visual Studio 中 Clean/Rebuild 后重新运行。" -ForegroundColor Green
Write-Host "建议：右键 Visual Studio -> 以管理员身份运行。" -ForegroundColor Yellow
