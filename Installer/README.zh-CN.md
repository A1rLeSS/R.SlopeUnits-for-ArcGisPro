# 安装器构建说明

[English](README.md) | [简体中文](README.zh-CN.md)

安装器是一个自解压的 .NET 10 WinForms 程序，运行时会解压内嵌的 `payload.zip`，再调用 PowerShell 安装脚本。

## 项目内容

```text
Installer/
├─ BasinSlopeToolsInstaller.csproj
├─ Program.cs              # WinForms 安装向导和命令行选项
├─ InstallerEngine.cs      # 解压 payload 并运行 install.ps1
└─ payload.zip             # 发行包构建输入，不提交到 Git
```

要生成可复现的发行包，请使用仓库根目录的构建脚本。脚本会先生成便携包，再将其作为安装器 payload。GRASS runtime 和 ArcGIS Pro SDK 的要求见[源码构建说明](../docs/BUILDING.md)。

## 构建

```powershell
./scripts/build.ps1 `
  -GrassRuntime 'C:\path\to\grass\bin\grass85.bat' `
  -ArcGISProSDKTargets 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
```

脚本会在 `artifacts/` 下生成带版本号的发行文件和 `SHA256SUMS.txt`，并在构建前运行测试与版本检查。正式发行建议始终使用该脚本，确保安装器与便携包来自同一份构建输入。

## 诊断选项

```cmd
BasinSlopeTools_Setup.exe --dry-run
BasinSlopeTools_Setup.exe --silent --accept-license
```

`--dry-run` 只解压并检查安装内容，不执行安装。静默安装必须明确接受许可。

日志位置：

```text
%TEMP%\BasinSlopeTools_Setup.log
```
