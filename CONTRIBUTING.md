# Contributing

[English](CONTRIBUTING.md) | [简体中文](CONTRIBUTING.zh-CN.md)

This is a research and teaching project. Bug reports and focused pull requests are welcome.

## Before opening a pull request

Keep each pull request focused. Describe the problem, the user-visible change, and how you checked it. For behavior changes, update `CHANGELOG.md`.

Run the same checks used by CI from the repository root:

```powershell
python -m unittest discover -s tests -v
python -m compileall -q BasinSlopeTools/Tools tests scripts
python scripts/check_markdown_links.py
```

If you change release metadata, also run:

```powershell
./scripts/check-version.ps1
```

Change `VERSION` when preparing a release with a new application version. Change `TOOLS_REVISION` whenever the deployed Python toolbox changes, so existing installations refresh their cached tools. Keep the corresponding markers in the add-in and installer scripts consistent; the version check verifies them.

The CI suite does not run ArcGIS Pro or the bundled GRASS workflow. If a change affects GIS execution, report the ArcGIS Pro and GRASS versions used and whether you tested it with a real or synthetic DEM. Clearly identify checks that could not be run.

## What not to commit

Do not commit generated or machine-specific artifacts, including:

- raster, vector, geodatabase, and project datasets;
- compiled assemblies, installers, add-in packages, archives, and other generated binaries;
- virtual-environment contents, dependency caches, and IDE state files;
- logs containing personal paths, credentials, tokens, or other private information.

Small synthetic fixtures that are necessary for automated tests may be committed when they are reproducible, appropriately licensed, and contain no private or restricted data. Do not attach real research data unless you have permission to share it.

## Code and documentation

- Keep Python dependencies minimal. Keep ArcPy and GRASS runtime calls in the toolbox or engine layer where practical.
- Follow the existing C#/.NET and ArcGIS Pro SDK patterns; explain any new dependency in the pull request.
- Preserve upstream copyright and license notices when editing bundled GRASS add-on code.
- Update user documentation when commands, parameters, outputs, or installation behavior change.

## Reporting a bug

Use the bug report form when possible. Include the ArcGIS Pro and Windows versions, DEM format and coordinate system, relevant parameter values, and whether paths contain spaces or non-ASCII characters. Attach relevant log excerpts after removing personal paths and other sensitive information. A small synthetic DEM is preferable; only share real data when you are authorized to do so.
