# 中文乱码修复补丁

## 现象

界面输出类似：

```json
"Status": "���ʧ��"
```

原因是 Windows PowerShell 5.1 默认输出编码可能是 OEM/GBK，而 C# 代码按 UTF-8 读取，导致中文被错误解码。

## 使用方法

把 `Fix-Encoding.ps1` 放到 `Win11SecurityHardeningManager_V1` 项目根目录，然后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Fix-Encoding.ps1
```

然后在 Visual Studio 中：

```text
生成 -> 清理解决方案
生成 -> 重新生成解决方案
```

再运行程序。
