# Windows 11 单机安全合规加固管理器 V1

## 编译

在已安装 .NET 8 SDK 的 Windows 11 上执行：

```powershell
cd Win11SecurityHardeningManager_V1
dotnet build -c Release
```

编译后复制以下目录到 exe 同级：

- Scripts
- Config
- Logs
- Reports

或直接在项目根目录运行：

```powershell
dotnet run
```

## 运行

必须右键以管理员身份运行。程序 manifest 已配置 requireAdministrator。

## 配置

修改：

```text
Config\settings.json
```

重点修改：

- AllowedDomainUsers
- AuditDirectories
- KeepLocalAdmins
- BitLockerRecoveryKeyBackupPath
- VulnerabilityScannerInstallerPath
- VulnerabilityScannerServiceName

## 重要说明

第 4 项“密码后再指纹 2FA 登录”不能通过普通 C# / PowerShell 在登录前强制实现。
正规方案需要 Credential Provider、Windows Hello for Business 或第三方 MFA 登录组件。

本工具只做“状态说明”，不伪装成真实登录前 2FA。

## 高风险模块

以下模块可能影响用户登录、USB 使用、远程管理、磁盘加密，请先在测试机验证：

- Step02_LoginRestriction
- Step03_DisableAdminShares
- Step06_USBBlock
- Step08_BitLocker
- Step09_LocalAdmin

## 单独运行脚本示例

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Step06_USBBlock\Enable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Step06_USBBlock\Rollback.ps1
```
