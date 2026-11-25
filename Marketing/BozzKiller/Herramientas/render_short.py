"""Render a Bozz Killer short from a JSON spec.
spec = {
  "src": path, "voice": mp3, "body": seconds, "intro": mp4, "cats": mp4, "out": mp4,
  "clips": [ {"t": start, "d": dur, "mode": "crop"|"fit"|"box"|"still", "x":.., "y":.., "w":.., "h":.., "zoom":..}, ...],
  "caps":  [ ["LINE1|LINE2", "white"|"yellow"|"white,yellow", t0, t1], ... ]   (voice-relative times)
}
"""
import json, subprocess, sys, os

FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
FP = r"C:\Users\delfa\Downloads\ffprobe.exe"
LEAD = 0.1
ENC = ["-c:v", "libx264", "-crf", "14", "-preset", "fast", "-pix_fmt", "yuv420p"]


def run(args):
    subprocess.run([FF, "-v", "error"] + args + ["-y"], check=True)


def clip_filter(c):
    m = c.get("mode", "crop")
    if m == "crop":
        return f"crop=608:1080:{c.get('x', 656)}:0,scale=1080:1920:flags=lanczos"
    if m == "box":  # arbitrary rect, any aspect -> cover 9:16
        w, h = c["w"], c["h"]
        return f"crop={w}:{h}:{c['x']}:{c['y']},scale=1080:1920:force_original_aspect_ratio=increase:flags=lanczos,crop=1080:1920"
    if m == "fit":  # inner 16:9 strip without corner marks, over blurred bg
        x = c.get("x", 360); w = c.get("w", 1200)
        off = c.get("off", -250)
        return (f"crop={w}:{c.get('h', 1080)}:{x}:{c.get('y', 0)},split[p][q];[p]scale=-2:1920,crop=1080:1920,boxblur=30:3,eq=brightness=-0.15[bg];"
                f"[q]scale=1080:-2[fg];[bg][fg]overlay=0:(H-h)/2{off:+d}")
    raise ValueError(m)


def render(spec_path):
    s = json.load(open(spec_path, encoding="utf-8"))
    wd = os.path.splitext(spec_path)[0] + "_work"
    os.makedirs(wd, exist_ok=True)
    parts = []
    total = 0
    for i, c in enumerate(s["clips"]):
        p = os.path.join(wd, f"p{i}.mp4")
        if c.get("mode") == "still":
            png = os.path.join(wd, f"s{i}.png")
            run(["-ss", str(c["t"]), "-i", c.get("src", s["src"]), "-frames:v", "1", png])
            w, h = c.get("w", 608), c.get("h", 1080)
            frames = int(round(c["d"] * 60))
            z = c.get("zoom", 0.12)
            vf = (f"crop={w}:{h}:{c.get('x', 656)}:{c.get('y', 0)},scale=2160:3840,"
                  f"zoompan=z='1+{z}*on/{frames}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1080x1920:fps=60,setsar=1")
            run(["-loop", "1", "-framerate", "60", "-t", str(c["d"]), "-i", png, "-vf", vf, "-an", "-t", str(c["d"])] + ENC + [p])
        else:
            vf = clip_filter(c) + ",fps=60,setsar=1"
            run(["-ss", str(c["t"]), "-t", str(c["d"]), "-i", c.get("src", s["src"]), "-filter_complex", vf, "-an"] + ENC + [p])
        parts.append(p)
        total += c["d"]
    body = s["body"]
    assert abs(total - body) < 0.02, f"clips sum {total} != body {body}"
    lst = os.path.join(wd, "l.txt")
    open(lst, "w").write("".join(f"file '{os.path.basename(p)}'\n" for p in parts))

    font = "anton.ttf"
    dt = []
    for text, color, t0, t1 in s["caps"]:
        lines = text.split("|")
        cols = color.split(",")
        for j, ln in enumerate(lines):
            col = cols[min(j, len(cols) - 1)]
            fs = 165 if len(ln) <= 11 else int(165 * 11 / len(ln))
            y = 1080 + j * 190
            a, b = t0 + LEAD, min(t1 + LEAD, body)
            ln = ln.replace("'", "")
            dt.append(f"drawtext=fontfile={font}:text='{ln}':fontsize={fs}:fontcolor={col}:borderw=10:bordercolor=black:"
                      f"x=(w-text_w)/2:y={y}:enable='gte(t,{a:.3f})*lt(t,{b:.3f})'")
    vf = ",".join(dt)
    # copy font next to list so relative path works
    import shutil
    shutil.copy(s["font"], os.path.join(wd, font))
    cuerpo = os.path.join(wd, "cuerpo.mp4")
    cwd = os.getcwd(); os.chdir(wd)
    run(["-f", "concat", "-safe", "0", "-i", "l.txt", "-i", s["voice"], "-filter_complex",
         f"[0:v]{vf}[v];[1:a]adelay={int(LEAD*1000)}|{int(LEAD*1000)},volume=-1dB,aresample=48000,apad,atrim=0:{body},aformat=channel_layouts=stereo[a]",
         "-map", "[v]", "-map", "[a]", "-t", str(body)] + ENC + ["-r", "60", "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "cuerpo.mp4"])
    os.chdir(cwd)
    run(["-i", s["intro"], "-i", cuerpo, "-i", s["cats"], "-filter_complex",
         "[0:v][0:a][1:v][1:a][2:v][2:a]concat=n=3:v=1:a=1[v][a]", "-map", "[v]", "-map", "[a]",
         "-c:v", "libx264", "-crf", "16", "-preset", "slow", "-pix_fmt", "yuv420p", "-r", "60",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-movflags", "+faststart", s["out"]])
    dur = float(subprocess.run([FP, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", s["out"]],
                               capture_output=True, text=True).stdout)
    fz = subprocess.run([FF, "-i", s["out"], "-vf", "freezedetect=n=0.003:d=0.3", "-an", "-f", "null", "-"],
                        capture_output=True, text=True).stderr
    freezes = [l.split("freeze_start: ")[1].strip() for l in fz.splitlines() if "freeze_start" in l]
    ok = 9.30 <= dur <= 10.45
    print(f"{os.path.basename(s['out'])}: {dur:.3f}s {'OK' if ok else 'FUERA DE RANGO'} freezes={freezes}")
    # preview sheet
    run(["-i", s["out"], "-vf", "fps=2,scale=150:-1,tile=11x2", "-frames:v", "1", os.path.join(wd, "sheet.png")])


if __name__ == "__main__":
    for p in sys.argv[1:]:
        render(p)
