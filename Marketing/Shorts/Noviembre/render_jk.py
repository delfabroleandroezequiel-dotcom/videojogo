"""Render de short vertical de Jekyria (receta Diseno/JefeMarketing.md §4.5).
spec JSON:
  out, voice, voice_start, font,
  clips: [{src, t, d, x, y=0, w=608, mode="crop"|"fit", vol=1.0, eq=""}],
  caps:  [["LINEA1|LINEA2", "white"|"yellow"|"white,yellow", t0, t1]]   (tiempos absolutos)
  music (opcional): {src, t, vol}
Audio: juego por clip (vol), agachado bajo la voz con sidechaincompress; voz x1.3; loudnorm -14 LUFS.
"""
import json, os, subprocess, sys, shutil

FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
FP = r"C:\Users\delfa\Downloads\ffprobe.exe"
ENC = ["-c:v", "libx264", "-crf", "15", "-preset", "fast", "-pix_fmt", "yuv420p", "-r", "60"]
YEL = "0xFFD23C"


def run(a):
    subprocess.run([FF, "-v", "error"] + a + ["-y"], check=True)


def src_h(src):
    return int(subprocess.run([FP, "-v", "error", "-select_streams", "v", "-show_entries", "stream=height", "-of", "csv=p=0", src],
                              capture_output=True, text=True).stdout.split()[0])


def vfilter(c):
    eq = ("," + c["eq"]) if c.get("eq") else ""
    # coordenadas siempre en 1080p (o 2160p para la fuente 4K): las grabaciones 720p se escalan antes de recortar
    pre = "scale=1920:1080:flags=lanczos," if src_h(c["src"]) == 720 else ""
    if c.get("mode", "crop") == "fit":
        w = c.get("w", 1440); x = c.get("x", 240)
        return (f"[0:v]{pre}crop={w}:1080:{x}:0{eq},split[p][q];[p]scale=-2:1920,crop=1080:1920,boxblur=30:3,eq=brightness=-0.2[bg];"
                f"[q]scale=1080:-2:flags=lanczos[fg];[bg][fg]overlay=0:{c.get('fy', 720)},setsar=1,fps=60[v]")
    w = c.get("w", 608); h = c.get("h", min(1080 if w <= 608 else 2160, int(w * 16 / 9)))
    return f"[0:v]{pre}crop={w}:{h}:{c['x']}:{c.get('y', 0)},scale=1080:1920:flags=lanczos{eq},setsar=1,fps=60[v]"


def render(path):
    s = json.load(open(path, encoding="utf-8"))
    wd = os.path.splitext(path)[0] + "_work"; os.makedirs(wd, exist_ok=True)
    parts, total = [], 0.0
    for i, c in enumerate(s["clips"]):
        p = os.path.join(wd, f"p{i}.mp4"); d = c["d"]
        vol = c.get("vol", 1.0)
        af = f"[0:a]volume={vol},aresample=48000,aformat=channel_layouts=stereo,apad,atrim=0:{d}[a]"
        run(["-ss", str(c["t"]), "-t", str(d), "-i", c["src"], "-filter_complex", vfilter(c) + ";" + af,
             "-map", "[v]", "-map", "[a]", "-t", str(d)] + ENC + ["-c:a", "pcm_s16le", p.replace(".mp4", ".mov")])
        parts.append(p.replace(".mp4", ".mov")); total += d
    n = len(parts)
    ins = sum([["-i", p] for p in parts], [])
    cat = "".join(f"[{i}:v][{i}:a]" for i in range(n)) + f"concat=n={n}:v=1:a=1[v][a]"
    body = os.path.join(wd, "body.mov")
    run(ins + ["-filter_complex", cat, "-map", "[v]", "-map", "[a]"] + ENC + ["-c:a", "pcm_s16le", body])

    shutil.copy(s["font"], os.path.join(wd, "anton.ttf"))
    dt = []
    for text, color, t0, t1 in s["caps"]:
        lines = text.split("|"); cols = color.split(",")
        for j, ln in enumerate(lines):
            col = cols[min(j, len(cols) - 1)]
            col = YEL if col == "yellow" else col
            fs = 104 if len(ln) <= 14 else int(104 * 14 / len(ln))
            ln = ln.replace("'", "\u2019").replace(":", "\\:")
            dt.append(f"drawtext=fontfile=anton.ttf:text='{ln}':fontsize={fs}:fontcolor={col}:borderw=9:bordercolor=black:"
                      f"x=(w-text_w)/2:y={520 + j * 125}:enable='between(t,{t0:.3f},{t1:.3f})'")
    vs = int(s.get("voice_start", 0.15) * 1000)
    fc = (f"[0:v]{','.join(dt)}[v];"
          f"[1:a]adelay={vs}|{vs},aresample=48000,aformat=channel_layouts=stereo,volume=1.3,apad,atrim=0:{total},asplit[vo][sc];")
    extra = []
    if s.get("music"):
        m = s["music"]
        extra = ["-ss", str(m["t"]), "-i", m["src"]]
        fc += (f"[2:a]aresample=48000,aformat=channel_layouts=stereo,volume={m['vol']},apad,atrim=0:{total}[mu];"
               f"[0:a][mu]amix=inputs=2:normalize=0[g0];")
    else:
        fc += "[0:a]anull[g0];"
    fc += (f"[g0][sc]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=350[gd];"
           f"[gd][vo]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,"
           f"afade=t=out:st={total - 0.3:.2f}:d=0.3,apad,atrim=0:{total}[a]")
    cwd = os.getcwd(); os.chdir(wd)
    run(["-i", "body.mov", "-i", s["voice"]] + extra + ["-filter_complex", fc, "-map", "[v]", "-map", "[a]", "-t", f"{total}",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-r", "60",
         "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", s["out"]])
    os.chdir(cwd)
    q = lambda sel: subprocess.run([FP, "-v", "error", "-select_streams", sel, "-show_entries", "stream=duration", "-of", "csv=p=0", s["out"]],
                                   capture_output=True, text=True).stdout.strip()
    print(f"{os.path.basename(s['out'])}: video {q('v')} s, audio {q('a')} s (cuerpo {total:.2f})")
    run(["-i", s["out"], "-vf", "fps=2,scale=160:-1,tile=10x2", "-frames:v", "1", os.path.join(wd, "sheet.png")])


if __name__ == "__main__":
    for p in sys.argv[1:]:
        render(p)
