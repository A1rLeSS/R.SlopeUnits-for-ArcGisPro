$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
$tools = (Get-Content (Join-Path $root "TOOLS_REVISION") -Raw).Trim()

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION must be a three-part semantic version, got: $version"
}
if ($tools -notmatch '^\d{4}-\d{2}-\d{2}\.\d+$') {
    throw "TOOLS_REVISION must look like 2026-11-05.9, got: $tools"
}

$shortVersion = ($version -split '\.')[0..1] -join '.'

$daml = Get-Content (Join-Path $root "BasinSlopeTools\Config.daml") -Raw
if ($daml -notmatch ('<AddInInfo[^>]*version="' + [regex]::Escape($shortVersion) + '"')) {
    throw "Config.daml does not declare add-in version $shortVersion"
}

$csproj = Get-Content (Join-Path $root "Installer\BasinSlopeToolsInstaller.csproj") -Raw
if ($csproj -notmatch ('<Version>' + [regex]::Escape($version) + '</Version>')) {
    throw "Installer csproj does not declare Version $version"
}

$buttons = Get-Content (Join-Path $root "BasinSlopeTools\Buttons.cs") -Raw
if ($buttons -notmatch [regex]::Escape($tools)) {
    throw "Buttons.cs ToolsRevision does not contain $tools"
}

$install = Get-Content (Join-Path $root "dist\install.ps1") -Raw
if ($install -notmatch [regex]::Escape($tools)) {
    throw "dist/install.ps1 does not contain Tools revision $tools"
}

Write-Host "Version OK: app $version, add-in $shortVersion, Tools $tools" -ForegroundColor Green
