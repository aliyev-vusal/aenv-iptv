@echo off
title AENV IPTV - building installer
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 8 SDK not found, installing...
    winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
    set "PATH=%PATH%;%ProgramFiles%\dotnet"
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
if errorlevel 1 (
    echo.
    echo Build failed. See the messages above.
    pause
    exit /b 1
)

echo.
echo Done. The installer is in the "dist" folder.
pause
