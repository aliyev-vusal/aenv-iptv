"""
Generates the complete AENV IPTV demo set in docs/demo:
  logos/*.png, posters/*.jpg        original artwork (demo_brand.py)
  live/<id>/index.m3u8 + *.ts       HLS "live" channels made of procedural animations
  vod/<id>.mp4                      short demo "movies"
  aenv-demo.m3u                     playlist with groups, logos, EPG link and movies
  aenv-demo-epg.xml.gz              XMLTV guide (2-hour slots through 2027)

Everything is synthetic: mathematical patterns, generated tones and invented names.
Usage:  python3 tools/make_demo.py [--base https://raw.githubusercontent.com/<user>/<repo>/main/docs/demo]
"""
import argparse
import gzip
import os
import shutil
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from xml.sax.saxutils import escape

sys.path.insert(0, os.path.dirname(__file__))
from demo_brand import CHANNELS, MOVIES, channel_logo, movie_poster  # noqa: E402

OUT = os.path.join(os.path.dirname(__file__), "..", "docs", "demo")
W, H, FPS, SECONDS = 960, 540, 25, 24
FONT = "/usr/share/fonts/opentype/inter/Inter-SemiBold.otf"

# Procedural video source per channel (ffmpeg lavfi) — cheap to render and small to store
SOURCES = {
    "lumora": f"gradients=s={W}x{H}:r={FPS}:n=4:c0=0x064e3b:c1=0x10b981:c2=0x6ee7b7:c3=0x0f766e:speed=0.015",
    "kodo":   f"gradients=s={W}x{H}:r={FPS}:n=5:c0=0xf59e0b:c1=0xec4899:c2=0x8b5cf6:c3=0x22d3ee:c4=0xfde047:speed=0.03:type=spiral",
    "vexo":   f"gradients=s={W}x{H}:r={FPS}:n=3:c0=0x7f1d1d:c1=0xdc2626:c2=0xf97316:speed=0.06,"
              f"drawbox=x='mod(t*520\\,{W}+120)-120':y=0:w=60:h={H}:color=white@0.12:t=fill,"
              f"drawbox=x='mod(t*300+400\\,{W}+120)-120':y=0:w=24:h={H}:color=white@0.10:t=fill",
    "orbita": f"gradients=s={W}x{H}:r={FPS}:n=4:c0=0x1e1b4b:c1=0x4338ca:c2=0x22d3ee:c3=0x0f172a:speed=0.03:type=circular,"
              f"drawbox=x='{W}/2+300*cos(t*0.8)-14':y='{H}/2+110*sin(t*0.8)-14':w=28:h=28:color=0xe0e7ff@0.9:t=fill,"
              f"drawbox=x='{W}/2-46':y='{H}/2-46':w=92:h=92:color=0x22d3ee@0.55:t=fill",
    "melodi": f"gradients=s={W}x{H}:r={FPS}:n=4:c0=0x4c1d95:c1=0x7c3aed:c2=0xf472b6:c3=0x1e1b4b:speed=0.04:type=radial",
    "pixeo":  f"life=s=240x135:r=12:ratio=0.12:mold=8:life_color=#a3e635:death_color=#0c4a6e:mold_color=#0369a1,"
              f"scale={W}:{H}:flags=neighbor,fps={FPS}",
}

# Gentle generated audio: a soft chord for music, quiet ambient tones for others
AUDIO = {
    "melodi": "aevalsrc='0.08*sin(2*PI*220*t)+0.06*sin(2*PI*277.18*t)+0.05*sin(2*PI*329.63*t)*(0.5+0.5*sin(2*PI*0.5*t))':s=44100",
}
DEFAULT_AUDIO = "aevalsrc='0.02*sin(2*PI*174*t)*(0.6+0.4*sin(2*PI*0.2*t))':s=44100"

PROGRAMMES = {
    "lumora": ["Forest Light", "Rivers at Dawn", "Quiet Meadows", "Tidal Patterns", "Green Horizons", "Night Garden"],
    "kodo":   ["Shape Parade", "Colour Lab", "Counting Clouds", "Story Spiral", "Sky Painters", "Little Builders"],
    "vexo":   ["Sprint Studio", "Track Highlights", "Motion Replay", "Training Day", "Finish Line", "Speed Talk"],
    "orbita": ["Fractal Journeys", "Edge of Infinity", "How Patterns Grow", "Deep Zoom", "Maths of Nature", "Orbit Hour"],
    "melodi": ["Ambient Morning", "Soft Chords", "Colour & Sound", "Evening Waves", "Late Night Loops", "Studio Calm"],
    "pixeo":  ["Life Simulations", "Cells & Rules", "Emergent Worlds", "Pixel Stories", "Pattern Lab", "Small Universes"],
}


def run(args):
    r = subprocess.run(args, capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(r.stderr[-2000:])


def overlay_chain(name, logo, programme_hint):
    """Small, translucent logo bug bottom-right, clear of the player's own overlays."""
    hint = programme_hint.replace(":", r"\:")
    return (
        f"[1:v]scale=64:64,format=rgba,colorchannelmixer=aa=0.75[logo];"
        f"[0:v][logo]overlay=W-w-32:H-h-28:format=auto,"
        f"format=yuv420p[v]"
    )


def make_live(ch):
    cid, name = ch[0], ch[1]
    d = os.path.join(OUT, "live", cid)
    shutil.rmtree(d, ignore_errors=True)
    os.makedirs(d)
    run([
        "ffmpeg", "-y", "-loglevel", "error",
        "-f", "lavfi", "-t", str(SECONDS), "-i", SOURCES[cid],
        "-i", os.path.join(OUT, "logos", f"{cid}.png"),
        "-f", "lavfi", "-t", str(SECONDS), "-i", AUDIO.get(cid, DEFAULT_AUDIO),
        "-filter_complex", overlay_chain(name, None, "AENV demo channel · generated content"),
        "-map", "[v]", "-map", "2:a",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "30", "-g", str(FPS * 4), "-keyint_min", str(FPS * 4),
        "-sc_threshold", "0", "-c:a", "aac", "-b:a", "64k", "-ac", "2",
        "-f", "hls", "-hls_time", "4", "-hls_playlist_type", "vod",
        "-hls_segment_filename", os.path.join(d, "seg%02d.ts"), os.path.join(d, "index.m3u8"),
    ])


def make_vod(m):
    mid, title = m[0], m[1]
    os.makedirs(os.path.join(OUT, "vod"), exist_ok=True)
    src = {
        "quiet-orbit":    f"gradients=s={W}x{H}:r={FPS}:n=3:c0=0x1e1b4b:c1=0x6366f1:c2=0x0f172a:speed=0.02:type=radial",
        "paper-lanterns": f"gradients=s={W}x{H}:r={FPS}:n=3:c0=0x7c2d12:c1=0xf59e0b:c2=0x451a03:speed=0.02:type=radial",
        "tide-pool":      f"gradients=s={W}x{H}:r={FPS}:n=4:c0=0x164e63:c1=0x14b8a6:c2=0x5eead4:c3=0x0e7490:speed=0.02",
    }[mid]
    safe = title.replace(":", r"\:")
    run([
        "ffmpeg", "-y", "-loglevel", "error",
        "-f", "lavfi", "-t", str(SECONDS), "-i", src,
        "-f", "lavfi", "-t", str(SECONDS), "-i", DEFAULT_AUDIO,
        "-vf", f"drawtext=fontfile={FONT}:text='{safe}':x=(w-tw)/2:y=(h-th)/2:fontsize=56:fontcolor=white:"
               f"alpha='if(lt(t,1),t,if(lt(t,4),1,if(lt(t,5),5-t,0)))',format=yuv420p",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "30", "-c:a", "aac", "-b:a", "64k",
        "-movflags", "+faststart", os.path.join(OUT, "vod", f"{mid}.mp4"),
    ])


def make_epg(path):
    start = datetime(2026, 11, 1, tzinfo=timezone.utc)
    end = datetime(2028, 1, 1, tzinfo=timezone.utc)
    fmt = "%Y%m%d%H%M%S +0000"
    with gzip.open(path, "wt", encoding="utf-8", compresslevel=9) as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n<tv generator-info-name="AENV IPTV demo">\n')
        for cid, name, *_ in CHANNELS:
            f.write(f'  <channel id="{cid}.aenv"><display-name>{escape(name)}</display-name></channel>\n')
        for cid, name, *_ in CHANNELS:
            titles = PROGRAMMES[cid]
            t, i = start, 0
            while t < end:
                s, e = t, t + timedelta(hours=2)
                title = titles[i % len(titles)]
                f.write(f'  <programme start="{s.strftime(fmt)}" stop="{e.strftime(fmt)}" channel="{cid}.aenv">'
                        f'<title lang="en">{escape(title)}</title>'
                        f'<desc lang="en">{escape(title)} on {escape(name)} — generated demo programme.</desc></programme>\n')
                t, i = e, i + 1
        f.write("</tv>\n")


def make_playlist(base):
    lines = [f'#EXTM3U url-tvg="{base}/aenv-demo-epg.xml.gz"']
    for cid, name, group, *_ in CHANNELS:
        lines.append(f'#EXTINF:-1 tvg-id="{cid}.aenv" tvg-name="{name}" tvg-logo="{base}/logos/{cid}.png" '
                     f'group-title="{group}",{name}')
        lines.append(f"{base}/live/{cid}/index.m3u8")
    for mid, title, genre, _ in MOVIES:
        lines.append(f'#EXTINF:-1 tvg-logo="{base}/posters/{mid}.jpg" group-title="Movies · {genre}",{title}')
        lines.append(f"{base}/vod/{mid}.mp4")
    with open(os.path.join(OUT, "aenv-demo.m3u"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="https://raw.githubusercontent.com/aliyev-vusal/aenv-iptv/main/docs/demo")
    ap.add_argument("--skip-video", action="store_true")
    a = ap.parse_args()
    for sub in ("logos", "posters"):
        os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    for ch in CHANNELS:
        channel_logo(ch).save(os.path.join(OUT, "logos", f"{ch[0]}.png"), optimize=True)
    for m in MOVIES:
        movie_poster(m).save(os.path.join(OUT, "posters", f"{m[0]}.jpg"), quality=88)
    if not a.skip_video:
        for ch in CHANNELS:
            print("live", ch[0]); make_live(ch)
        for m in MOVIES:
            print("vod", m[0]); make_vod(m)
    make_epg(os.path.join(OUT, "aenv-demo-epg.xml.gz"))
    make_playlist(a.base.rstrip("/"))
    print("done")


if __name__ == "__main__":
    main()
