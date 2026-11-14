# Installer 构建说明

安装器是 .NET 10 WinForms 自解压程序，内嵌 `payload.zip`。

## 界面

安装器为约 640 × 520 的简单窗口，不使用图片或复杂卡片：

1. 选择语言（English / 简体中文）；
2. 阅读并接受双语许可协议；
3. 显示百分比进度条和本地化的详细安装事件；
4. 完成页可打开日志或关闭。

许可证正文来自 `BasinSlopeTools/Licenses/AGREEMENT_en.txt`、`AGREEMENT_zh.txt`，并在末尾附加 `GPL-2.0.txt` 官方英文全文。

## 组成

```text
Installer/
├─ BasinSlopeToolsInstaller.csproj
├─ Program.cs              # 语言选择 + 许可 + 安装进度 + 完成页
├─ InstallerEngine.cs      # 解压 payload、调用 install.ps1
└─ payload.zip             # 便携包（Add-in + GRASS runtime + install.ps1）
```

`payload.zip` 来自便携包：

```text
BasinSlopeTools_Portable_20261111.zip
```

## 构建

```powershell
dotnet publish `
  .\Installer\BasinSlopeToolsInstaller.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\Installer\publish
```

输出：

```text
publish\BasinSlopeTools_Setup.exe
```

## 测试参数

```cmd
BasinSlopeTools_Setup.exe --dry-run                 # 只解压校验，不安装
BasinSlopeTools_Setup.exe --silent --accept-license # 静默安装（必须接受许可）
```

日志：

```text
%TEMP%\BasinSlopeTools_Setup.log
```
