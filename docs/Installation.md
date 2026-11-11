# R.slopeunits for ArcGIS Pro: Installation and User Guide

[English](Installation.md) | [简体中文](安装说明.md)

> Version 0.4 (2026-10-25) · ArcGIS Pro 3.0–3.7 · Add-in target: .NET 6 · GRASS GIS 8.5.0

This add-in runs GRASS `r.slopeunits.create` and `r.slopeunits.clean` from ArcGIS Pro to generate slope units. It includes tools for single runs, batch processing, parameter sweeps, and quality reports. It does not perform watershed delineation, basin–slope coupling, or machine-learning analysis.

## Requirements

- Windows x64 and ArcGIS Pro 3.0–3.7.
- A projected DEM in meters. Geographic-coordinate DEMs are rejected.
- The packaged installer includes the portable GRASS runtime; a separate GRASS installation is not required.
- Allow at least 1.8 GB of free space on the local application-data drive and temporary drive during installation.

## Install

Close ArcGIS Pro before installing.

### Option A: Setup application

Download the `BasinSlopeTools_Setup_0.4.exe` asset from the repository's GitHub Releases page, run it, and select **Start Installation**. The setup installs the add-in and portable GRASS runtime for the current Windows user; administrator rights are not required.

For unattended deployment, run:

```cmd
BasinSlopeTools_Setup_0.4.exe --silent
```

### Option B: Portable ZIP

Download and extract the portable release ZIP to a local folder, then run `install.cmd`. The installer checks for ArcGIS Pro, free disk space, and a closed Pro process before installation.

### Option C: Add-in only

Use this option when GRASS GIS 8.5 is already installed:

1. Close ArcGIS Pro.
2. Double-click `BasinSlopeTools.esriAddinX` and follow the ArcGIS Pro add-in installation prompt.
3. Configure or select the GRASS `grass85.bat` path in the toolbox if it is not detected automatically.
4. Restart ArcGIS Pro.

After installation, the **R.slopeunits Slope Units** tab should appear. If it does not, enable BasinSlopeTools in ArcGIS Pro's Add-In Manager.

## Generate slope units

Open the **R.slopeunits** Python toolbox and select **Generate Slope Units**. Choose an input DEM and output GeoPackage, then set the processing parameters. The toolbox can also write a File Geodatabase and load results into the current map.

| Parameter | Default | Description |
|---|---:|---|
| Input DEM | — | Projected raster; supported formats include TIFF, IMG, ASCII Grid, and ESRI Grid (`hdr.adf`). |
| Output GeoPackage | — | Destination `.gpkg` file. |
| GRASS `grass85.bat` | Automatic | Uses the bundled runtime first; select a path manually if needed. |
| DEM EPSG | Automatic | Optional coordinate system check; when provided, it must match the DEM. |
| Preset | Custom | Includes 13 m / 2 km², 30 m / 5 km², and Quick Preview presets. |
| `thresh` | 250,000 m² | Initial threshold. |
| `areamin` | 200,000 m² | Minimum slope-unit area. |
| `areamax` | — | Optional maximum area. |
| `cvmin` | 0.25 | Circular variance threshold for aspect. |
| `rf` | 2 | Threshold reduction factor. |
| `maxiteration` | 50 | Maximum iterations. |
| `convergence` | 5 | MFD convergence factor (1–10). |
| SFD | Off | Uses MFD by default; enable for D8/SFD flow routing. |
| `cleansize` | 25,000 m² | Small-unit cleaning threshold. |
| Clean method | Fast (method 1) | Fast, stripe cleanup (method 2), or detailed cleanup (method 3). |
| Also output GDB | Off | Writes a File Geodatabase alongside the GeoPackage. |

Use **Quick Preview** or a smaller DEM subset to tune parameters before processing a full-resolution study area.

## Outputs and reports

A run can produce:

- A GeoPackage containing slope-unit polygons and fields such as `AREA_M2` and `CVAR`.
- An optional `slope_units.gdb` feature class.
- `<output>_quality_report.html`, `.json`, and `.csv` files.
- `<output>_run_manifest.json` with parameters and run details.
- A run log in the Windows temporary directory.

The **Quality Report** button opens the latest report. Its composite score (0–100) combines CVAR pass rate, area pass rate, and clean status. Treat it as a relative workflow indicator, not as an absolute measure of terrain accuracy.

## GRASS runtime location

The installer places the bundled runtime here:

```text
%LOCALAPPDATA%\BasinSlopeTools\runtime\grass\bin\grass85.bat
```

The add-in also checks common installation locations. If GRASS is elsewhere, browse to `grass85.bat` in the tool parameters.

## Uninstall and troubleshooting

Use **Apps & Features** in Windows, run the installed `uninstall.cmd`, or launch the setup application with:

```cmd
BasinSlopeTools_Setup_0.4.exe --uninstall
```

Close ArcGIS Pro before uninstalling. If installation fails, review `%TEMP%\BasinSlopeTools_Setup.log`. For runtime errors, inspect the per-run log in `%TEMP%` and confirm the DEM uses a projected coordinate system with meter units and that the selected GRASS batch file exists.

## Build from source

See the repository [README](../README.md) for SDK requirements and build commands. The GRASS runtime and generated installer payload are excluded from Git because of their size; a complete packaged release must include the runtime, relevant license notices, and source-offer materials.

## License

See `BasinSlopeTools/Licenses/` and the notices under `dist/`. GRASS GIS and bundled third-party components are distributed under their own licenses. Preserve all required notices and source-offer information when redistributing a package.
