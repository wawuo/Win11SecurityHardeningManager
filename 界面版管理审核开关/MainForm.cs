using System.Text;
using System.Text.Json;

namespace Win11SecurityHardeningManager;

public sealed class MainForm : Form
{
    private readonly DataGridView grid = new();
    private readonly TextBox output = new();
    private readonly Button btnCheckAll = new();
    private readonly Button btnEnableAll = new();
    private readonly Button btnReport = new();
    private readonly Label helpLabel = new();

    private readonly List<HardeningStep> steps = new()
    {
        new(){Id="01", Title="Windows 11 Pro 单机审计日志保留自动化工具 V4", ScriptFolder="Step01_AuditLog"},
        new(){Id="02", Title="限制其他域账号登录", ScriptFolder="Step02_LoginRestriction", HighRisk=true},
        new(){Id="03", Title="关闭默认共享如 C$", ScriptFolder="Step03_DisableAdminShares", HighRisk=true},
        new(){Id="04", Title="外加指纹启用 2FA 登录（人工/第三方 Credential Provider）", ScriptFolder="", HighRisk=true},
        new(){Id="05", Title="添加漏洞扫描服务", ScriptFolder="Step05_VulnerabilityScanner"},
        new(){Id="06", Title="关闭 USB 传输", ScriptFolder="Step06_USBBlock", HighRisk=true},
        new(){Id="07", Title="超时锁屏", ScriptFolder="Step07_AutoLock"},
        new(){Id="08", Title="开启 BitLocker 并将恢复密钥保存", ScriptFolder="Step08_BitLocker", HighRisk=true},
        new(){Id="09", Title="将本地管理员全部停用（保留白名单）", ScriptFolder="Step09_LocalAdmin", HighRisk=true},
    };

    public MainForm()
    {
        Text = "Windows 11 单机安全合规加固管理器 V1";
        Width = 1260;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45 };
        btnCheckAll.Text = "一键检查全部";
        btnEnableAll.Text = "一键执行全部加固";
        btnReport.Text = "生成 HTML 报告";
        btnCheckAll.Width = 130;
        btnEnableAll.Width = 160;
        btnReport.Width = 140;
        top.Controls.AddRange(new Control[]{btnCheckAll, btnEnableAll, btnReport});

        helpLabel.Dock = DockStyle.Top;
        helpLabel.Height = 78;
        helpLabel.Padding = new Padding(10, 6, 10, 6);
        helpLabel.BackColor = Color.FromArgb(255, 250, 225);
        helpLabel.BorderStyle = BorderStyle.FixedSingle;
        helpLabel.Text =
            "状态说明：\r\n" +
            "1）执行结果：表示 PowerShell 脚本是否正常运行完成，例如 成功/失败。\r\n" +
            "2）启用状态：表示该安全加固项当前是否已经生效，例如 已启用/未启用。\r\n" +
            "3）详细说明：脚本返回的具体说明，例如 检查完成、启用失败、已禁用 USB 存储等。";

        grid.Dock = DockStyle.Top;
        grid.Height = 390;
        grid.AllowUserToAddRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        grid.Columns.Add("Id", "步骤");
        grid.Columns.Add("Title", "项目");
        grid.Columns.Add("ExecResult", "执行结果");
        grid.Columns.Add("EnabledState", "启用状态");
        grid.Columns.Add("DetailStatus", "详细说明");
        AddButtonColumn("Check", "检查");
        AddButtonColumn("Enable", "启用");
        AddButtonColumn("Rollback", "回滚/禁用");

        foreach (var s in steps) grid.Rows.Add(s.Id, s.Title, "未执行", "未知", "未检查", "检查", "启用", "回滚");
        grid.Columns[0].Width = 55;
        grid.Columns[1].Width = 430;
        grid.Columns[2].Width = 90;
        grid.Columns[3].Width = 90;
        grid.Columns[4].Width = 260;

        output.Dock = DockStyle.Fill;
        output.Multiline = true;
        output.ScrollBars = ScrollBars.Both;
        output.Font = new Font("Consolas", 10);

        Controls.Add(output);
        Controls.Add(grid);
        Controls.Add(helpLabel);
        Controls.Add(top);

        grid.CellContentClick += Grid_CellContentClick;
        btnCheckAll.Click += async (_, _) => await RunAllAsync("Check");
        btnEnableAll.Click += async (_, _) => await RunAllAsync("Enable");
        btnReport.Click += (_, _) => GenerateHtmlReport();
    }

    private void AddButtonColumn(string name, string text)
    {
        var col = new DataGridViewButtonColumn { Name = name, HeaderText = text, Text = text, UseColumnTextForButtonValue = true };
        grid.Columns.Add(col);
    }

    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var colName = grid.Columns[e.ColumnIndex].Name;
        if (colName is not ("Check" or "Enable" or "Rollback")) return;
        await RunStepAsync(e.RowIndex, colName);
    }

    private async Task RunAllAsync(string action)
    {
        if (action == "Enable")
        {
            var r = MessageBox.Show("一键加固包含 USB 禁用、BitLocker、本地管理员停用等高风险项。确认继续？", "高风险确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }
        for (int i = 0; i < steps.Count; i++) await RunStepAsync(i, action);
    }

    private async Task RunStepAsync(int rowIndex, string action)
    {
        var step = steps[rowIndex];
        if (step.Id == "04")
        {
            grid.Rows[rowIndex].Cells["ExecResult"].Value = "跳过";
            grid.Rows[rowIndex].Cells["EnabledState"].Value = "人工确认";
            grid.Rows[rowIndex].Cells["DetailStatus"].Value = "需 Credential Provider / Windows Hello for Business / 第三方 MFA，普通脚本不能强制登录前 2FA";
            Append($"[04] {grid.Rows[rowIndex].Cells["DetailStatus"].Value}\r\n");
            return;
        }
        if (action != "Check" && step.HighRisk)
        {
            var r = MessageBox.Show($"确认对步骤 {step.Id} 执行 {action}？\r\n{step.Title}", "高风险确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        var scriptName = action == "Rollback" ? "Rollback.ps1" : action + ".ps1";
        var appRoot = FindAppRoot();
        var scriptPath = Path.Combine(appRoot, "Scripts", step.ScriptFolder, scriptName);
        if (!File.Exists(scriptPath))
        {
            grid.Rows[rowIndex].Cells["ExecResult"].Value = "失败";
            grid.Rows[rowIndex].Cells["EnabledState"].Value = "未知";
            grid.Rows[rowIndex].Cells["DetailStatus"].Value = "脚本不存在：" + scriptPath;
            return;
        }

        grid.Rows[rowIndex].Cells["ExecResult"].Value = "执行中";
        grid.Rows[rowIndex].Cells["EnabledState"].Value = "未知";
        grid.Rows[rowIndex].Cells["DetailStatus"].Value = "正在执行脚本...";

        var result = await PowerShellRunner.RunScriptAsync(scriptPath, $"{step.Id}-{action}");
        Append(result.Output + "\r\n");
        SetStatusFromJson(rowIndex, result.Output);
    }

    private void SetStatusFromJson(int rowIndex, string text)
    {
        try
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = text.Substring(start, end - start + 1);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var success = root.TryGetProperty("Success", out var s) && s.GetBoolean();
                var enabled = root.TryGetProperty("Enabled", out var e) && e.GetBoolean();
                var status = root.TryGetProperty("Status", out var st) ? st.GetString() : "已完成";
                var error = root.TryGetProperty("Error", out var er) ? er.GetString() : "";

                grid.Rows[rowIndex].Cells["ExecResult"].Value = success ? "成功" : "失败";
                grid.Rows[rowIndex].Cells["EnabledState"].Value = enabled ? "已启用" : "未启用";
                grid.Rows[rowIndex].Cells["DetailStatus"].Value = string.IsNullOrWhiteSpace(error) ? status : status + "；" + error;
                return;
            }
        }
        catch { }

        grid.Rows[rowIndex].Cells["ExecResult"].Value = "完成";
        grid.Rows[rowIndex].Cells["EnabledState"].Value = "未知";
        grid.Rows[rowIndex].Cells["DetailStatus"].Value = "未解析到 JSON，详见下方输出";
    }

    private string FindAppRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var scriptsDir = Path.Combine(dir.FullName, "Scripts");
            var configDir = Path.Combine(dir.FullName, "Config");
            if (Directory.Exists(scriptsDir) && Directory.Exists(configDir)) return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    private void Append(string s)
    {
        if (InvokeRequired) { Invoke(() => Append(s)); return; }
        output.AppendText(s + Environment.NewLine);
    }

    private void GenerateHtmlReport()
    {
        Directory.CreateDirectory("Reports");
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Compliance Report</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Microsoft YaHei,sans-serif}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:8px}</style></head><body>");
        sb.AppendLine("<h1>Windows 11 单机安全合规加固报告</h1>");
        sb.AppendLine($"<p>计算机：{Environment.MachineName}　时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("<h2>状态说明</h2><ul><li>执行结果：脚本是否正常运行完成。</li><li>启用状态：该安全加固项当前是否生效。</li><li>详细说明：脚本返回的具体说明或错误信息。</li></ul>");
        sb.AppendLine("<table><tr><th>步骤</th><th>项目</th><th>执行结果</th><th>启用状态</th><th>详细说明</th></tr>");
        foreach (DataGridViewRow row in grid.Rows)
            sb.AppendLine($"<tr><td>{row.Cells["Id"].Value}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["Title"].Value))}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["ExecResult"].Value))}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["EnabledState"].Value))}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["DetailStatus"].Value))}</td></tr>");
        sb.AppendLine("</table><h2>执行输出</h2><pre>");
        sb.AppendLine(System.Net.WebUtility.HtmlEncode(output.Text));
        sb.AppendLine("</pre></body></html>");
        var file = Path.Combine("Reports", $"ComplianceReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        MessageBox.Show("报告已生成：" + Path.GetFullPath(file));
    }
}
