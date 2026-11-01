# AENV IPTV

<img src="windows/Assets/AenvIPTV.png" width="96" align="right" alt="AENV IPTV icon">

A personal IPTV player for **Windows**, **iPhone**, **iPad**, **Apple TV** and **Mac**.

AENV IPTV is a player only. It does not provide, sell or link to any channels or content —
you add your own legal playlist (M3U, XSPF or Xtream Codes).

**Languages:** English · Azərbaycanca · Türkçe · Deutsch · Español

## Download (Windows)
- **Microsoft Store** (recommended once published): signed by Microsoft, automatic updates.
- **Direct download:** open **[Releases](../../releases/latest)**, download `AENV-IPTV-Setup-x.y.z.exe`,
  double-click it and follow the standard setup wizard (Welcome → License → Location → Start menu →
  Ready → Finish).

No admin rights and no separate .NET installation are required.

> The installer is not code-signed yet. Windows SmartScreen may show *"Windows protected your PC"* →
> click **More info → Run anyway**. On PCs with **Smart App Control** enabled, unsigned apps are blocked
> entirely; see [Code signing](#code-signing).

## Features
- M3U / M3U8, XSPF and Xtream Codes API playlists, local `.m3u` files
- XMLTV EPG (`.xml` / `.xml.gz`) with now/next and progress
- Live TV, movies and series with groups, search and tile / list view
- Favorites and recently watched
- Plays inside the main window: switch channels while watching, channel list panel, fullscreen, Back / Esc to return
- **Windows:** VLC engine (HLS, MPEG-TS, RTMP, RTSP, UDP, MKV …), OneDrive sync between PCs
- **Apple:** SwiftUI, one codebase for iOS / iPadOS / tvOS / macOS, iCloud sync

## Repository layout
| Folder | Content |
|---|---|
| [`windows/`](windows) | .NET 8 WPF app + Inno Setup installer |
| [`apple/`](apple) | SwiftUI app for iOS, iPadOS, tvOS and macOS (XcodeGen) |
| [`tests/`](tests) | Automated tests (run on Windows, macOS or Linux) |
| [`docs/demo/`](docs/demo) | Original demo channels, films, EPG and playlist (generated, free to use) |
| [`store-assets/`](store-assets) | Microsoft Store screenshots and artwork |
| [`tools/`](tools) | Scripts that generate the demo content and store artwork |
| [`.github/workflows/`](.github/workflows) | CI: tests on every push, installer on demand / per tag |

## Building the Windows installer
- **On GitHub (recommended):** *Actions → Build installer → Run workflow*. The installer appears as a
  downloadable artifact. Pushing a tag such as `v1.1.0` also publishes a Release.
- **Locally:** see [`windows/README.md`](windows/README.md).

## Tests
`tests/AenvIPTV.Tests` covers the playlist parser (M3U, XSPF, 50 000-entry performance), XMLTV EPG
(time zones, GZIP, time window), the Xtream Codes client against a local fake server, all five translations,
data migration from earlier versions, and static consistency checks of the WPF views (event handlers,
resources, translation keys). The tests run on every push.

```bash
dotnet test tests/AenvIPTV.Tests
```

## Microsoft Store
The Store package (MSIX) is built by `windows/build-msix.ps1` or by CI. Step-by-step submission guide,
listing texts in five languages and certification notes: [`windows/STORE.md`](windows/STORE.md).

## Legal
[End user license](LICENSE.txt) · [Privacy policy](docs/privacy-policy.md) ·
[Third-party notices](windows/Legal/THIRD-PARTY-NOTICES.txt)

## Code signing
Windows Smart App Control and SmartScreen trust installers that are signed with a recognised certificate.
This only concerns the direct-download installer — Store packages are signed by Microsoft. Options include
[Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) or a standard OV/EV code-signing
certificate; signing can then be added as a workflow step.

## License
© 2026 Vusal Aliyev. All rights reserved. See [LICENSE.txt](LICENSE.txt).
