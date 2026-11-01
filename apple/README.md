# AENV IPTV for Apple platforms

One SwiftUI codebase for **iPhone, iPad, Apple TV and Mac** (iOS 17 / tvOS 17 / macOS 14).

## Features
| | |
|---|---|
| Playlists | M3U, XSPF, Xtream Codes API |
| EPG | XMLTV `.xml` / `.xml.gz` (built-in GZIP decoder) |
| Library | Live TV / Movies / Series, groups, search, tile ↔ list |
| Sync | Playlists, favorites and history via iCloud key-value store |
| Player | Channel switching while watching, channel list panel, EPG overlay |
| Languages | English, Azərbaycanca, Türkçe, Deutsch, Español (`Localizable.xcstrings`) |

**Apple TV remote:** up/down switches channels, left/right opens the channel list, Menu exits.
**Mac / iPad keyboard:** ↑/↓ channel, Space pause, Esc close.

## Build
```bash
brew install xcodegen
cd apple
xcodegen generate
open AenvIPTV.xcodeproj
```
1. **Target → Signing & Capabilities:** select your team.
2. Make the bundle ID unique if needed (`com.vusal.aenviptv`) and update both `.entitlements` files.
3. **+ Capability → iCloud → Key-value storage** (requires a paid Apple Developer account).
4. Choose a device (iPhone, iPad, Apple TV, *My Mac*) and run.

## Playback formats
AVPlayer plays **HLS (.m3u8), MP4 and MOV**. `.ts` URLs are retried automatically as `.m3u8`, which works with
most Xtream servers. RTMP, RTSP, UDP, MKV or AVI would need **VLCKit** (MobileVLCKit / TVVLCKit / VLCKit);
`PlayerController` is the single place to swap the engine.

## Structure
```
Sources/
  App/AenvIPTVApp.swift          entry point, audio session
  Models/Models.swift            Playlist, Channel, Episode, Programme
  Services/M3UParser.swift       M3U / EXTINF + XSPF
  Services/XtreamClient.swift    player_api.php (live, VOD, series, episodes)
  Services/EPGService.swift      XMLTV parser + GZIP
  Services/LibraryStore.swift    persistence, favorites, iCloud sync
  Views/                         SwiftUI screens and player
  Localizable.xcstrings          translations
```
