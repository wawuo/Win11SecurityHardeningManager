using System.Text;
using System.Text.Json;

namespace Win11SecurityHardeningManager;

public sealed class MainForm : Form
{
    private readonly DataGridView grid = new();
    private readonly DataGridView logGrid = new();
    private readonly TextBox detailBox = new();
    private readonly Label help = new();
    private readonly List<LogEntry> logs = new();
    private bool adjustingWidth;

    private readonly List<StepItem> steps = new()
    {
        new(){Id="01", Title="V5 审计日志保留和采集（C# + Windows CLI，无 PS1）"},
        new(){Id="02", Title="限制其他域账号登录（说明/半自动）", HighRisk=true},
        new(){Id="03", Title="关闭默认共享 C$/D$/E$", HighRisk=true},
        new(){Id="04", Title="指纹 2FA 登录说明项", HighRisk=true},
        new(){Id="05", Title="漏洞扫描服务"},
        new(){Id="06", Title="关闭 USB 传输", HighRisk=true},
        new(){Id="07", Title="超时锁屏"},
        new(){Id="08", Title="BitLocker", HighRisk=true},
        new(){Id="09", Title="本地管理员停用（保留白名单）", HighRisk=true},
    };

    public MainForm()
    {
        Text = "Windows 11 单机安全合规加固管理器 V5 - C# Native";
        Width = 1320; Height = 850; StartPosition = FormStartPosition.CenterScreen;
        var top = new FlowLayoutPanel{ Dock = DockStyle.Top, Height = 42 };
        var btnCheckAll = Btn("一键检查", 90); var btnEnableAll = Btn("一键加固", 90); var btnExportAudit = Btn("立即采集审计", 110); var btnReport = Btn("导出报告", 90); var btnClearLog = Btn("清空日志", 90);
        top.Controls.AddRange(new Control[]{btnCheckAll, btnEnableAll, btnExportAudit, btnReport, btnClearLog});
        help.Dock = DockStyle.Top; help.Height = 72; help.Padding = new Padding(8); help.BackColor = Color.FromArgb(245,250,255); help.BorderStyle=BorderStyle.FixedSingle;
        help.Text = "V5说明：能改成 C# 的已改为 C# 直接处理；auditpol/wevtutil/manage-bde/net/schtasks 这类系统功能由 C# 直接调用，不再通过 PS1。\r\n"+
                    "日志区更直观：下面按 时间/级别/步骤/消息 展示，点击日志行可在右侧/下方查看完整 JSON 细节。列宽限制为最小20、最大100，避免拖拽变形。";

        grid.Dock = DockStyle.Top; grid.Height = 310; InitGrid(grid);
        grid.Columns.Add("Id", "步骤"); grid.Columns.Add("Title", "项目"); grid.Columns.Add("Exec", "执行"); grid.Columns.Add("Enabled", "状态"); grid.Columns.Add("Msg", "说明");
        AddBtnCol("Check", "检查"); AddBtnCol("Enable", "启用"); AddBtnCol("Rollback", "回滚");
        foreach (var s in steps) grid.Rows.Add(s.Id, s.Title, "未执行", "未知", "未检查", "检查", "启用", "回滚");
        ApplyColumnLimits(grid);

        var split = new SplitContainer{ Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
        logGrid.Dock = DockStyle.Fill; InitGrid(logGrid);
        logGrid.Columns.Add("Time", "时间"); logGrid.Columns.Add("Level", "级别"); logGrid.Columns.Add("Step", "步骤"); logGrid.Columns.Add("Message", "消息");
        ApplyColumnLimits(logGrid);
        detailBox.Dock = DockStyle.Fill; detailBox.Multiline = true; detailBox.ScrollBars = ScrollBars.Both; detailBox.Font = new Font("Consolas", 10);
        split.Panel1.Controls.Add(logGrid); split.Panel2.Controls.Add(detailBox);

        Controls.Add(split); Controls.Add(grid); Controls.Add(help); Controls.Add(top);
        grid.CellContentClick += Grid_CellContentClick;
        grid.ColumnWidthChanged += LimitColumnWidth;
        logGrid.ColumnWidthChanged += LimitColumnWidth;
        logGrid.SelectionChanged += (_,_) => ShowSelectedLogDetail();
        btnCheckAll.Click += async (_,_) => { for(int i=0;i<steps.Count;i++) await RunStep(i,"Check"); };
        btnEnableAll.Click += async (_,_) => { if(Confirm("一键加固包含 USB、BitLocker、本地管理员等高风险项，确认继续？")) for(int i=0;i<steps.Count;i++) await RunStep(i,"Enable"); };
        btnExportAudit.Click += async (_,_) => await RunAuditExport();
        btnReport.Click += (_,_) => ExportUiReport();
        btnClearLog.Click += (_,_) => { logs.Clear(); logGrid.Rows.Clear(); detailBox.Clear(); };
    }

    private static Button Btn(string text, int w) => new(){ Text=text, Width=w, Height=28 };
    private void InitGrid(DataGridView g)
    {
        g.AllowUserToAddRows=false; g.RowHeadersVisible=false; g.SelectionMode=DataGridViewSelectionMode.FullRowSelect; g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
        g.DefaultCellStyle.WrapMode=DataGridViewTriState.False; g.MultiSelect=false; g.ReadOnly=false;
    }
    private void AddBtnCol(string name,string text) => grid.Columns.Add(new DataGridViewButtonColumn{Name=name,HeaderText=text,Text=text,UseColumnTextForButtonValue=true});
    private void ApplyColumnLimits(DataGridView g)
    {
        foreach(DataGridViewColumn c in g.Columns){ c.MinimumWidth = 20; c.Width = Math.Max(20, Math.Min(100, c.Width)); }
    }
    private void LimitColumnWidth(object? sender, DataGridViewColumnEventArgs e)
    {
        if(adjustingWidth) return;
        adjustingWidth=true;
        if(e.Column.Width < 20) e.Column.Width=20;
        if(e.Column.Width > 500) e.Column.Width=500;
        adjustingWidth=false;
    }
    private bool Confirm(string msg) => MessageBox.Show(msg,"确认",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes;

    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if(e.RowIndex<0) return; var col=grid.Columns[e.ColumnIndex].Name;
        if(col is "Check" or "Enable" or "Rollback") await RunStep(e.RowIndex, col);
    }

    private async Task RunStep(int row, string action)
    {
        var step = steps[row];
        if(action != "Check" && step.HighRisk && !Confirm($"确认执行 {step.Id} {step.Title} 的 {action}？")) return;
        SetRow(row,"执行中","未知","正在执行...");
        AddLog("信息", step.Id, $"开始执行 {action}: {step.Title}", "");
        var settings = ConfigManager.Load();
        ActionResultEx result;
        try
        {
            result = step.Id switch
            {
                "01" => action switch { "Check" => await AuditV5Manager.CheckAsync(settings), "Enable" => await AuditV5Manager.EnableAsync(settings), _ => await AuditV5Manager.RollbackAsync(settings) },
                "02" => ResultFactory.Ok("02_LoginRestriction", false, "为避免锁死，此项保留为说明/半自动：建议用本地安全策略或域策略精确配置 Allow/Deny logon rights。"),
                "03" => action switch { "Check" => await AdminShareManager.CheckAsync(settings), "Enable" => await AdminShareManager.EnableAsync(settings), _ => await AdminShareManager.RollbackAsync(settings) },
                "04" => ResultFactory.Ok("04_Fingerprint2FA", false, "登录前密码+指纹强制 2FA 需要 Credential Provider、Windows Hello for Business 或第三方 MFA，普通 C# 程序不能严谨插入登录链路。"),
                "05" => action switch { "Check" => await VulnerabilityScannerManager.CheckAsync(settings), "Enable" => await VulnerabilityScannerManager.EnableAsync(settings), _ => await VulnerabilityScannerManager.RollbackAsync(settings) },
                "06" => action switch { "Check" => await UsbPolicyManager.CheckAsync(settings), "Enable" => await UsbPolicyManager.EnableAsync(settings), _ => await UsbPolicyManager.RollbackAsync(settings) },
                "07" => action switch { "Check" => await AutoLockManager.CheckAsync(settings), "Enable" => await AutoLockManager.EnableAsync(settings), _ => await AutoLockManager.RollbackAsync(settings) },
                "08" => action switch { "Check" => await BitLockerManager.CheckAsync(settings), "Enable" => await BitLockerManager.EnableAsync(settings), _ => await BitLockerManager.RollbackAsync(settings) },
                "09" => action switch { "Check" => await LocalAdminManager.CheckAsync(settings), "Enable" => await LocalAdminManager.EnableAsync(settings), _ => await LocalAdminManager.RollbackAsync(settings) },
                _ => ResultFactory.Ok("Unknown", false, "未知步骤")
            };
        }
        catch(Exception ex) { result = ResultFactory.Fail(step.Id, "执行异常", ex); }
        SetRow(row, result.Success?"成功":"失败", result.Enabled?"已启用":"未启用", result.Status);
        AddLog(result.Success?"成功":"失败", step.Id, result.Status, JsonSerializer.Serialize(result, new JsonSerializerOptions{WriteIndented=true}));
    }

    private async Task RunAuditExport()
    {
        var settings = ConfigManager.Load();
        SetRow(0,"执行中","未知","正在采集审计...");
        var r = await AuditV5Manager.ExportAsync(settings);
        SetRow(0, r.Success?"成功":"失败", r.Enabled?"已启用":"未启用", r.Status);
        AddLog(r.Success?"成功":"失败", "01", r.Status, JsonSerializer.Serialize(r, new JsonSerializerOptions{WriteIndented=true}));
    }
    private void SetRow(int row,string exec,string enabled,string msg){ grid.Rows[row].Cells["Exec"].Value=exec; grid.Rows[row].Cells["Enabled"].Value=enabled; grid.Rows[row].Cells["Msg"].Value=msg; grid.Rows[row].Cells["Msg"].ToolTipText=msg; }
    private void AddLog(string level,string step,string msg,string detail)
    {
        var le = new LogEntry{Level=level, Step=step, Message=msg, Detail=detail}; logs.Add(le);
        var idx = logGrid.Rows.Add(le.Time.ToString("HH:mm:ss"), level, step, Short(msg, 80));
        logGrid.Rows[idx].Tag = le;
        if(level=="失败") logGrid.Rows[idx].DefaultCellStyle.BackColor=Color.MistyRose;
        else if(level=="成功") logGrid.Rows[idx].DefaultCellStyle.BackColor=Color.Honeydew;
        logGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, logGrid.Rows.Count-1);
    }
    private static string Short(string s,int len) => string.IsNullOrEmpty(s) || s.Length<=len ? s : s[..len] + "...";
    private void ShowSelectedLogDetail()
    {
        if(logGrid.SelectedRows.Count==0) return;
        if(logGrid.SelectedRows[0].Tag is LogEntry le) detailBox.Text = $"时间：{le.Time:yyyy-MM-dd HH:mm:ss}\r\n级别：{le.Level}\r\n步骤：{le.Step}\r\n消息：{le.Message}\r\n\r\n详细：\r\n{le.Detail}";
    }
    private void ExportUiReport()
    {
        var root = ConfigManager.AppRoot; Directory.CreateDirectory(Path.Combine(root,"Reports"));
        var file = Path.Combine(root,"Reports",$"V5_UI_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'><style>body{font-family:Segoe UI,Microsoft YaHei}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:6px}</style></head><body>");
        sb.AppendLine($"<h1>V5 安全合规加固报告</h1><p>{Environment.MachineName} {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p><table><tr><th>步骤</th><th>项目</th><th>执行</th><th>状态</th><th>说明</th></tr>");
        foreach(DataGridViewRow r in grid.Rows) sb.AppendLine($"<tr><td>{r.Cells["Id"].Value}</td><td>{Enc(r.Cells["Title"].Value)}</td><td>{Enc(r.Cells["Exec"].Value)}</td><td>{Enc(r.Cells["Enabled"].Value)}</td><td>{Enc(r.Cells["Msg"].Value)}</td></tr>");
        sb.AppendLine("</table><h2>日志</h2><table><tr><th>时间</th><th>级别</th><th>步骤</th><th>消息</th></tr>");
        foreach(var l in logs) sb.AppendLine($"<tr><td>{l.Time:yyyy-MM-dd HH:mm:ss}</td><td>{Enc(l.Level)}</td><td>{Enc(l.Step)}</td><td>{Enc(l.Message)}</td></tr>");
        sb.AppendLine("</table></body></html>"); File.WriteAllText(file,sb.ToString(),Encoding.UTF8); MessageBox.Show("报告已导出："+file);
    }
    private static string Enc(object? o) => System.Net.WebUtility.HtmlEncode(Convert.ToString(o) ?? "");
}
