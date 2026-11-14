# Security Policy

## Supported version

| Version | Supported |
|---|---|
| 0.5.x | Yes |
| 0.4.x and earlier | No |

Security fixes are prepared for the latest minor release. Upgrade to the latest patch release in the supported line when one is available. The project is maintained on a best-effort basis; no fixed response-time SLA is offered.

## Reporting

Report security-relevant issues privately to **leomilk362@gmail.com**. Do not publish exploitable details before a fix is available.

## Scope notes

- The installer is **not code-signed**. Download only from this repository's GitHub Releases and verify `SHA256SUMS.txt`.
- The add-in executes a bundled Python Toolbox and a locally installed GRASS runtime. ArcGIS Pro may ask for permission to run third-party Python code; only accept if you trust this repository and the release checksum.
- The project does not collect telemetry or upload DEMs/results.
- Do not run untrusted `.pyt`/`.ps1` files from third parties under the same privileges.
