# R.slopeunits for ArcGIS Pro — Portable Package 0.4

[English](README.en.md) | [简体中文](README.md)

The portable package runs on Windows with ArcGIS Pro 3.0–3.7. It includes GRASS GIS 8.5.0, so users do not need to install GRASS separately.

## Install

1. Close ArcGIS Pro.
2. Extract the ZIP to a local folder. A short path without special characters is recommended.
3. Run `install.cmd`, or run `install.ps1` from PowerShell.
4. Restart ArcGIS Pro. The **R.slopeunits Slope Units** tab should appear.

The installer registers the add-in for the current user, copies GRASS to `%LOCALAPPDATA%\BasinSlopeTools\runtime\grass`, and sets the user-level `BASINSLOPE_GRASS` variable. It does not require administrator rights or change the system PATH. Windows Apps & Features or `%LOCALAPPDATA%\BasinSlopeTools\uninstall.cmd` can be used to uninstall.

## Tools

| Tool | What it does |
|---|---|
| Generate Slope Units | Runs GRASS `r.slopeunits.create` and `r.slopeunits.clean`; exports GeoPackage and optionally File Geodatabase. |
| Batch Generate | Processes multiple DEMs and can resume completed work. |
| Parameter Sweep | Runs selected parameter combinations and can resume completed work. |
| Quality Report | Opens the latest run report. |
| About / Licenses | Shows version, license notices, and source information. |

The add-in does not delineate watersheds, couple basins with slope units, or run machine-learning models.

## Input and parameters

Use a projected DEM with meter units. Supported formats include TIFF, IMG, ASCII Grid, and ESRI Grid (select `hdr.adf`). The tool checks for common NoData sentinels such as 65535 and -9999.

The toolbox offers presets for 13 m / 2 km², 30 m / 5 km², and quick preview. Advanced controls include `thresh`, `areamin`, optional `areamax`, `cvmin`, `rf`, `maxiteration`, MFD convergence, SFD routing, cleaning size and method, and optional GDB output. Start with a small area or quick preview before running a large DEM.

## Outputs

Runs can create a `.gpkg`, an optional `.gdb`, HTML/JSON/CSV quality reports, a JSON run manifest, and a log under the Windows temporary folder. The composite quality score is an engineering aid for relative comparison; it is not a measure of absolute terrain accuracy.

## GRASS runtime

The bundled executable is located at:

```text
%LOCALAPPDATA%\BasinSlopeTools\runtime\grass\bin\grass85.bat
```

If the runtime is installed elsewhere, select its `grass85.bat` in the tool parameters.

## Notes and licenses

Full-resolution DEM processing can take a long time. Tune parameters on a small area first. GRASS GIS is licensed under GPL v2 or later. Preserve the bundled license files and source-offer information when redistributing the package. See `LICENSE.md`, `LICENSE_GRASS.md`, and `THIRD_PARTY_NOTICES.txt`.
