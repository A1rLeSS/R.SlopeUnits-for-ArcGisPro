# Contributing

This is primarily a research/course project. Issues and focused pull requests are welcome.

## Before opening a PR

1. Run the pure Python tests:

   ```powershell
   python -m unittest discover -s tests -v
   ```

2. Run `.\scripts\check-version.ps1` if you changed any version marker.
3. Do not commit `Data_Raw/`, `output/`, `runtime/grass/`, `Installer/payload.zip` or ArcGIS Pro build outputs.
4. Keep the scope clear: the add-in generates **slope units**, not watershed polygons.

## Code style

- Python: standard library where possible; keep GRASS/ArcPy calls in the toolbox/engine layer.
- C#: .NET 6, WPF, no additional packages unless required.
- Document any user-visible behaviour change in `CHANGELOG.md`.

## Reporting bugs

Include:

- ArcGIS Pro version and Windows version;
- whether the DEM is GeoTIFF or ESRI Grid (`hdr.adf`);
- input path characteristics (ASCII/Chinese) and whether the path contains spaces;
- `%TEMP%\R_slopeunits_*.log` and `%TEMP%\BasinSlopeTools_Setup.log` where relevant;
- a small reproducible DEM if possible.
