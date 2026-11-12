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

发行时使用的 payload 来自便携包：

```text
output\00_交付包\BasinSlopeTools_Portable_20261025.zip
```

## 构建

```powershell
dotnet publish `
  D:\BasinSlopeCoupling\pro_addin\Installer\BasinSlopeToolsInstaller.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o D:\BasinSlopeCoupling\pro_addin\Installer\publish
```

生成文件：

```text
publish\BasinSlopeTools_Setup.exe
```

文件体积取决于 payload，主要包含自包含的 .NET 10 运行时和 GRASS 运行环境。

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
