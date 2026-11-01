# Builds the AENV IPTV installer (dist\AENV-IPTV-Setup-x.y.z.exe).
# Usage:  powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 [-Version 1.1.0] [-Url https://github.com/<user>/aenv-iptv]

param([string]$Version = "1.0.0", [string]$Url = "", [switch]$NoExplorer)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "`n[1/3] Publishing app (self-contained, no .NET install needed)..." -ForegroundColor Cyan
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish AenvIPTV.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -p:Version=$Version -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "`n[2/3] Looking for Inno Setup..." -ForegroundColor Cyan
function Find-Iscc {
    $paths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $paths) { if (Test-Path $p) { return $p } }
    return $null
}
$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host "Inno Setup not found, installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
    $iscc = Find-Iscc
    if (-not $iscc) { throw "Inno Setup could not be installed. Get it from https://jrsoftware.org/isdl.php" }
}

Write-Host "`n[3/3] Building installer..." -ForegroundColor Cyan
$isccArgs = @("/DMyAppVersion=$Version")
if ($Url) { $isccArgs += "/DMyAppURL=$Url" }
& $iscc @isccArgs "installer\AenvIPTV.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }

$out = Join-Path $PSScriptRoot "dist\AENV-IPTV-Setup-$Version.exe"
$size = [math]::Round((Get-Item $out).Length / 1MB, 1)
Write-Host "`nDone: $out  ($size MB)" -ForegroundColor Green
if (-not $NoExplorer) { explorer.exe /select,"$out" }
