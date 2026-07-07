# Windows 11 单机安全合规加固管理器 V5 - C# Native

## 这版改动

这版按要求把能改成 C# 的都改成 C# 直接执行，尽量不再依赖 PS1：

- 03 禁止 C$/D$/E$：C# 写注册表 + 调用 net.exe 删除当前盘符共享
- 06 禁 USB：C# 写注册表 + gpupdate.exe
- 07 超时锁屏：C# 写注册表
- 09 本地管理员管理：C# 调用 wmic/net.exe 获取 Administrators 内置组并禁用非白名单本地用户
- 01 审计：C# 直接调用 auditpol.exe/wevtutil.exe，并用 C# 配置目录 SACL 和生成 ComplianceMapping.html
- 08 BitLocker：C# 直接调用 manage-bde.exe
- 日志界面改为结构化日志列表，点击日志可查看完整 JSON 细节
- 表格列宽限制最小 20、最大 100，避免界面拖拽变形

## 仍保留外部 Windows CLI 的原因

以下功能是 Windows 系统管理命令，C# 直接调用它们，比 C#->PowerShell->命令 更稳定：

- auditpol.exe
- wevtutil.exe
- manage-bde.exe
- net.exe
- gpupdate.exe
- wmic.exe

## 编译

Visual Studio 2022 打开：

```text
Win11SecurityHardeningManager.csproj
```

需要：

```text
.NET 8 SDK
.NET 桌面开发
```

建议右键 Visual Studio，以管理员身份运行。

## 部署

生成 Release 后复制整个目录：

```text
bin\Release\net8.0-windows\
```

不要只复制 exe。目标电脑需要 .NET Desktop Runtime 8。

## 配置

修改：

```text
Config\settings.json
```

重点：

- AuditDirectories
- ArchiveRoot
- KeepLocalAdmins
- ScreenLockTimeoutSeconds
- BitLockerRecoveryKeyBackupPath

## 注意

- 09 本地管理员管理默认只保留 `Administrator`，如果要保留当前 admin，请把 `admin` 加回 `KeepLocalAdmins`。
- 09 不处理域用户/域组/AzureAD/MicrosoftAccount，只处理本地用户。
- 02 登录限制和 04 指纹 2FA 保留为说明项，避免用程序误配置导致锁死或伪 2FA。
