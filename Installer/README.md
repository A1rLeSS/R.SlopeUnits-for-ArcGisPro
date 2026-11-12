# Installer build guide

[English](README.md) | [简体中文](README.zh-CN.md)

The installer is a self-extracting .NET 10 WinForms application. It embeds a `payload.zip` containing the add-in, GRASS runtime, and installation scripts.

## Project contents

```text
Installer/
├─ BasinSlopeToolsInstaller.csproj
├─ Program.cs              # WinForms setup wizard and command-line options
├─ InstallerEngine.cs      # Extracts the payload and runs install.ps1
└─ payload.zip             # Portable package (Add-in, GRASS runtime, scripts)
```

The release payload is prepared from the portable package:

```text
output\00_交付包\BasinSlopeTools_Portable_20261025.zip
```

## Build

```powershell
dotnet publish `
  D:\BasinSlopeCoupling\pro_addin\Installer\BasinSlopeToolsInstaller.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o D:\BasinSlopeCoupling\pro_addin\Installer\publish
```

The published executable is written to:

```text
publish\BasinSlopeTools_Setup.exe
```

The executable is large because it bundles the self-contained .NET 10 runtime and GRASS payload. Its size depends on the payload used for the build.

## Diagnostic options

```cmd
BasinSlopeTools_Setup.exe --dry-run                 # Extract and check the payload without installing
BasinSlopeTools_Setup.exe --silent --accept-license # Install without showing the wizard
```

The setup log is written to:

```text
%TEMP%\BasinSlopeTools_Setup.log
```


