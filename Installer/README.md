# Installer 构建说明

安装器是 .NET 10 WinForms 自解压程序，内嵌 `payload.zip`。

## 组成

```text
Installer/
├─ BasinSlopeToolsInstaller.csproj
├─ Program.cs              # 三步式 WinForms 向导 + --dry-run / --silent / --uninstall
├─ InstallerEngine.cs      # 解压 payload、调用 install.ps1
└─ payload.zip             # 便携包（Add-in + GRASS runtime + install.ps1）
```

`payload.zip` 来自：

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

输出：

```text
publish\BasinSlopeTools_Setup.exe
```

约 428 MB（自包含 .NET 10 + GRASS runtime payload）。

## 测试参数

```cmd
BasinSlopeTools_Setup.exe --dry-run                 # 只解压校验，不安装
BasinSlopeTools_Setup.exe --silent --accept-license # 静默安装（必须接受许可）
```

日志：

```text
%TEMP%\BasinSlopeTools_Setup.log
```


