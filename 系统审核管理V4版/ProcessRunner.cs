using System.Diagnostics;
using System.Text;

namespace Win11SecurityHardeningManager;

public static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(string fileName, string arguments, int timeoutMs = 120000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true

            // 重要：不要强制 StandardOutputEncoding = Encoding.UTF8。
            // manage-bde.exe / net.exe / sc.exe / auditpol.exe 等 Windows 控制台工具
            // 在中文 Windows 上通常按 OEM 代码页输出，例如 CP936。
            // 强制 UTF-8 会出现：锟斤拷、��� 等乱码。
            // 这里保持 null，让 .NET 使用系统默认控制台编码读取。
        };

        using var p = new Process { StartInfo = psi };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) sbOut.AppendLine(e.Data);
        };

        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) sbErr.AppendLine(e.Data);
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!await Task.Run(() => p.WaitForExit(timeoutMs)))
        {
            try { p.Kill(true); } catch { }
            return (-1, sbOut.ToString(), "执行超时：" + fileName + " " + arguments);
        }

        return (p.ExitCode, sbOut.ToString(), sbErr.ToString());
    }
}
