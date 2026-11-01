# AENV IPTV demo content

Six fictional channels and three short films for testing AENV IPTV, taking Store screenshots and
Microsoft Store certification.

| Channel | Group | Picture |
|---|---|---|
| Lumora Nature | Nature | flowing green gradients |
| Kodo Kids | Kids | colour spiral |
| Vexo Sport | Sport | moving light bars |
| Orbita Science | Science | orbit animation |
| Melodi Music | Music | radial colour pulse + soft chord |
| Pixeo Life | Discovery | Conway's Game of Life |

Movies: *The Quiet Orbit*, *Paper Lanterns*, *Tide Pool*.

**Playlist:** `aenv-demo.m3u` (with EPG `aenv-demo-epg.xml.gz`, programmes through 2027).
Add it in AENV IPTV with *Add playlist → M3U / XSPF URL* using its raw GitHub URL. The repository (or a copy
of this folder) must be publicly reachable for that.

## Origin and rights
Everything here was generated from scratch by [`tools/make_demo.py`](../../tools/make_demo.py) and
[`tools/demo_brand.py`](../../tools/demo_brand.py): mathematical video patterns (FFmpeg test sources),
synthesised tones, geometric logos and invented names. No third-party footage, music, logos or trademarks
are used. Text is set in Inter (SIL Open Font License). The material may be used freely to demonstrate
AENV IPTV, including in store listings.

Regenerate (e.g. for another host): `python3 tools/make_demo.py --base https://<host>/docs/demo`
