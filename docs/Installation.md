# Installation and User Guide

[English](Installation.md) | [简体中文](安装说明.md)

> Version 0.5.0 · ArcGIS Pro 3.0–3.7 · .NET 6 add-in · GRASS GIS 8.5.0

## 1. What this project generates

This add-in generates **slope units**, not watershed/basin polygons. It runs GRASS `r.slopeunits.create` and `r.slopeunits.clean` and writes a feature layer named `slope_units` with `AREA_M2` and `CVAR` attributes plus a run manifest and quality report.

Basin delineation and basin–slope coupling are outside the current scope of the add-in.

## 2. Download and install

Download the latest release from:

<https://github.com/A1rLeSS/R.SlopeUnits-for-ArcGisPro/releases/latest>

Assets:

- `BasinSlopeTools_Setup_0.5.exe` — self-contained Windows installer (includes GRASS 8.5 runtime);
- `BasinSlopeTools_Portable_20261111.zip` — portable package and manual `install.ps1`;
- `SHA256SUMS.txt` — verify downloads.

Installation steps:

1. Close ArcGIS Pro and any running GRASS analysis.
2. Run `BasinSlopeTools_Setup_0.5.exe`.
3. Choose **English** or **简体中文**, read the bilingual licence and tick the acceptance box.
4. Continue through **Language → Licence → Install → Finish**; the install page shows a percentage progress bar and a detailed event log.
5. Reopen ArcGIS Pro and open the **R.slopeunits** tab.
6. On first use, allow the Python Toolbox to execute third-party code when Pro asks.

The installer requires no administrator rights. It checks for ArcGIS Pro 3.0–3.7, blocks installation if Pro is running, and checks for 1.8 GB free space.

### Silent install / uninstall

```powershell
.\BasinSlopeTools_Setup_0.5.exe --silent --accept-license
.\BasinSlopeTools_Setup_0.5.exe --uninstall
```

`--silent` without `--accept-license` exits with code `2`. Exit code `1` means an installation error; see `%TEMP%\BasinSlopeTools_Setup.log`.

## 3. Input data

- DEM must be in a **projected coordinate system with metre units**. Geographic DEMs are rejected.
- Supported inputs: GeoTIFF, `.img`, ASCII grid, and **ESRI Grid**.
- For an ESRI Grid, select the `hdr.adf` file inside the grid directory; the toolbox resolves the parent directory automatically.
- `65535` and `-9999` are masked with `r.null` before GRASS runs.
- Non-ASCII (including Chinese) input and output paths are supported through an ASCII temporary link/copy.
- Output file: `*.gpkg`; optional `*.gdb` is created beside it.

## 4. Tools and parameters

| Tool | Purpose |
|---|---|
| Generate slope units | Single DEM → GPKG/GDB, quality report, optional map layer |
| Batch generate | Multiple DEMs with resumable state and `batch_summary.csv` |
| Parameter sweep | Combinations of threshold/area/CVAR/iteration with resumable state |
| Quality report | Latest report and previous-run comparison |
| About / licenses | Version, GPL and third-party notices |

Main parameters:

| Parameter | Default | Notes |
|---|---:|---|
| Input DEM | — | Projected DEM; ESRI Grid: choose `hdr.adf` |
| Output GeoPackage | — | `*.gpkg` |
| GRASS `grass85.bat` | automatic | Bundled runtime or custom path |
| DEM EPSG | automatic | Must match the DEM if entered manually |
| Preset | Custom | 13 m / 2 km², 30 m / 5 km², Fast preview |
| `thresh` | 250,000 m² | Initial threshold |
| `areamin` | 200,000 m² | Minimum unit area |
| `areamax` | empty | Optional maximum area |
| `cvmin` | 0.25 | Circular-variance threshold |
| `rf` | 2 | Threshold reduction factor |
| `maxiteration` | 50 | Maximum iterations |
| `convergence` | 5 | MFD convergence factor |
| SFD | off | MFD by default; enable for D8 |
| `cleansize` | 25,000 m² | Small-unit cleaning threshold |
| Clean method | Fast (1) | Fast / stripe (2) / detailed (3) |
| Make GDB | off | Also write `<output>.gdb` |
| Symbology | on (single tool) | CVAR 5-class NaturalBreaks; requires GDB/map |
| Add to map | on (single tool) | Batch/sweep default off |

## 5. Outputs

```text
<output>.gpkg
<output>.gdb/                 optional
<output>_quality_report.html
<output>_quality_report.json
<output>_quality_report.csv
<output>_preview.png
<output>_run_manifest.json
%TEMP%\R_slopeunits_<timestamp>_<pid>.log
```

Quality reports are copied to `%LOCALAPPDATA%\BasinSlopeTools\reports` and indexed in `quality_reports.json`. The newest 50 entries are retained; older history files are cleaned up.

## 6. Generation quality score

The report adds a **0–100 generation quality score** and a grade:

- CVAR pass rate (45%): units with `CVAR <= cvmin`;
- area pass rate (25%): units within `areamin`–`areamax` (or `>= areamin` when no maximum);
- shape pass rate (20%): Polsby-Popper compactness `4πA/P² >= 0.20`;
- clean status (10%): full credit if `r.slopeunits.clean` succeeded.
- Sliver rate (reported): fraction of units with compactness `< 0.15`.
- The HTML report embeds a generated slope-unit preview PNG; batch/sweep summary CSV files include quality score/grade columns.

Grades: `>=85 Excellent`, `>=70 Good`, `>=55 Acceptable`, `<55 Needs review`.

The score is a **relative parameter-comparison heuristic**, not a ground-truth accuracy measure. See [QUALITY_SCORE.md](QUALITY_SCORE.md).

## 7. Uninstall

Use Windows **Apps & Features**, the installer's Uninstall button, or:

```powershell
%LOCALAPPDATA%\BasinSlopeTools\uninstall.cmd
```

Uninstalling removes the add-in, Tools, GRASS runtime, environment variable, quality-report history and uninstall registry entry, but not your DEMs or output GPKG/GDB files.

## 8. Known limitations

- Full-resolution 7,895 × 7,895 DEM runs can take a long time; tune with the fast preset or a smaller area first.
- Batch/sweep are sequential and have no cancellation API.
- Symbolisation/map insertion are not covered by CI.
- The installer is unsigned; SmartScreen may warn.
- Pro 3.0–3.6 are target-compatible but were not all tested on real installations.

## 9. Licence

GPL-2.0-or-later. See the repository root `LICENSE`, `BasinSlopeTools/Licenses/THIRD_PARTY_NOTICES.txt` and `SOURCE_OFFER.txt`.





