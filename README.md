# R.slopeunits for ArcGIS Pro

[English](README.md) | [简体中文](README.zh-CN.md)

ArcGIS Pro add-in and Python toolbox for generating slope units with the GRASS GIS `r.slopeunits.create` and `r.slopeunits.clean` modules.

## Repository layout

- `BasinSlopeTools/` — ArcGIS Pro add-in source, Python toolbox, GRASS add-on scripts, and license notices.
- `Installer/` — Windows setup application source.
- `dist/` — install scripts and release documentation.
- `docs/` — Chinese installation and usage guide.

The GRASS runtime and generated installer payload are intentionally excluded from Git because together they exceed 1 GB. Download a packaged release from the GitHub Releases page, or follow the build notes below to recreate the package.

## Requirements

- Windows x64
- ArcGIS Pro 3.0–3.7
- .NET 6 Windows SDK for the add-in
- .NET 10 SDK for the installer
- GRASS GIS 8.5 runtime for running the packaged toolbox

The add-in project currently references an ArcGIS Pro SDK targets file at `D:\ArcGISPro\bin\Esri.ProApp.SDK.Desktop.targets`. Update that import to match the local ArcGIS Pro installation before building on another machine.

## Build

Build the add-in using the ArcGIS Pro SDK environment:

```powershell
dotnet build .\BasinSlopeTools\BasinSlopeTools.csproj -c Release
```

To build the single-file setup application, first prepare `Installer\payload.zip` using the release packaging workflow, then run:

```powershell
dotnet publish .\Installer\BasinSlopeToolsInstaller.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -o .\Installer\publish
```

## License

See the license and third-party notices under `BasinSlopeTools/Licenses/` and `dist/`. GRASS GIS and its bundled dependencies retain their respective licenses; packaged releases must include the notices and source offer applicable to redistributed components.
