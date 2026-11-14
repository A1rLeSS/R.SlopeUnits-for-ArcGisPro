param(
    [string]$GrassRuntime = $env:GRASS_RUNTIME,
    [string]$ArcGISProSDKTargets = $env:ARCGIS_PRO_SDK_TARGETS,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-Path($path, $description) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) {
        throw "$description not found: $path"
    }
}

if (-not $SkipTests) {
    Write-Host "==> Running tests" -ForegroundColor Cyan
    python -m unittest discover -s (Join-Path $root "tests") -v
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

Write-Host "==> Checking version markers" -ForegroundColor Cyan
& (Join-Path $root "scripts\check-version.ps1")

if (-not $GrassRuntime) {
    throw "GrassRuntime is required. Pass -GrassRuntime <path to grass85.bat> or set GRASS_RUNTIME."
}
Assert-Path $GrassRuntime "GRASS runtime"
$grassRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $GrassRuntime))
$runtimeGrass = Join-Path $grassRoot "grass"
if (-not (Test-Path (Join-Path $runtimeGrass "bin\grass85.bat"))) {
    throw "Could not resolve the runtime root from $GrassRuntime. Expected <runtime>\bin\grass85.bat."
}

if (-not $ArcGISProSDKTargets) {
    $ArcGISProSDKTargets = "D:\ArcGISPro\bin\Esri.ProApp.SDK.Desktop.targets"
}
if (-not (Test-Path -LiteralPath $ArcGISProSDKTargets)) {
    throw "ArcGIS Pro SDK targets not found: $ArcGISProSDKTargets"
}

$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
$toolsRevision = (Get-Content (Join-Path $root "TOOLS_REVISION") -Raw).Trim()
$artifacts = Join-Path $root "artifacts"
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

Write-Host "==> Building add-in" -ForegroundColor Cyan
dotnet build (Join-Path $root "BasinSlopeTools\BasinSlopeTools.csproj") `
    -c Release -p:ArcGISProSDKTargets="$ArcGISProSDKTargets"
if ($LASTEXITCODE -ne 0) { throw "Add-in build failed" }

$addin = Join-Path $root "BasinSlopeTools\bin\Release\net6.0-windows\BasinSlopeTools.esriAddinX"
Assert-Path $addin "Built add-in"

$dist = Join-Path $root "dist"
Copy-Item -LiteralPath $addin -Destination (Join-Path $dist "BasinSlopeTools.esriAddinX") -Force

# Keep a clean copy of the Python toolbox in dist.
$toolsSrc = Join-Path $root "BasinSlopeTools\Tools"
$toolsDst = Join-Path $dist "Tools"
Get-ChildItem $toolsSrc -Recurse -Directory -Filter "__pycache__" -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $toolsDst) { Remove-Item $toolsDst -Recurse -Force }
robocopy $toolsSrc $toolsDst /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy Tools failed: $LASTEXITCODE" }

$distRuntime = Join-Path $dist "runtime\grass"
if (Test-Path $distRuntime) { Remove-Item $distRuntime -Recurse -Force }
robocopy $runtimeGrass $distRuntime /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy GRASS runtime failed: $LASTEXITCODE" }

$buildDate = Get-Date -Format "yyyyMMdd"
$portable = Join-Path $artifacts "BasinSlopeTools_Portable_${buildDate}.zip"
if (Test-Path $portable) { Remove-Item $portable -Force }
tar.exe -a -c -f $portable -C $dist .
if ($LASTEXITCODE -ne 0) { throw "Portable archive failed" }

if (-not $SkipInstaller) {
    Write-Host "==> Building installer" -ForegroundColor Cyan
    Copy-Item -LiteralPath $portable -Destination (Join-Path $root "Installer\payload.zip") -Force

    $publish = Join-Path $root "Installer\publish"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish (Join-Path $root "Installer\BasinSlopeToolsInstaller.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Installer publish failed" }

    $shortVersion = ($version -split '\.')[0..1] -join '.'
    Copy-Item -LiteralPath (Join-Path $publish "BasinSlopeTools_Setup.exe") `
        -Destination (Join-Path $artifacts "BasinSlopeTools_Setup_${shortVersion}.exe") -Force
}

$docsZip = Join-Path $artifacts "BasinSlopeTools_${version}_docs.zip"
if (Test-Path $docsZip) { Remove-Item $docsZip -Force }
$docsStage = Join-Path $env:TEMP ("bst_docs_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $docsStage | Out-Null
Copy-Item (Join-Path $root "README.md") $docsStage
Copy-Item (Join-Path $root "README.zh-CN.md") $docsStage
Copy-Item (Join-Path $root "CHANGELOG.md") $docsStage
Copy-Item (Join-Path $root "LICENSE") $docsStage
Copy-Item (Join-Path $root "LICENSE.md") $docsStage
$docsDir = Join-Path $docsStage "docs"
New-Item -ItemType Directory $docsDir | Out-Null
Copy-Item (Join-Path $root "docs\*") $docsDir -Recurse
$distDocs = Join-Path $docsStage "dist"
New-Item -ItemType Directory $distDocs | Out-Null
Copy-Item (Join-Path $root "dist\README*.md") $distDocs
Copy-Item (Join-Path $root "dist\LICENSE*.md") $distDocs
Copy-Item (Join-Path $root "dist\THIRD_PARTY_NOTICES.txt") $distDocs
Copy-Item (Join-Path $root "dist\SOURCE_OFFER.txt") $distDocs
Compress-Archive -Path (Join-Path $docsStage "*") -DestinationPath $docsZip -CompressionLevel Optimal
Remove-Item $docsStage -Recurse -Force

$hashFile = Join-Path $artifacts "SHA256SUMS.txt"
Get-ChildItem $artifacts -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash  $($_.Name)"
    } | Set-Content -LiteralPath $hashFile -Encoding ASCII

Write-Host "==> Done" -ForegroundColor Green
Get-ChildItem $artifacts -File | Select-Object Name, Length
Get-Content $hashFile




