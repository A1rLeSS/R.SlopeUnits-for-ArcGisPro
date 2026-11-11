# Changelog

All notable changes to this project are documented here. The application version follows `VERSION`; the Tools cache revision follows `TOOLS_REVISION`.

## 0.4.0 — 2026-11-11

First public GitHub release.

### Added

- ArcGIS Pro add-in with five commands: single DEM, batch, parameter sweep, quality report and about/licences.
- Python Toolbox backed by GRASS GIS 8.5 `r.slopeunits.create` / `r.slopeunits.clean`.
- Portable installer with licence acceptance, three-step wizard, progress bar and detailed event log.
- ESRI Grid / `.adf` DEM support: select `hdr.adf`; the parent grid directory is resolved automatically.
- Non-ASCII and Chinese path support through an ASCII temporary link/copy before GRASS starts.
- `65535` / `-9999` DEM value masking before GRASS processing.
- Quality report history, previous-report comparison, and a 0–100 generation quality score.
- Batch/sweep resumable state and per-task `batch_summary.csv` / `sweep_summary.csv`.

### Fixed

- Installer Pro version check now targets ArcGIS Pro 3.0–3.7 explicitly.
- Installer finish-page button overlap and close-during-install guard.
- Empty backup directory left after successful installation.
- Quality report opened the oldest rather than newest history entry.
- Quality report feature count when `AREA_M2` is missing.
- Log filename collisions within the same second.
- Unbounded growth of quality-report history files.
- GDB output documentation now uses `<output>.gdb`.

### Known limitations

- Full 7,895 × 7,895 DEM benchmark is not included.
- Batch/sweep are sequential and do not expose cancellation.
- Symbolisation and add-to-map are not CI-tested.
- Installer is unsigned.
- 3.0–3.6 target compatibility is not verified on every Pro release.
- The quality score is a relative heuristic, not an absolute accuracy metric.
