# Validation Record

This file records the manual checks performed for the 0.5.0 release. It is not a substitute for an independent accuracy assessment.

## Environment

- Windows 10/11, x64
- ArcGIS Pro 3.7 (target 3.0–3.7)
- .NET 6 add-in, .NET 10 installer
- GRASS GIS 8.5 portable runtime

## Checks performed

| Check | Result |
|---|---|
| Installer welcome/licence/confirm/progress/finish | Passed; licence acceptance required |
| `--silent` without `--accept-license` | Exit `2` |
| `--silent --accept-license` | Exit `0` |
| `--dry-run` | Exit `0` |
| Uninstall and reinstall | Passed; close GRASS/Pro before uninstall |
| Backup directory after successful install | `0` residual directories |
| Small DEM `create + clean + CVAR + AREA_M2 + v.out.ogr` | Passed |
| ESRI Grid (`hdr.adf`) input | Passed |
| `65535`/`-9999` masking (`DEM_SENTINEL_MASK`) | Passed on a synthetic grid |
| Chinese input path | Passed using ASCII junction |
| Chinese output path | Passed using temporary output and copy-back |
| Quality report HTML/JSON/CSV generation | Passed |
| Quality score calculation | Mock feature class: score 76.7 with shape pass; shape/sliver columns present |
| Quality report preview PNG | Generated and embedded in HTML |
| Quality score recorded in HTML/CSV/JSON | Passed |
| Batch/sweep summary score columns | Implemented; live multi-DEM run still recommended |

The full 7,895 × 7,895 production DEM was **not** run end-to-end in the automated test session because of runtime; ADF recognition, projection validation and GRASS import were verified separately.

## Not covered by automated tests

- Batch and parameter-sweep end-to-end on real DEMs.
- Symbolisation and “add to map” in a live Pro map.
- Pro 3.0–3.6 installations.
- Full-resolution performance benchmark.

## Recommended acceptance experiment

For a thesis/report, run at least:

- 2–3 small catchments;
- 3 threshold/area parameter sets;
- 1 fast-preview and 1 full-quality run.

Record unit count, median area, median CVAR, quality score, runtime and a visual check of representative units. Treat the score as comparative, not absolute.

