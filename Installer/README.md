# Installer build guide

[English](README.md) | [简体中文](README.zh-CN.md)

The installer is a self-extracting .NET 10 WinForms application. It embeds a `payload.zip` containing the add-in, GRASS runtime, and installation scripts.

## Interface

The window is kept simple (about 640 × 520, no decorative images):

1. choose English or 简体中文;
2. read and accept the bilingual licence;
3. view a percentage progress bar and localized detailed install events;
4. finish, with an optional log button.

## Project contents

```text
Installer/
├─ BasinSlopeToolsInstaller.csproj
├─ Program.cs              # WinForms setup wizard and command-line options
├─ InstallerEngine.cs      # Extracts the payload and runs install.ps1
└─ payload.zip             # Portable package (Add-in, GRASS runtime, scripts)
```

For a reproducible release build, use the repository build script. It creates the portable package first and embeds that package as the installer payload. See [Building from Source](../docs/BUILDING.md) for the required GRASS runtime and ArcGIS Pro SDK inputs.

## Build

```powershell
./scripts/build.ps1 `
  -GrassRuntime 'C:\path\to\grass\bin\grass85.bat' `
  -ArcGISProSDKTargets 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
```

The script writes versioned release assets and `SHA256SUMS.txt` under `artifacts/`. It runs tests and checks version markers before building. Use this script for releases so the installer payload and portable package come from the same build.

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




