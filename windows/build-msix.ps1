# Builds the MSIX package for the Microsoft Store (dist\AENV-IPTV-x.y.z.msix).
#
# Store submission (the Store signs the package, no certificate needed):
#   .\build-msix.ps1 -Version 1.0.0 -IdentityName "12345Vusal.AENVIPTV" `
#                    -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Vusal Aliyev"
#   (exact values: Partner Center → your app → Product management → Product identity)
#
# Local test without a certificate (Windows "Developer Mode" must be on):
#   .\build-msix.ps1 -Register
#
# Requirements: Windows 10/11, .NET 8 SDK, Windows 10/11 SDK (makeappx.exe, makepri.exe).

param(
    [string]$Version = "1.0.0",
    [string]$IdentityName = "AENVIPTV.Local",
    [string]$Publisher = "CN=AENV-IPTV-Local",
    [string]$PublisherDisplayName = "Vusal Aliyev",
    [switch]$Register
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# MSIX needs a 4-part version; the Store requires the last part to be 0
$parts = @($Version.Split('.') | ForEach-Object { [int]$_ }) + @(0, 0, 0)
$msixVersion = "{0}.{1}.{2}.0" -f $parts[0], $parts[1], $parts[2]

$layout = Join-Path $PSScriptRoot "msix\layout"
if (Test-Path msix) { Remove-Item msix -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout, dist | Out-Null

Write-Host "`n[1/4] Publishing app..." -ForegroundColor Cyan
dotnet publish AenvIPTV.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -p:Version=$Version -o $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "`n[2/4] Writing manifest and assets..." -ForegroundColor Cyan
Copy-Item packaging\Assets (Join-Path $layout "Assets") -Recurse
$manifest = Get-Content packaging\AppxManifest.xml -Raw
$manifest = $manifest.Replace('$IDENTITY_NAME$', $IdentityName).Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($Publisher)) `
                     .Replace('$PUBLISHER_DISPLAY_NAME$', [Security.SecurityElement]::Escape($PublisherDisplayName)).Replace('$VERSION$', $msixVersion)
Set-Content -Path (Join-Path $layout "AppxManifest.xml") -Value $manifest -Encoding UTF8

if ($Register) {
    Write-Host "`nRegistering the app for local testing (Developer Mode)..." -ForegroundColor Cyan
    Get-AppxPackage -Name $IdentityName | Remove-AppxPackage -ErrorAction SilentlyContinue
}

Write-Host "`n[3/4] Locating Windows SDK tools..." -ForegroundColor Cyan
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "Windows SDK not found. Install it with: winget install Microsoft.WindowsSDK.10.0.26100" }
$bin = $makeappx.DirectoryName

Write-Host "`n[4/4] Building resources.pri and the package..." -ForegroundColor Cyan
& "$bin\makepri.exe" createconfig /cf msix\priconfig.xml /dq en-US /pv 10.0.0 /o | Out-Null
& "$bin\makepri.exe" new /pr $layout /cf msix\priconfig.xml /mn (Join-Path $layout "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri failed." }

if ($Register) {
    Add-AppxPackage -Register (Join-Path $layout "AppxManifest.xml")
    Write-Host "`nInstalled. Start 'AENV IPTV' from the Start menu." -ForegroundColor Green
    exit 0
}

$out = Join-Path $PSScriptRoot "dist\AENV-IPTV-$Version.msix"
& "$bin\makeappx.exe" pack /d $layout /p $out /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }

$size = [math]::Round((Get-Item $out).Length / 1MB, 1)
Write-Host "`nDone: $out  ($size MB, version $msixVersion)" -ForegroundColor Green
Write-Host "Upload this file in Partner Center. The Store signs it during certification." -ForegroundColor Green
