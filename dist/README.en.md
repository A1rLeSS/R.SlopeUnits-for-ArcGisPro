# R.slopeunits for ArcGIS Pro — Portable Package 0.5.0

[English](README.en.md) | [简体中文](README.md)

The portable package supports ArcGIS Pro 3.0–3.7 and includes GRASS GIS 8.5.0. You do not need to install GRASS separately.

## Install

1. Close ArcGIS Pro and any running GRASS analysis.
2. Extract the ZIP to a local folder. Chinese paths are supported; a short path is still recommended.
3. Run `install.cmd`, or run `install.ps1` from PowerShell.
4. Restart ArcGIS Pro. The **R.slopeunits Slope Units** tab should appear.

The installer registers the add-in for the current user, copies GRASS to `%LOCALAPPDATA%\BasinSlopeTools\runtime\grass`, and sets the user-level `BASINSLOPE_GRASS` variable. It checks that ArcGIS Pro is installed and closed and that enough disk space is available. It does not require administrator rights or change the system PATH.

To uninstall, use Windows **Apps & Features** or run `%LOCALAPPDATA%\BasinSlopeTools\uninstall.cmd`.

## Tools

| Tool | What it does |
|---|---|
| Generate Slope Units | Runs GRASS `r.slopeunits.create` and `r.slopeunits.clean`; exports a GeoPackage and optionally a File Geodatabase. |
| Batch Generate | Processes multiple DEMs and can resume completed work. |
| Parameter Sweep | Runs selected parameter combinations and can resume completed work. |
| Quality Report | Opens the latest report and its comparison with the previous run. |
| About / Licenses | Shows the version, license notices, and source information. |

The add-in does not delineate watersheds, couple basins with slope units, or run machine-learning models.

## Input data and parameters

Use a projected DEM in meters. Supported formats include GeoTIFF, IMG, ASCII Grid, and ESRI Grid. For ESRI Grid, select `hdr.adf`; the toolbox resolves the parent grid directory. The tool masks common NoData sentinels `65535` and `-9999` before running GRASS.

The presets are 13 m / 2 km², 30 m / 5 km², and Quick Preview. Advanced controls include `thresh`, `areamin`, optional `areamax`, `cvmin`, `rf`, `maxiteration`, MFD convergence, SFD routing, cleaning size and method, and optional GDB output. Start with a small area or Quick Preview before processing a large DEM.

## Outputs and quality report

A run can create:

- a `.gpkg` with `AREA_M2` and `CVAR` attributes;
- an optional `<output>.gdb` feature class;
- HTML, JSON, and CSV quality reports, plus a PNG preview;
- a JSON run manifest and a log under the Windows temporary directory.

The report's 0–100 score combines CVAR pass rate (45%), area pass rate (25%), shape compactness (20%), and clean status (10%). These weights and thresholds are project heuristics for comparing parameter sets in the same area. The score is not an absolute measure of terrain accuracy.

## GRASS runtime

The bundled executable is located at:

```text
%LOCALAPPDATA%\BasinSlopeTools\runtime\grass\bin\grass85.bat
```

If GRASS is installed elsewhere, select its `grass85.bat` in the tool parameters.

## Notes and licenses

Full-resolution DEM processing can take a long time. Tune parameters on a small area first. GRASS GIS is licensed under GPL v2 or later. Preserve the bundled license files and source-offer information when redistributing the package. See `LICENSE.md`, `LICENSE_GRASS.md`, and `THIRD_PARTY_NOTICES.txt`.
