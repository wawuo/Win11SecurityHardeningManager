using System.Diagnostics;
using System.Text;

namespace Win11SecurityHardeningManager;

public static class PowerShellRunner
{
    public static async Task<(int ExitCode, string Output)> RunScriptAsync(string scriptPath, string actionName, string appRoot)
    {
        Directory.CreateDirectory(Path.Combine(appRoot, "Logs"));
        var logFile = Path.Combine(appRoot, "Logs", DateTime.Now.ToString("yyyyMMdd") + ".log");
        var quotedScript = PsSingleQuote(scriptPath);
        var command = "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "[Console]::InputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "$OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
                      "& " + quotedScript;
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + QuoteForCmdArg(command),
            WorkingDirectory = appRoot,
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
        p.Start();
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        sb.AppendLine(stdout);
        if(!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine("[ERROR] " + stderr);
        sb.AppendLine("ExitCode=" + p.ExitCode);
        await File.AppendAllTextAsync(logFile, sb.ToString(), new UTF8Encoding(false));
        return (p.ExitCode, stdout + Environment.NewLine + stderr);
    }
    private static string PsSingleQuote(string s) => "'" + s.Replace("'", "''") + "'";
    private static string QuoteForCmdArg(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
}
