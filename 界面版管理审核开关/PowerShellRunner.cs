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
