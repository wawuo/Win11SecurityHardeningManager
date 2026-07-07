using System.Text;
using System.Text.Json;

namespace Win11SecurityHardeningManager;

public sealed class MainForm : Form
{
    private readonly DataGridView grid = new();
    private readonly TextBox output = new();
    private readonly Label helpLabel = new();
    private readonly Button btnCheckAll = new(){Text="一键检查全部", Width=130};
    private readonly Button btnEnableAll = new(){Text="一键执行全部加固", Width=160};
    private readonly Button btnExportAudit = new(){Text="立即采集审计", Width=130};
    private readonly Button btnInstallTask = new(){Text="安装每日任务", Width=130};
    private readonly Button btnReport = new(){Text="生成界面报告", Width=130};

    private readonly List<HardeningStep> steps = new()
    {
        new(){Id="01", Title="Windows 11 Pro 单机审计日志保留自动化工具 V4（完整审计引擎）", ScriptFolder="Step01_AuditLogV4", AuditV4=true},
        new(){Id="02", Title="限制其他域账号登录", ScriptFolder="Step02_LoginRestriction", HighRisk=true},
        new(){Id="03", Title="关闭默认共享如 C$", ScriptFolder="Step03_DisableAdminShares", HighRisk=true},
        new(){Id="04", Title="外加指纹启用 2FA 登录（说明项：需 Credential Provider/WHfB/第三方 MFA）", ScriptFolder="", HighRisk=true},
        new(){Id="05", Title="添加漏洞扫描服务", ScriptFolder="Step05_VulnerabilityScanner"},
        new(){Id="06", Title="关闭 USB 传输", ScriptFolder="Step06_USBBlock", HighRisk=true},
        new(){Id="07", Title="超时锁屏", ScriptFolder="Step07_AutoLock"},
        new(){Id="08", Title="开启 BitLocker 并将恢复密钥保存", ScriptFolder="Step08_BitLocker", HighRisk=true},
        new(){Id="09", Title="将本地管理员全部停用（保留白名单）", ScriptFolder="Step09_LocalAdmin", HighRisk=true},
    };

    public MainForm()
    {
        Text="Windows 11 单机安全合规加固管理器 - V4 Integrated";
        Width=1380; Height=860; StartPosition=FormStartPosition.CenterScreen;
        var top=new FlowLayoutPanel{Dock=DockStyle.Top, Height=45};
        top.Controls.AddRange(new Control[]{btnCheckAll,btnEnableAll,btnExportAudit,btnInstallTask,btnReport});
        helpLabel.Dock=DockStyle.Top; helpLabel.Height=95; helpLabel.Padding=new Padding(10,6,10,6); helpLabel.BackColor=Color.FromArgb(255,250,225); helpLabel.BorderStyle=BorderStyle.FixedSingle;
        helpLabel.Text="状态说明：执行结果=脚本是否正常运行；启用状态=安全项是否生效；详细说明=脚本返回的具体说明/错误。\r\n"+
            "01 项为完整 V4 审计引擎：启用登录/注销、文件访问、共享访问、远程访问、账户管理、USB、打印等审计；支持立即采集、生成 ComplianceMapping.html、安装每日归档任务。\r\n"+
            "高风险项执行前会二次确认；请先在测试机验证 settings.json 中的目录、账号白名单、BitLocker 密钥路径。";
        grid.Dock=DockStyle.Top; grid.Height=405; grid.AllowUserToAddRows=false; grid.RowHeadersVisible=false; grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add("Id","步骤"); grid.Columns.Add("Title","项目"); grid.Columns.Add("ExecResult","执行结果"); grid.Columns.Add("EnabledState","启用状态"); grid.Columns.Add("DetailStatus","详细说明");
        AddButtonColumn("Check","检查"); AddButtonColumn("Enable","启用"); AddButtonColumn("Rollback","回滚/禁用"); AddButtonColumn("Extra","采集/任务");
        foreach(var s in steps) grid.Rows.Add(s.Id,s.Title,"未执行","未知","未检查","检查","启用","回滚", s.AuditV4 ? "采集" : "-");
        grid.Columns[0].Width=50; grid.Columns[1].Width=510; grid.Columns[2].Width=85; grid.Columns[3].Width=85; grid.Columns[4].Width=330;
        output.Dock=DockStyle.Fill; output.Multiline=true; output.ScrollBars=ScrollBars.Both; output.Font=new Font("Consolas",10);
        Controls.Add(output); Controls.Add(grid); Controls.Add(helpLabel); Controls.Add(top);
        grid.CellContentClick += Grid_CellContentClick;
        btnCheckAll.Click += async (_,_) => await RunAllAsync("Check");
        btnEnableAll.Click += async (_,_) => await RunAllAsync("Enable");
        btnExportAudit.Click += async (_,_) => await RunAuditV4SpecialAsync("Export-AuditEvents.ps1", "01-Export");
        btnInstallTask.Click += async (_,_) => await RunAuditV4SpecialAsync("Install-ScheduledTask.ps1", "01-InstallTask");
        btnReport.Click += (_,_) => GenerateHtmlReport();
    }
    private void AddButtonColumn(string name,string text){ grid.Columns.Add(new DataGridViewButtonColumn{Name=name,HeaderText=text,Text=text,UseColumnTextForButtonValue=true}); }
    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if(e.RowIndex<0) return; var col=grid.Columns[e.ColumnIndex].Name;
        if(col is "Check" or "Enable" or "Rollback") await RunStepAsync(e.RowIndex,col);
        if(col=="Extra" && steps[e.RowIndex].AuditV4) await RunAuditV4SpecialAsync("Export-AuditEvents.ps1", "01-Export");
    }
    private async Task RunAllAsync(string action)
    {
        if(action=="Enable" && MessageBox.Show("一键加固包含 USB 禁用、BitLocker、本地管理员停用等高风险项。确认继续？","高风险确认",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
        for(int i=0;i<steps.Count;i++) await RunStepAsync(i,action);
    }
    private async Task RunStepAsync(int rowIndex,string action)
    {
        var step=steps[rowIndex];
        if(step.Id=="04"){ SetRow(rowIndex,"跳过","人工确认","强制登录前密码+指纹需要 Credential Provider、Windows Hello for Business 或第三方 MFA；普通 C#/PS1 不能严谨实现。",null); return; }
        if(action!="Check" && step.HighRisk && MessageBox.Show($"确认对步骤 {step.Id} 执行 {action}?\r\n{step.Title}","高风险确认",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
        var scriptName=action=="Rollback" ? "Rollback.ps1" : action + ".ps1";
        await RunScriptForRow(rowIndex, Path.Combine(FindAppRoot(),"Scripts",step.ScriptFolder,scriptName), $"{step.Id}-{action}");
    }
    private async Task RunAuditV4SpecialAsync(string scriptName,string action)
    {
        await RunScriptForRow(0, Path.Combine(FindAppRoot(),"Scripts","Step01_AuditLogV4",scriptName), action);
    }
    private async Task RunScriptForRow(int rowIndex,string scriptPath,string actionName)
    {
        if(!File.Exists(scriptPath)){ SetRow(rowIndex,"失败","未知","脚本不存在："+scriptPath,null); return; }
        SetRow(rowIndex,"执行中","未知","正在执行脚本...",null);
        var appRoot=FindAppRoot(); var result=await PowerShellRunner.RunScriptAsync(scriptPath,actionName,appRoot);
        Append(result.Output+"\r\n"); SetStatusFromJson(rowIndex,result.Output);
    }
    private void SetRow(int row,string exec,string enabled,string detail, Color? back)
    {
        grid.Rows[row].Cells["ExecResult"].Value=exec; grid.Rows[row].Cells["EnabledState"].Value=enabled; grid.Rows[row].Cells["DetailStatus"].Value=detail;
        if(back.HasValue) grid.Rows[row].DefaultCellStyle.BackColor=back.Value;
    }
    private void SetStatusFromJson(int rowIndex,string text)
    {
        try{
            var start=text.IndexOf('{'); var end=text.LastIndexOf('}');
            if(start>=0 && end>start){ using var doc=JsonDocument.Parse(text.Substring(start,end-start+1)); var root=doc.RootElement;
                var success=root.TryGetProperty("Success",out var s)&&s.GetBoolean(); var enabled=root.TryGetProperty("Enabled",out var e)&&e.GetBoolean();
                var status=root.TryGetProperty("Status",out var st)?st.GetString():"已完成"; var error=root.TryGetProperty("Error",out var er)?er.GetString():"";
                SetRow(rowIndex, success?"成功":"失败", enabled?"已启用":"未启用", string.IsNullOrWhiteSpace(error)?(status??""):$"{status}；{error}", success?Color.White:Color.MistyRose); return; }
        }catch{}
        SetRow(rowIndex,"完成","未知","未解析到 JSON，详见下方输出",Color.LightYellow);
    }
    private string FindAppRoot()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null){ if(Directory.Exists(Path.Combine(dir.FullName,"Scripts"))&&File.Exists(Path.Combine(dir.FullName,"Config","settings.json"))) return dir.FullName; dir=dir.Parent; }
        return AppContext.BaseDirectory;
    }
    private void Append(string s){ if(InvokeRequired){Invoke(()=>Append(s));return;} output.AppendText(s+Environment.NewLine); }
    private void GenerateHtmlReport()
    {
        var appRoot=FindAppRoot(); Directory.CreateDirectory(Path.Combine(appRoot,"Reports")); var sb=new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Hardening Report</title><style>body{font-family:Segoe UI,Microsoft YaHei,sans-serif}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:7px}</style></head><body>");
        sb.AppendLine($"<h1>Windows 11 单机安全合规加固报告</h1><p>计算机：{Environment.MachineName}　时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("<p>执行结果=脚本是否正常运行；启用状态=安全项是否生效；详细说明=脚本返回的具体说明/错误。</p><table><tr><th>步骤</th><th>项目</th><th>执行结果</th><th>启用状态</th><th>详细说明</th></tr>");
        foreach(DataGridViewRow row in grid.Rows) sb.AppendLine($"<tr><td>{row.Cells["Id"].Value}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["Title"].Value))}</td><td>{row.Cells["ExecResult"].Value}</td><td>{row.Cells["EnabledState"].Value}</td><td>{System.Net.WebUtility.HtmlEncode(Convert.ToString(row.Cells["DetailStatus"].Value))}</td></tr>");
        sb.AppendLine("</table><h2>执行输出</h2><pre>"+System.Net.WebUtility.HtmlEncode(output.Text)+"</pre></body></html>");
        var file=Path.Combine(appRoot,"Reports",$"HardeningReport_{DateTime.Now:yyyyMMdd_HHmmss}.html"); File.WriteAllText(file,sb.ToString(),Encoding.UTF8); MessageBox.Show("报告已生成："+file);
    }
}
