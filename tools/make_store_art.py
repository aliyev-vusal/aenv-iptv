"""
Renders Microsoft Store artwork for AENV IPTV from the demo content (store-assets/):
  screenshots/01..05-*.png   1920x1080 screens of the app UI (colours and layout from App.xaml / views)
  hero-1920x1080.png         Store "hero art"
  poster-720x1080.png        9:16 poster art
  boxart-1080x1080.png       1:1 box art
  tile-300x300.png           app tile icon
Run tools/make_demo.py first (it creates the logos, posters and videos used here).
"""
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from demo_brand import CHANNELS, MOVIES, font, gradient  # noqa: E402
from make_demo import PROGRAMMES  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..")
DEMO = os.path.join(ROOT, "docs", "demo")
OUT = os.path.join(ROOT, "store-assets")
SHOTS = os.path.join(OUT, "screenshots")
ICON = os.path.join(ROOT, "windows", "Assets", "AenvIPTV.png")

# Palette from windows/App.xaml
BG, PANEL, CARD, HOVER = "#0F1115", "#171A21", "#20242D", "#2A2F3A"
ACCENT, TEXT, MUTED = "#3B82F6", "#F1F3F7", "#8B93A3"
W, H = 1920, 1080


def frame(video, t, size=(W, H)):
    path = f"/tmp/aenv_frame_{os.path.basename(os.path.dirname(video))}_{os.path.basename(video)}_{t}.png"
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-ss", str(t), "-i", video, "-frames:v", "1",
                    "-vf", f"scale={size[0]}:{size[1]}", path], check=True)
    return Image.open(path).convert("RGB")


def logo(cid, s):
    return Image.open(os.path.join(DEMO, "logos", f"{cid}.png")).convert("RGBA").resize((s, s), Image.LANCZOS)


def poster(mid, w, h):
    return Image.open(os.path.join(DEMO, "posters", f"{mid}.jpg")).convert("RGB").resize((w, h), Image.LANCZOS)


def rr(d, box, r, fill=None, outline=None, width=1):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=width)


def chip(d, x, y, text, active=False, f=None):
    f = f or font(18, "Medium")
    w = d.textlength(text, font=f) + 36
    rr(d, (x, y, x + w, y + 40), 20, fill=ACCENT if active else CARD)
    d.text((x + 18, y + 9), text, font=f, fill=TEXT)
    return x + w + 10


# ---------- small vector icons (stand-ins for Segoe Fluent glyphs) ----------
def icon(d, kind, cx, cy, s=10, color=TEXT, w=3):
    if kind == "star":
        import math
        pts = [(cx + (s if i % 2 == 0 else s * .45) * math.cos(-math.pi / 2 + i * math.pi / 5),
                cy + (s if i % 2 == 0 else s * .45) * math.sin(-math.pi / 2 + i * math.pi / 5)) for i in range(10)]
        d.polygon(pts, fill=color)
    elif kind == "clock":
        d.ellipse((cx - s, cy - s, cx + s, cy + s), outline=color, width=w)
        d.line((cx, cy, cx, cy - s * .6), fill=color, width=w)
        d.line((cx, cy, cx + s * .5, cy), fill=color, width=w)
    elif kind == "list":
        for k in (-1, 0, 1):
            d.line((cx - s, cy + k * s * .6, cx + s, cy + k * s * .6), fill=color, width=w)
    elif kind == "plus":
        d.line((cx - s, cy, cx + s, cy), fill=color, width=w)
        d.line((cx, cy - s, cx, cy + s), fill=color, width=w)
    elif kind == "info":
        d.ellipse((cx - s, cy - s, cx + s, cy + s), outline=color, width=w)
        d.line((cx, cy - 1, cx, cy + s * .55), fill=color, width=w)
        d.ellipse((cx - 2, cy - s * .55 - 2, cx + 2, cy - s * .55 + 2), fill=color)
    elif kind == "refresh":
        d.arc((cx - s, cy - s, cx + s, cy + s), 40, 330, fill=color, width=w)
        d.polygon([(cx + s * .9, cy - s * .9), (cx + s * 1.1, cy - s * .1), (cx + s * .25, cy - s * .35)], fill=color)
    elif kind == "search":
        d.ellipse((cx - s, cy - s, cx + s * .5, cy + s * .5), outline=color, width=w)
        d.line((cx + s * .3, cy + s * .3, cx + s, cy + s), fill=color, width=w)
    elif kind == "globe":
        d.ellipse((cx - s, cy - s, cx + s, cy + s), outline=color, width=w)
        d.ellipse((cx - s * .45, cy - s, cx + s * .45, cy + s), outline=color, width=max(1, w - 1))
        d.line((cx - s, cy, cx + s, cy), fill=color, width=max(1, w - 1))
    elif kind == "grid":
        q = s * .8
        for a in (-1, 1):
            for b in (-1, 1):
                d.rectangle((cx + a * q / 2 - q / 3, cy + b * q / 2 - q / 3, cx + a * q / 2 + q / 3, cy + b * q / 2 + q / 3), fill=color)
    elif kind == "back":
        d.line((cx - s, cy, cx + s, cy), fill=color, width=w)
        d.line((cx - s, cy, cx - s * .3, cy - s * .7), fill=color, width=w)
        d.line((cx - s, cy, cx - s * .3, cy + s * .7), fill=color, width=w)
    elif kind in ("up", "down"):
        k = -1 if kind == "up" else 1
        d.line((cx - s * .8, cy - k * s * .4, cx, cy + k * s * .4), fill=color, width=w)
        d.line((cx + s * .8, cy - k * s * .4, cx, cy + k * s * .4), fill=color, width=w)
    elif kind == "pause":
        d.rectangle((cx - s * .7, cy - s, cx - s * .2, cy + s), fill=color)
        d.rectangle((cx + s * .2, cy - s, cx + s * .7, cy + s), fill=color)
    elif kind == "volume":
        d.polygon([(cx - s, cy - s * .4), (cx - s * .4, cy - s * .4), (cx + s * .1, cy - s), (cx + s * .1, cy + s),
                   (cx - s * .4, cy + s * .4), (cx - s, cy + s * .4)], fill=color)
        d.arc((cx - s * .2, cy - s * .7, cx + s * .8, cy + s * .7), -50, 50, fill=color, width=w)
    elif kind == "full":
        for a, b in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            d.line((cx + a * s, cy + b * s, cx + a * s * .4, cy + b * s), fill=color, width=w)
            d.line((cx + a * s, cy + b * s, cx + a * s, cy + b * s * .4), fill=color, width=w)


# ---------- main window ----------
def sidebar(img, selected="demo"):
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 300, H), fill=PANEL)
    d.text((24, 26), "AENV IPTV", font=font(30), fill=TEXT)
    items = [("fav", "star", "Favorites", "3"), ("recent", "clock", "Recently watched", ""), ("demo", "list", "AENV Demo", "9")]
    y = 96
    for key, ic, label, badge in items:
        if key == selected:
            rr(d, (12, y, 288, y + 48), 10, fill=ACCENT)
        icon(d, ic, 40, y + 24, 9, TEXT if key != "fav" or key == selected else "#FACC15", 3)
        d.text((64, y + 13), label, font=font(19, "Medium"), fill=TEXT)
        if badge:
            d.text((268 - d.textlength(badge, font=font(15, "Medium")), y + 16), badge, font=font(15, "Medium"), fill=MUTED)
        y += 54
    # bottom area
    by = H - 214
    icon(d, "globe", 32, by + 22, 9, MUTED, 2)
    rr(d, (52, by + 2, 288, by + 42), 6, fill="#F3F4F6")
    d.text((64, by + 12), "English", font=font(17, "Medium"), fill="#111827")
    d.polygon([(268, by + 18), (278, by + 18), (273, by + 25)], fill="#111827")
    buttons = [("plus", "Add playlist", ACCENT), ("info", "About", CARD), ("refresh", "Refresh all  (F5)", CARD)]
    by += 56
    for ic, label, col in buttons:
        rr(d, (12, by, 288, by + 44), 8, fill=col)
        tw = d.textlength(label, font=font(17, "Medium"))
        x0 = 150 - (tw + 28) / 2
        icon(d, ic, x0 + 8, by + 22, 8, TEXT, 3)
        d.text((x0 + 28, by + 11), label, font=font(17, "Medium"), fill=TEXT)
        by += 50


def header(img, title, kinds, active, groups, active_group):
    d = ImageDraw.Draw(img)
    x0 = 330
    d.text((x0, 26), title, font=font(30, "SemiBold"), fill=TEXT)
    x = x0 + d.textlength(title, font=font(30, "SemiBold")) + 30
    for k in kinds:
        x = chip(d, x, 28, k, k == active)
    # search + view toggle
    rr(d, (W - 420, 26, W - 92, 72), 6, fill=CARD, outline="#2E3440")
    icon(d, "search", W - 396, 48, 8, MUTED, 2)
    d.text((W - 374, 37), "Search…", font=font(18), fill=MUTED)
    rr(d, (W - 78, 26, W - 32, 72), 8, fill=CARD)
    icon(d, "list", W - 55, 49, 10, TEXT, 3)
    x = x0
    for g in groups:
        x = chip(d, x, 96, g, g == active_group, font(17, "Medium"))


def channel_tile(img, x, y, ch, prog, progress, fav=False, selected=False):
    d = ImageDraw.Draw(img)
    w, h = 250, 228
    rr(d, (x, y, x + w, y + h), 14, fill=CARD, outline=ACCENT if selected else None, width=3 if selected else 1)
    rr(d, (x + 12, y + 12, x + w - 12, y + 142), 8, fill="#11141A")
    lg = logo(ch[0], 112)
    img.paste(lg, (x + (w - 112) // 2, y + 21), lg)
    if fav:
        icon(d, "star", x + w - 30, y + 30, 10, "#FACC15")
    d.text((x + 14, y + 154), ch[1], font=font(19, "SemiBold"), fill=TEXT)
    d.text((x + 14, y + 182), prog, font=font(15), fill=MUTED)
    rr(d, (x + 14, y + 210, x + w - 14, y + 214), 2, fill="#2E3440")
    rr(d, (x + 14, y + 210, x + 14 + (w - 28) * progress, y + 214), 2, fill=ACCENT)


def shot_library():
    img = Image.new("RGB", (W, H), BG)
    sidebar(img)
    header(img, "AENV Demo", ["Live TV", "Movies"], "Live TV",
           ["All", "Nature", "Kids", "Sport", "Science", "Music", "Discovery"], "All")
    progress = [.62, .35, .81, .18, .47, .7]
    favs = {"lumora", "orbita", "melodi"}
    for i, ch in enumerate(CHANNELS):
        col, row = i % 5, i // 5
        channel_tile(img, 330 + col * 272, 166 + row * 250, ch, PROGRAMMES[ch[0]][i % 6], progress[i],
                     fav=ch[0] in favs, selected=(i == 3))
    return img


def shot_movies():
    img = Image.new("RGB", (W, H), BG)
    sidebar(img)
    header(img, "AENV Demo", ["Live TV", "Movies"], "Movies", ["All", "Movies · Sci-Fi", "Movies · Drama", "Movies · Family"], "All")
    d = ImageDraw.Draw(img)
    for i, m in enumerate(MOVIES):
        x, y = 330 + i * 230, 166
        rr(d, (x, y, x + 210, y + 392), 14, fill=CARD)
        p = poster(m[0], 186, 279)
        mask = Image.new("L", p.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, p.size[0] - 1, p.size[1] - 1), 8, fill=255)
        img.paste(p, (x + 12, y + 12), mask)
        d.text((x + 14, y + 304), m[1], font=font(18, "SemiBold"), fill=TEXT)
        d.text((x + 14, y + 330), m[2], font=font(15), fill=MUTED)
    return img


def player_overlay(img, ch, prog, nxt, show_list=False):
    d = ImageDraw.Draw(img, "RGBA")
    # top gradient
    for yy in range(230):
        a = int(230 * (1 - yy / 230))
        d.line((0, yy, W, yy), fill=(0, 0, 0, a))
    d.text((W - 110, 46), "21:10", font=font(28, "SemiBold"), fill=TEXT)
    if show_list:
        draw_channel_list(img, d, ch)
        draw_controls(d)
        return
    icon(d, "back", 60, 82, 13, TEXT, 4)
    rr(d, (100, 50, 230, 130), 10, fill=(255, 255, 255, 34))
    lg = logo(ch[0], 70)
    img.paste(lg, (130, 55), lg)
    d.text((252, 46), ch[1], font=font(32), fill=TEXT)
    d.text((252, 90), f"Now  20:00–22:00   {prog}", font=font(19, "Medium"), fill=TEXT)
    rr(d, (252, 122, 680, 127), 2, fill=(255, 255, 255, 68))
    rr(d, (252, 122, 252 + 428 * .58, 127), 2, fill=ACCENT)
    d.text((252, 138), f"Next  22:00   {nxt}", font=font(16), fill=(255, 255, 255, 180))
    draw_controls(d)


def draw_controls(d):
    bw = 760
    bx, by = (W - bw) // 2, H - 128
    rr(d, (bx, by, bx + bw, by + 84), 20, fill=(26, 29, 36, 217))
    cx = bx + 70
    for ic in ("up", "pause", "down", "list", "star"):
        icon(d, ic, cx, by + 42, 12, "#FACC15" if ic == "star" else TEXT, 3)
        cx += 64
    d.line((cx - 10, by + 22, cx - 10, by + 62), fill=(255, 255, 255, 50), width=1)
    icon(d, "volume", cx + 30, by + 42, 12, TEXT, 3)
    rr(d, (cx + 64, by + 40, cx + 200, by + 45), 2, fill=(255, 255, 255, 60))
    rr(d, (cx + 64, by + 40, cx + 64 + 109, by + 45), 2, fill=ACCENT)
    d.ellipse((cx + 166, by + 35, cx + 180, by + 49), fill=TEXT)
    d.line((cx + 220, by + 22, cx + 220, by + 62), fill=(255, 255, 255, 50), width=1)
    icon(d, "full", cx + 262, by + 42, 11, TEXT, 3)


def draw_channel_list(img, d, ch):
    d.rectangle((0, 0, 520, H), fill=(20, 23, 29, 240))
    d.text((26, 30), "Channels", font=font(26, "SemiBold"), fill=TEXT)
    y = 92
    for i, c in enumerate(CHANNELS):
        if c[0] == ch[0]:
            rr(d, (12, y - 6, 508, y + 66), 8, fill=(59, 130, 246, 230))
        lg = logo(c[0], 54)
        img.paste(lg, (26, y), lg)
        d.text((98, y + 4), c[1], font=font(19, "Medium"), fill=TEXT)
        d.text((98, y + 32), PROGRAMMES[c[0]][(i + 2) % 6], font=font(15), fill=(200, 205, 215, 255))
        y += 84


def shot_player(list_open=False):
    ch = CHANNELS[0] if not list_open else CHANNELS[4]
    img = frame(os.path.join(DEMO, "live", ch[0], "index.m3u8"), 11)
    # hide the stream's own lower third: the player overlay sits on top in the app as well
    player_overlay(img, ch, PROGRAMMES[ch[0]][1], PROGRAMMES[ch[0]][2], show_list=list_open)
    return img


def shot_add_playlist():
    img = shot_library().filter(ImageFilter.GaussianBlur(3))
    d = ImageDraw.Draw(img, "RGBA")
    d.rectangle((0, 0, W, H), fill=(0, 0, 0, 120))
    x0, y0, w, h = (W - 560) // 2, 210, 560, 600
    rr(d, (x0, y0, x0 + w, y0 + h), 10, fill=PANEL, outline="#2E3440")
    d.text((x0 + 28, y0 + 26), "New playlist", font=font(26, "SemiBold"), fill=TEXT)
    x = chip(d, x0 + 28, y0 + 80, "M3U / XSPF URL", False)
    chip(d, x, y0 + 80, "Xtream Codes", True)
    fields = [("Name", "Living room"), ("Server (e.g. http://server.com:8080)", "http://192.168.1.20:8080"),
              ("Username", "family"), ("Password", "••••••••••")]
    y = y0 + 148
    for label, value in fields:
        d.text((x0 + 28, y), label, font=font(16), fill=MUTED)
        rr(d, (x0 + 28, y + 26, x0 + w - 28, y + 70), 4, fill=CARD, outline="#2E3440")
        d.text((x0 + 40, y + 37), value, font=font(18), fill=TEXT)
        y += 92
    rr(d, (x0 + w - 268, y0 + h - 70, x0 + w - 160, y0 + h - 26), 8, fill=CARD)
    d.text((x0 + w - 246, y0 + h - 59), "Cancel", font=font(17, "Medium"), fill=TEXT)
    rr(d, (x0 + w - 148, y0 + h - 70, x0 + w - 28, y0 + h - 26), 8, fill=ACCENT)
    d.text((x0 + w - 113, y0 + h - 59), "Add", font=font(17, "Medium"), fill=TEXT)
    return img


# ---------- marketing art ----------
def hero():
    img = gradient(W // 8, H // 8, "#1D4ED8", "#0F172A").resize((W, H), Image.BICUBIC)
    d = ImageDraw.Draw(img)
    # tilted screenshot
    s = shot_library().resize((1100, 619), Image.LANCZOS)
    mask = Image.new("L", s.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, s.size[0] - 1, s.size[1] - 1), 18, fill=255)
    shadow = Image.new("RGBA", (s.size[0] + 80, s.size[1] + 80), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((40, 40, s.size[0] + 40, s.size[1] + 40), 18, fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(24))
    img.paste(shadow, (W - 1100 - 120, (H - 619) // 2 - 10), shadow)
    img.paste(s, (W - 1100 - 80, (H - 619) // 2 + 20), mask)
    ic = Image.open(ICON).convert("RGBA").resize((150, 150), Image.LANCZOS)
    img.paste(ic, (110, 300), ic)
    d.text((110, 480), "AENV IPTV", font=font(86), fill="white")
    d.text((114, 590), "Your playlists. One clean player.", font=font(32, "Medium"), fill=(219, 234, 254))
    d.text((114, 642), "M3U · XSPF · Xtream Codes · EPG", font=font(24), fill=(191, 219, 254))
    return img


def poster_art(w, h):
    img = gradient(w // 6, h // 6, "#1D4ED8", "#0F172A").resize((w, h), Image.BICUBIC)
    d = ImageDraw.Draw(img)
    square = w == h
    s = int(w * (.30 if square else .42))
    icon_y = int(h * (.12 if square else .2))
    ic = Image.open(ICON).convert("RGBA").resize((s, s), Image.LANCZOS)
    img.paste(ic, ((w - s) // 2, icon_y), ic)
    f = font(int(w * (.11 if square else .12)))
    t = "AENV IPTV"
    title_y = icon_y + s + int(h * .04)
    d.text(((w - d.textlength(t, font=f)) / 2, title_y), t, font=f, fill="white")
    # strip of demo channel logos, always below the title
    k = int(w * .12)
    gap = int(w * .025)
    x = (w - (6 * k + 5 * gap)) // 2
    y = max(int(title_y + f.size * 1.6), int(h * .78))
    y = min(y, h - k - int(h * .05))
    for ch in CHANNELS:
        lg = logo(ch[0], k)
        img.paste(lg, (x, y), lg)
        x += k + gap
    return img


def main():
    os.makedirs(SHOTS, exist_ok=True)
    shots = [
        ("01-library.png", shot_library()),
        ("02-player-epg.png", shot_player()),
        ("03-channel-list.png", shot_player(list_open=True)),
        ("04-movies.png", shot_movies()),
        ("05-add-playlist.png", shot_add_playlist()),
    ]
    for name, im in shots:
        im.save(os.path.join(SHOTS, name), optimize=True)
    hero().save(os.path.join(OUT, "hero-1920x1080.png"), optimize=True)
    poster_art(720, 1080).save(os.path.join(OUT, "poster-720x1080.png"), optimize=True)
    poster_art(1080, 1080).save(os.path.join(OUT, "boxart-1080x1080.png"), optimize=True)
    Image.open(ICON).convert("RGBA").resize((300, 300), Image.LANCZOS).save(os.path.join(OUT, "tile-300x300.png"))
    print("done")


if __name__ == "__main__":
    main()
