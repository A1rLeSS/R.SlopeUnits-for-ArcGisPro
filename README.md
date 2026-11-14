# R.slopeunits for ArcGIS Pro

[English](README.md) | [简体中文](README.zh-CN.md) | [Installation guide](docs/Installation.md) | [中文安装说明](docs/安装说明.md)

ArcGIS Pro add-in and Python toolbox that run the GRASS GIS `r.slopeunits.create` and `r.slopeunits.clean` modules to generate slope units.

This project is a work in progress aimed at reproducible, local, offline slope-unit generation. It is **not** an official Esri or GRASS GIS product.

## Download

The repository contains the add-in source, Python toolbox, installer source, and release documentation. User guides are available in [English](docs/Installation.md) and [Simplified Chinese](docs/安装说明.md).

Prebuilt packages are attached to [GitHub Releases](https://github.com/A1rLeSS/R.SlopeUnits-for-ArcGisPro/releases/latest):

- the `BasinSlopeTools_Setup_<version>.exe` installer and `BasinSlopeTools_Portable_<date>.zip` package;
- `SHA256SUMS.txt` with checksums for the assets attached to that release.

The installer is not code-signed, so Windows SmartScreen may show an unknown-publisher warning.

## Features

The add-in adds a **R.slopeunits** tab with six commands:

| Command | Purpose |
|---|---|
| Generate slope units | Single DEM → `r.slopeunits.create` + `clean` → GeoPackage, GDB, exports and ZIP bundle |
| Batch generate | Process multiple DEMs with resumable state |
| Parameter sweep | Scan `thresh` / `areamin` / `cvmin` / `maxiteration`, resumable |
| Quality report | Open the latest report and compare it with the previous run |
| Check for updates | Check GitHub Release, show bilingual notes and download a verified installer |
| About / licenses | Version, GPL notice, third-party notices and source offer |

Additional behaviour:

- ESRI Grid DEMs: select `hdr.adf`; the toolbox resolves the parent grid directory.
- Non-ASCII / Chinese Windows paths are bridged through an ASCII temporary link or copy before GRASS starts.
- `65535` and `-9999` DEM values are masked with `r.null` before processing.
- The quality report includes a 0–100 **generation quality score** based on CVAR pass rate (45%), area pass rate (25%), shape compactness (20%) and clean status (10%), plus a rendered preview PNG.
- Batch and sweep tools write `batch_summary.csv` / `sweep_summary.csv`.
- Outputs include `AREA_M2`, `CVAR`, `*_run_manifest.json`, and HTML/JSON/CSV quality reports.
- Optional Shapefile/GeoJSON/CSV exports and a one-click `*_bundle.zip` containing GPKG/GDB, reports, preview, manifest and exports.
- Update window checks the latest GitHub Release, shows bilingual notes, verifies the installer SHA256 and starts a deferred updater that waits for ArcGIS Pro to close.

## Requirements

- Windows x64
- ArcGIS Pro **3.0–3.7** (tested on 3.7)
- A projected DEM in metres; geographic-coordinate DEMs are rejected
- For building: .NET 6 Windows SDK for the add-in and .NET 10 SDK for the installer
- GRASS GIS 8.5 runtime for local execution; the packaged installer bundles it

## Install

1. Close ArcGIS Pro.
2. Run `BasinSlopeTools_Setup_0.6.exe`.
3. Choose **English** or **简体中文**, read and accept the bilingual licence, then follow the percentage progress and detailed event log.
4. Reopen ArcGIS Pro and open the **R.slopeunits** tab.
5. On first use, allow the Python Toolbox to run when Pro asks.

Silent installation requires explicit licence acceptance:

```powershell
.\BasinSlopeTools_Setup_0.6.exe --silent --accept-license
```

Uninstall from Windows Apps & Features or with:

```powershell
.\BasinSlopeTools_Setup_0.6.exe --uninstall
```

Close ArcGIS Pro and any running GRASS analysis before uninstalling.

## Build

The GRASS runtime and `Installer\payload.zip` are intentionally not stored in Git because they are large. The build script expects a local GRASS runtime.

```powershell
# Optional: point at your ArcGIS Pro SDK targets file if it is not at the default path.
$env:ARCGIS_PRO_SDK_TARGETS = 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'

.\scripts\build.ps1 `
  -GrassRuntime 'D:\path\to\grass\bin\grass85.bat' `
  -ArcGISProSDKTargets $env:ARCGIS_PRO_SDK_TARGETS
```

For an add-in-only debug build:

```powershell
dotnet build .\BasinSlopeTools\BasinSlopeTools.csproj -c Release
```

## Test

Pure Python helpers are tested without ArcGIS Pro or ArcPy:

```powershell
python -m unittest discover -s tests -v
```

The test suite covers ADF path normalisation, DEM list handling and the quality assessment formula. Integration tests with ArcGIS Pro and GRASS are documented in [docs/VALIDATION.md](docs/VALIDATION.md) and are not run in CI because they require licensed Esri software and a local GRASS runtime.

Release history is available in [CHANGELOG.md](CHANGELOG.md) (English) and [CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md) (简体中文). Performance and GPU feasibility are assessed in [docs/PERFORMANCE.md](docs/PERFORMANCE.md).

## Repository layout

```text
BasinSlopeTools/        ArcGIS Pro add-in, Python Toolbox and GRASS add-on scripts
Installer/              Windows installer source
dist/                   portable installer scripts and distribution notices
docs/                   installation, build, validation and quality-score documentation
scripts/                build and version checks
tests/                  dependency-free Python tests
```

## Versioning

The app version and the Tools revision are stored in:

- `VERSION`
- `TOOLS_REVISION`

`scripts/check-version.ps1` verifies that the C# project, DAML file, installer script and add-in cache marker agree. Do not edit generated release artifacts by hand.

## Known limitations

- Full-resolution 7,895 × 7,895 DEM runs are computationally expensive; use a small area or the fast preset while tuning.
- ArcGIS Pro 3.0–3.6 are supported by the target framework but were not all tested on real installations.
- The quality score is a heuristic for comparing parameter sets in the same area; it is not an absolute terrain-accuracy metric.
- Batch/sweep runs are sequential. There is no task queue or parallel execution yet.
- Symbolisation and “add to map” are best-effort in a Pro map session and are not covered by CI.
- The installer is unsigned and the Esri/GRASS licence boundary should be reviewed before commercial redistribution.

## AI assistance disclosure

Parts of the code, documentation, tests and release tooling in this repository were generated or edited with assistance from **DeepSeek Harness** and/or **ChatGPT Codex**. All AI-assisted changes were reviewed, tested and adapted by the maintainer. AI systems are not project authors and do not change the GPL-2.0-or-later licensing.

## Licence

The project is distributed under **GPL-2.0-or-later**. See [LICENSE](LICENSE), [BasinSlopeTools/Licenses](BasinSlopeTools/Licenses), [THIRD_PARTY_NOTICES.txt](BasinSlopeTools/Licenses/THIRD_PARTY_NOTICES.txt) and [SOURCE_OFFER.txt](BasinSlopeTools/Licenses/SOURCE_OFFER.txt).

GRASS GIS and bundled runtime components retain their own licences. ArcGIS Pro SDK and ArcPy are Esri products and are not redistributed by this repository.








