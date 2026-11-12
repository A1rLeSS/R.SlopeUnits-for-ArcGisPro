# Building from Source

## Prerequisites

- Windows x64
- .NET 6 SDK (add-in) and .NET 10 SDK (installer)
- ArcGIS Pro 3.x with the SDK targets file, usually:

```text
C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets
```

- A local GRASS GIS 8.5 runtime containing `bin\grass85.bat` (not committed to Git)

## Configure the ArcGIS Pro SDK path

`BasinSlopeTools/BasinSlopeTools.csproj` imports the SDK targets from `D:\ArcGISPro\bin\Esri.ProApp.SDK.Desktop.targets` by default. On another machine, either edit that property or pass:

```powershell
$env:ARCGIS_PRO_SDK_TARGETS = 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
```

The build script replaces the import with the supplied path in a temporary project copy only. The source file is not rewritten.

## Build and package

```powershell
.\scripts\build.ps1 `
  -GrassRuntime 'D:\path\to\grass\bin\grass85.bat' `
  -ArcGISProSDKTargets 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
```

Outputs:

```text
artifacts/
├─ BasinSlopeTools_Setup_<major.minor>.exe
├─ BasinSlopeTools_Portable_<build-date>.zip
├─ BasinSlopeTools_<version>_docs.zip
└─ SHA256SUMS.txt
```

The payload is generated from:

- `BasinSlopeTools/bin/Release/net6.0-windows/BasinSlopeTools.esriAddinX`
- `BasinSlopeTools/Tools/**`
- `dist/install.ps1`, license notices and the GRASS runtime tree

`Installer/payload.zip` is a generated file and is excluded from Git.

## Run tests

```powershell
python -m unittest discover -s tests -v
```

## Version consistency

`VERSION` and `TOOLS_REVISION` are the source of truth for the app version and Tools cache revision. Before packaging:

```powershell
.\scripts\check-version.ps1
```

The check verifies:

- `BasinSlopeTools/Config.daml` add-in version;
- `Installer/BasinSlopeToolsInstaller.csproj` version;
- `BasinSlopeTools/Buttons.cs` `ToolsRevision`;
- `dist/install.ps1` `.tools_revision` value.

After a Python Toolbox change, bump `TOOLS_REVISION`, `Buttons.cs` and `dist/install.ps1` together; the add-in cache logic uses this marker.

