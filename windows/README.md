# AENV IPTV for Windows

Windows 10/11 · .NET 8 WPF · LibVLC (VLC engine)

## Install
Download `AENV-IPTV-Setup-x.y.z.exe` from the repository's **Releases** page and run it.

## Build the installer locally
Requirements: Windows 10/11 and the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`).
Inno Setup is installed automatically if it is missing.

```powershell
cd windows
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -Version 1.0.0
```
or double-click **`Build-Installer.cmd`**. The result is `dist\AENV-IPTV-Setup-1.0.0.exe`.

> **Downloaded the repository as a ZIP?** Windows marks files from the internet, and Smart App Control may
> block `Build-Installer.cmd`. Before extracting, right-click the ZIP → **Properties** → tick **Unblock** → OK.
> Cloning with `git clone` avoids this.

## Microsoft Store package (MSIX)
```powershell
.\build-msix.ps1 -Register        # local install for testing (Developer Mode)
.\build-msix.ps1 -Version 1.0.0 -IdentityName "…" -Publisher "CN=…" -PublisherDisplayName "…"
```
Full guide: [STORE.md](STORE.md).

## Run during development
```powershell
cd windows
dotnet run
```

## Installer
- Standard wizard: Welcome → License agreement → Install location → Start menu folder → Desktop shortcut →
  Ready → Install → Finish (with "Launch AENV IPTV")
- Silent install: `AENV-IPTV-Setup-x.y.z.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
- Languages: English, Turkish, German, Spanish; the chosen language becomes the app's initial language
- Per-user install without admin rights (all-users install optional)
- Start menu entry, optional desktop shortcut, uninstall via *Settings → Apps*
- Updating: build with a higher `-Version`; the installer upgrades the existing installation

## App languages
English, Azərbaycanca, Türkçe, Deutsch, Español. The system language is used on first start; it can be
changed at any time from the 🌐 picker in the sidebar. All strings live in `Services/Loc.cs`.

## Playback
Double-click (or Enter on) a channel to play it **inside the main window**. The ← Back button, Esc or
Backspace returns to the list with the last channel selected. Series open their seasons and episodes in
the same window as well.

## Keyboard shortcuts
| Key | Action |
|---|---|
| ↑ / ↓, PgUp / PgDn | previous / next channel |
| ← / → | live: channel list · movies: −10 s / +30 s |
| Space | play / pause |
| F11 or double-click | fullscreen |
| L · F · M · + / − | channel list · favorite · mute · volume |
| Esc / Backspace | close list → leave fullscreen → back to the channel list |
| Main window: Enter, F5, Ctrl+F | play, refresh, search |

## Data
Playlists, favorites and history are stored in `OneDrive\AenvIPTV\library.json` when OneDrive is
available (syncs between PCs), otherwise in `%AppData%\AenvIPTV\`. Crash logs: `%AppData%\AenvIPTV\crash.log`.

## Project structure
```
App.xaml(.cs)                 theme, VLC initialisation, error reporting
Assets/AenvIPTV.ico           app icon
Models/Models.cs              data models
Services/Loc.cs               localization (5 languages)
Services/M3UParser.cs         M3U / EXTINF and XSPF
Services/XtreamClient.cs      Xtream Codes API
Services/EpgService.cs        XMLTV + GZip
Services/LibraryStore.cs      persistence, favorites, OneDrive sync
Services/AppPaths.cs          data folders, migration from earlier app names
Views/                        main window, add-playlist dialog, embedded series and player views
installer/AenvIPTV.iss        Inno Setup script (+ installer/images: wizard artwork)
packaging/                    MSIX manifest and Store visual assets
Legal/                        EULA, privacy policy, third-party notices (shipped with the app)
build-msix.ps1                Store package build
build-installer.ps1           publish + installer build
Build-Installer.cmd           double-click wrapper for the script
```
