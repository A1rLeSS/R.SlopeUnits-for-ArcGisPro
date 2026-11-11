# BasinSlopeTools portable installer with rollback
# Target: ArcGIS Pro 3.x; no separate GRASS installation required.

$ErrorActionPreference = "Stop"

$here = $PSScriptRoot
$addin = Join-Path $here "BasinSlopeTools.esriAddinX"
$runtime = Join-Path $here "runtime\grass"

if (-not (Test-Path $addin)) {
    throw "Add-in not found: $addin"
}
if (-not (Test-Path (Join-Path $runtime "bin\grass85.bat"))) {
    throw "Portable GRASS not found: $runtime"
}

$proFound = $false
$proExe = $null
$proCandidates = @(
    "C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe",
    "D:\ArcGISPro\bin\ArcGISPro.exe",
    "C:\Program Files (x86)\ArcGIS\Pro\bin\ArcGISPro.exe"
)
foreach ($key in @("HKLM:\SOFTWARE\ESRI\ArcGISPro", "HKCU:\SOFTWARE\ESRI\ArcGISPro")) {
    try {
        $installDir = (Get-ItemProperty -Path $key -Name InstallDir -ErrorAction Stop).InstallDir
        if ($installDir) {
            $proCandidates += (Join-Path $installDir "bin\ArcGISPro.exe")
        }
    } catch { }
}
foreach ($p in $proCandidates) {
    if (Test-Path $p) { $proFound = $true; $proExe = $p; break }
}
if (-not $proFound) {
    foreach ($key in @("HKLM:\SOFTWARE\ESRI\ArcGISPro", "HKLM:\SOFTWARE\WOW6432Node\ESRI\ArcGISPro", "HKCU:\SOFTWARE\ESRI\ArcGISPro")) {
        if (Test-Path $key) { $proFound = $true; break }
    }
}
if (-not $proFound) {
    throw "ArcGIS Pro not found. Install ArcGIS Pro 3.x before this add-in."
}
if (Get-Process -Name ArcGISPro -ErrorAction SilentlyContinue) {
    throw "ArcGIS Pro is running. Close it before installing this add-in."
}
if ($proExe) {
    $proVersionText = (Get-Item $proExe).VersionInfo.FileVersion
    $proVersion = $null
    try { $proVersion = [version]$proVersionText } catch { }
    if ($proVersion -and ($proVersion.Major -ne 3 -or $proVersion.Minor -gt 7)) {
        throw "ArcGIS Pro 3.0-3.7 is required. Found $proVersionText."
    }
}

$localItem = Get-Item $env:LOCALAPPDATA
if ($localItem.PSDrive -and $localItem.PSDrive.Free -lt 1800MB) {
    throw "Not enough free space on $($localItem.PSDrive.Name): 1.8 GB required."
}

$docs = [Environment]::GetFolderPath("MyDocuments")
$addinDir = Join-Path $docs "ArcGIS\AddIns\ArcGISPro\{7D2C4A9E-3F1B-4A77-9B6E-6B1C2D9A4E11}"
$addinTarget = Join-Path $addinDir "BasinSlopeTools.esriAddinX"

$toolsSrc = Join-Path $PSScriptRoot "Tools"
$toolsDst = Join-Path $env:LOCALAPPDATA "BasinSlopeTools\tools"

$runtimeRoot = Join-Path $env:LOCALAPPDATA "BasinSlopeTools\runtime"
$runtimeDst = Join-Path $runtimeRoot "grass"

$base = Join-Path $env:LOCALAPPDATA "BasinSlopeTools"
$uninstallPs1 = Join-Path $base "uninstall.ps1"
$uninstallCmd = Join-Path $base "uninstall.cmd"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BasinSlopeTools"
$uninstallKeyReg = "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BasinSlopeTools"

$backupRoot = Join-Path $env:LOCALAPPDATA ("BasinSlopeTools\.install_backup_" + [guid]::NewGuid().ToString("N"))
$backupAddin = Join-Path $backupRoot "addin.esriAddinX"
$backupTools = Join-Path $backupRoot "tools"
$backupRuntime = Join-Path $backupRoot "runtime"
$backupUninstallPs1 = Join-Path $backupRoot "uninstall.ps1"
$backupUninstallCmd = Join-Path $backupRoot "uninstall.cmd"
$backupArp = Join-Path $backupRoot "arp.reg"

$hadAddin = Test-Path $addinTarget
$hadTools = Test-Path $toolsDst
$hadRuntime = Test-Path $runtimeDst
$hadUninstallPs1 = Test-Path $uninstallPs1
$hadUninstallCmd = Test-Path $uninstallCmd
$hadArp = Test-Path $uninstallKey
$oldGrass = [Environment]::GetEnvironmentVariable("BASINSLOPE_GRASS", "User")
$success = $false
$restored = $false

try {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    if ($hadAddin) { Copy-Item -LiteralPath $addinTarget -Destination $backupAddin -Force }
    if ($hadTools) { Copy-Item -LiteralPath $toolsDst -Destination $backupTools -Recurse -Force }
    if ($hadUninstallPs1) { Copy-Item -LiteralPath $uninstallPs1 -Destination $backupUninstallPs1 -Force }
    if ($hadUninstallCmd) { Copy-Item -LiteralPath $uninstallCmd -Destination $backupUninstallCmd -Force }
    if ($hadArp) { reg.exe export $uninstallKeyReg $backupArp /y 2>$null | Out-Null }
    if ($hadRuntime) { Move-Item -LiteralPath $runtimeDst -Destination $backupRuntime -Force }

    # Install Add-in
    Write-Host "Installing Add-in..."
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    Copy-Item -LiteralPath $addin -Destination $addinTarget -Force

    # Deploy Tools next to the runtime so upgrades never reuse old cached tools.
    Write-Host "Deploying Tools..."
    if (Test-Path $toolsSrc) {
        if (Test-Path $toolsDst) {
            Remove-Item -LiteralPath $toolsDst -Recurse -Force
        }
        New-Item -ItemType Directory -Path $toolsDst -Force | Out-Null
        Copy-Item -Path (Join-Path $toolsSrc "*") -Destination $toolsDst -Recurse -Force
        Set-Content -LiteralPath (Join-Path $toolsDst ".tools_revision") -Value "2026-11-05.9" -Encoding ASCII
    }

    # Install GRASS runtime
    Write-Host "Copying GRASS runtime (this may take a few minutes)..."
    New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    robocopy $runtime $runtimeDst /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Failed to copy GRASS runtime, robocopy exit=$LASTEXITCODE"
    }

    $grassBat = Join-Path $runtimeDst "bin\grass85.bat"
    [Environment]::SetEnvironmentVariable("BASINSLOPE_GRASS", $grassBat, "User")

    $uninstallBody = @'
$ErrorActionPreference = "Stop"
if (Get-Process -Name ArcGISPro -ErrorAction SilentlyContinue) {
    throw "ArcGIS Pro is running. Close it before uninstalling."
}
$addinDir = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "ArcGIS\AddIns\ArcGISPro\{7D2C4A9E-3F1B-4A77-9B6E-6B1C2D9A4E11}"
$addin = Join-Path $addinDir "BasinSlopeTools.esriAddinX"
Remove-Item -LiteralPath $addin -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $addinDir -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $addin) { throw "Add-in is locked. Close ArcGIS Pro and retry." }
$base = Join-Path $env:LOCALAPPDATA "BasinSlopeTools"
Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $base) { throw "Runtime directory is locked. Close related processes and retry." }
[Environment]::SetEnvironmentVariable("BASINSLOPE_GRASS", $null, "User")
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BasinSlopeTools" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Uninstall complete. Restart ArcGIS Pro."
'@
    Set-Content -LiteralPath $uninstallPs1 -Value $uninstallBody -Encoding ASCII
    Set-Content -LiteralPath $uninstallCmd -Value "@echo off`r`npowershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"%~dp0uninstall.ps1`"`r`npause`r`n" -Encoding ASCII

    Write-Host "Registering uninstall entry..."
    New-Item -Path $uninstallKey -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "R.slopeunits Slope Units for ArcGIS Pro" -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "0.4" -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "BasinSlopeCoupling" -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $base -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$uninstallPs1`"" -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null

    $success = $true
} catch {
    $failure = $_
    try {
        if ($hadAddin -and (Test-Path $backupAddin)) {
            New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
            Copy-Item -LiteralPath $backupAddin -Destination $addinTarget -Force
        } else {
            Remove-Item -LiteralPath $addinTarget -Force -ErrorAction SilentlyContinue
            if (Test-Path $addinDir) { Remove-Item -LiteralPath $addinDir -Recurse -Force -ErrorAction SilentlyContinue }
        }
        if ($hadTools -and (Test-Path $backupTools)) {
            Remove-Item -LiteralPath $toolsDst -Recurse -Force -ErrorAction SilentlyContinue
            Copy-Item -LiteralPath $backupTools -Destination $toolsDst -Recurse -Force
        } else {
            Remove-Item -LiteralPath $toolsDst -Recurse -Force -ErrorAction SilentlyContinue
        }
        if ($hadRuntime -and (Test-Path $backupRuntime)) {
            Remove-Item -LiteralPath $runtimeDst -Recurse -Force -ErrorAction SilentlyContinue
            Move-Item -LiteralPath $backupRuntime -Destination $runtimeDst -Force
        } else {
            Remove-Item -LiteralPath $runtimeDst -Recurse -Force -ErrorAction SilentlyContinue
        }
        if ($hadUninstallPs1 -and (Test-Path $backupUninstallPs1)) {
            Copy-Item -LiteralPath $backupUninstallPs1 -Destination $uninstallPs1 -Force
        } else {
            Remove-Item -LiteralPath $uninstallPs1 -Force -ErrorAction SilentlyContinue
        }
        if ($hadUninstallCmd -and (Test-Path $backupUninstallCmd)) {
            Copy-Item -LiteralPath $backupUninstallCmd -Destination $uninstallCmd -Force
        } else {
            Remove-Item -LiteralPath $uninstallCmd -Force -ErrorAction SilentlyContinue
        }
        [Environment]::SetEnvironmentVariable("BASINSLOPE_GRASS", $oldGrass, "User")
        if ($hadArp -and (Test-Path $backupArp)) {
            reg.exe import $backupArp 2>$null | Out-Null
        } else {
            Remove-Item $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
        }
        $restored = $true
    } catch {
        Write-Warning "Rollback failed: $($_.Exception.Message)"
        Write-Warning "Backup retained at: $backupRoot"
    }
    throw $failure
} finally {
    if ($success -or $restored) {
        if (Test-Path $backupRoot) {
            Remove-Item -LiteralPath $backupRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $backupRoot) {
            # 某些杀软/资源管理器会短暂占用目录，用 cmd 兜底再删一次。
            cmd.exe /c rmdir /s /q "$backupRoot" 2>$null | Out-Null
        }
    }
}

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Add-in: $addinTarget"
Write-Host "GRASS:  $grassBat"
Write-Host "Uninstall: $uninstallCmd"
Write-Host ""
Write-Host "Close and reopen ArcGIS Pro. The 'R.slopeunits Slope Units' tab should appear."
Write-Host "If not, enable BasinSlopeTools in the ArcGIS Pro Add-In Manager."







