# Publishing AENV IPTV to the Microsoft Store

The Store version is an **MSIX package**. Microsoft signs it during certification, so no code-signing
certificate is needed and Store installs are never blocked by SmartScreen or Smart App Control.

## 1. Developer account (once)
1. Go to <https://storedeveloper.microsoft.com> and sign in with your Microsoft account.
2. Choose **Individual** (registration is free for individual developers; identity is verified with an ID
   document and a selfie). Choose the country carefully — it cannot be changed later.

## 2. Reserve the name
Partner Center → **Apps and games → New product → MSIX or PWA app** → reserve **AENV IPTV**.

## 3. Copy the product identity
Partner Center → your app → **Product management → Product identity**. Note three values:

| Partner Center | build-msix.ps1 parameter | GitHub variable |
|---|---|---|
| Package/Identity/Name | `-IdentityName` | `STORE_IDENTITY_NAME` |
| Package/Identity/Publisher | `-Publisher` | `STORE_PUBLISHER` |
| Package/Properties/PublisherDisplayName | `-PublisherDisplayName` | `STORE_PUBLISHER_DISPLAY_NAME` |

Save them as repository variables (*Settings → Secrets and variables → Actions → Variables*).

## 4. Build the package
- **GitHub:** *Actions → Build installer → Run workflow* → download the `AENV-IPTV-Store-x.y.z` artifact.
- **Locally:** `.\build-msix.ps1 -Version 1.0.0 -IdentityName "…" -Publisher "CN=…" -PublisherDisplayName "…"`

Version rule: every submission needs a higher version (`1.0.1`, `1.1.0`, …).

## 5. Test before submitting (recommended)
1. Windows Settings → *System → For developers* → turn on **Developer Mode**.
2. `.\build-msix.ps1 -Register` installs the app locally from the package layout. Start it from the
   Start menu, add a playlist, play, switch channels, close and reopen.
3. Run the **Windows App Certification Kit** (part of the Windows SDK, "Windows App Cert Kit" in the Start
   menu) → *Validate Store App* → select AENV IPTV. Fix anything marked *Failed* before uploading.

## 6. Submission
| Section | What to enter |
|---|---|
| Pricing and availability | Free · all markets (or as desired) |
| Properties | Category **Entertainment** (or *Multimedia design*); privacy policy URL: `https://github.com/<user>/<repo>/blob/main/docs/privacy-policy.md` |
| Age ratings | Complete the IARC questionnaire honestly. The app can play unrestricted internet content chosen by the user — answer *yes* to that question. |
| Packages | Upload the `.msix` |
| Store listings | Use the texts below and the artwork in `store-assets/` (see section 8) |
| Submission options → Notes for certification | See below |

**Notes for certification** (paste as is):
> AENV IPTV is a media player only and does not include or link to any content. To test, choose
> "Add playlist → M3U / XSPF URL" and enter:
> https://raw.githubusercontent.com/aliyev-vusal/aenv-iptv/main/docs/demo/aenv-demo.m3u
> It contains six fictional demo channels with a programme guide and three short demo films, all generated
> by the developer. Double-click a channel to play it; arrow keys switch channels; Esc returns to the list.

**Screenshots:** use only the AENV demo content. Screenshots showing real TV channels, logos or copyrighted
programmes are a common reason for rejection of IPTV players. The demo playlist must be publicly reachable
(public repository, or host `docs/demo` elsewhere and regenerate it with `tools/make_demo.py --base …`).

## 7. Store listing texts

**English**
> AENV IPTV is a fast, clean player for your own IPTV playlists. Add M3U, M3U8 or XSPF links, local files
> or an Xtream Codes account and watch live TV, movies and series with an electronic programme guide.
> • M3U, XSPF and Xtream Codes support • XMLTV programme guide (EPG) • Favorites and recently watched
> • Switch channels while watching with the arrow keys • Plays HLS, MPEG-TS, RTMP, RTSP and more
> • Available in English, Azərbaycanca, Türkçe, Deutsch and Español
> AENV IPTV does not provide any channels or content. You need your own playlist from a provider you are
> authorised to use.

**Azərbaycanca**
> AENV IPTV öz IPTV pleylistləriniz üçün sürətli və sadə pleyerdir. M3U, M3U8, XSPF linkləri, lokal fayllar
> və ya Xtream Codes hesabı əlavə edin və canlı TV, film və serialları proqram bələdçisi ilə izləyin.
> AENV IPTV heç bir kanal və ya məzmun təqdim etmir; istifadə hüququnuz olan öz pleylistiniz lazımdır.

**Türkçe**
> AENV IPTV, kendi IPTV listeleriniz için hızlı ve sade bir oynatıcıdır. M3U, M3U8, XSPF bağlantıları, yerel
> dosyalar veya bir Xtream Codes hesabı ekleyin; canlı TV, film ve dizileri yayın akışıyla izleyin.
> AENV IPTV hiçbir kanal veya içerik sağlamaz; kullanma hakkınız olan kendi listenize ihtiyacınız vardır.

**Deutsch**
> AENV IPTV ist ein schneller, übersichtlicher Player für Ihre eigenen IPTV-Playlists. Fügen Sie M3U-, M3U8-
> oder XSPF-Links, lokale Dateien oder ein Xtream-Codes-Konto hinzu und sehen Sie Live-TV, Filme und Serien
> mit elektronischem Programmführer. AENV IPTV stellt keine Sender oder Inhalte bereit; Sie benötigen eine
> eigene Playlist eines Anbieters, zu dessen Nutzung Sie berechtigt sind.

**Español**
> AENV IPTV es un reproductor rápido y sencillo para tus propias listas IPTV. Añade enlaces M3U, M3U8 o
> XSPF, archivos locales o una cuenta Xtream Codes y mira TV en directo, películas y series con guía de
> programación. AENV IPTV no proporciona canales ni contenido; necesitas tu propia lista de un proveedor
> que estés autorizado a usar.

## 8. Store artwork (`store-assets/`)
All images are original and show only the AENV demo content.

| File | Partner Center field |
|---|---|
| `screenshots/01-library.png` … `05-add-playlist.png` (1920×1080) | Screenshots → Desktop |
| `hero-1920x1080.png` | Store display images → 16:9 Super hero art |
| `poster-720x1080.png` | Store display images → 9:16 Poster art |
| `boxart-1080x1080.png` | Store display images → 1:1 Box art |
| `tile-300x300.png` | Store display images → 1:1 App tile icon |

Suggested screenshot captions: *Your channels at a glance* · *Programme guide while you watch* ·
*Switch channels without leaving the player* · *Movies and series* · *M3U, XSPF and Xtream Codes*.

The screenshots are rendered from the app's layout by `tools/make_store_art.py`. After installing the app,
you can also capture real screenshots with the demo playlist (Win + Shift + S, window at 1920×1080).

## Direct download vs. Store
| | Store (MSIX) | Direct download (`AENV-IPTV-Setup.exe`) |
|---|---|---|
| Signing | by Microsoft, free | needs your own code-signing certificate |
| Smart App Control / SmartScreen | never blocked | blocked / warned while unsigned |
| Updates | automatic through the Store | users install new versions manually |
| Data folder | per-package (Windows virtualises `%AppData%`) | `%AppData%\AenvIPTV` |
