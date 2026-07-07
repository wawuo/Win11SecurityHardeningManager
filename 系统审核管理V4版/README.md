# Windows 11 单机安全合规加固管理器 - V4 Integrated

这版是“真正接入 V4 审计引擎”的版本，而不是之前的简化框架版。

## 重点能力

01 项 `Step01_AuditLogV4` 已包含：

- 登录/注销审计：4624、4625、4634、4647、4800、4801、4740
- 文件访问审计：4663、4660、4670、4659
- 共享访问审计：5140、5145
- 远程访问/RDP 审计：Security LogonType 10/7/3，TerminalServices 21/24/25/1149
- 账户管理审计：4720-4781 相关用户/组/密码/锁定事件
- USB/可移动存储审计：Removable Storage、6416、DriverFrameworks-UserMode
- 打印审计：Microsoft-Windows-PrintService/Operational，例如 307
- Security 日志大小/归档目录
- 文件目录 SACL 自动配置
- 立即采集并导出 CSV/HTML
- 生成 `ComplianceMapping.html`，并在 11.2 下列出当前审核目录
- 安装每日计划任务自动采集

## 使用步骤

1. 用 Visual Studio 2022 打开 `Win11SecurityHardeningManager.csproj`。
2. 确认安装 `.NET 8 SDK` 和 “.NET 桌面开发”。
3. 修改 `Config\settings.json`：
   - `AuditDirectories`
   - `ArchiveRoot`
   - `KeepLocalAdmins`
   - `BitLockerRecoveryKeyBackupPath`
   - 漏洞扫描服务名称/安装包路径
4. 右键 Visual Studio，以管理员身份运行。
5. 清理并重新生成解决方案。
6. 先点 01 项 “检查”，再点 “启用”，再点 “立即采集审计”。

## 单独运行 V4 审计脚本

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Step01_AuditLogV4\Enable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Step01_AuditLogV4\Export-AuditEvents.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Step01_AuditLogV4\Install-ScheduledTask.ps1
```

## 高风险说明

- Step02 登录限制：为避免锁死，只创建白名单组，不自动写入拒绝登录 SID。
- Step03 禁止 C$：`AutoShareWks=0` 对 C$/D$ 等盘符默认管理共享是持久的；ADMIN$/IPC$ 可能由系统维护。
- Step08 BitLocker：启用前请确认恢复密钥保存位置可用。
- Step09 本地管理员停用：只停用非白名单本地用户，不处理域组/域用户。

## 输出位置

默认归档目录：

```text
C:\AuditArchive
```

每次立即采集会生成：

```text
C:\AuditArchive\yyyyMMdd_HHmmss\AuditSummary.csv
C:\AuditArchive\yyyyMMdd_HHmmss\ComplianceMapping.html
C:\AuditArchive\yyyyMMdd_HHmmss\LogonLogoff\*.csv/*.html
C:\AuditArchive\yyyyMMdd_HHmmss\FileAccess\*.csv/*.html
...
```
