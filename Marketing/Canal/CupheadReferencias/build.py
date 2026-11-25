"""Arma el primer corte del largo de referencias de Cuphead desde edl.py.
Video 1920x1080 30 fps; dibujos 4:3 al centro sobre el mismo plano desenfocado, con etiqueta "Título (año)".
Audio: voz (p1a + p2a con el párrafo de Saltbaker reemplazado por salt_b) + música de Cuphead por bloques, loudnorm -14."""
import os, subprocess, json
from concurrent.futures import ThreadPoolExecutor
from edl import SHOTS, TOTAL, CUP

H = os.path.dirname(os.path.abspath(__file__))
FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
FP = r"C:\Users\delfa\Downloads\ffprobe.exe"
FONT = "C\\:/Windows/Fonts/arialbd.ttf"
P = os.path.join(H, "parts"); os.makedirs(P, exist_ok=True)
ENC = ["-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p", "-r", "30", "-an"]


def run(a):
    subprocess.run([FF, "-hide_banner", "-loglevel", "error", "-y"] + a, check=True)


def esc(s):
    return s.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\u2019")


def src(i):
    return os.path.join(H, "fuentes", i + ".mp4")


def part(k, start, shot):
    end, s, t, label = shot
    d = round(end - start, 3)
    out = os.path.join(P, f"{k:03d}.mp4")
    if s == "ACH":
        vf = ("scale=1920:1080,eq=brightness=-0.35,boxblur=6:1,"
              f"drawbox=x=460:y=380:w=1000:h=320:color=black@0.75:t=fill,"
              f"drawtext=fontfile='{FONT}':text='LOGRO DESBLOQUEADO':fontsize=46:fontcolor=0xFFD23C:x=(w-text_w)/2:y=420,"
              f"drawtext=fontfile='{FONT}':text='Swing You Sinner':fontsize=96:fontcolor=white:x=(w-text_w)/2:y=500,"
              f"drawtext=fontfile='{FONT}':text='(vencer al Diablo)':fontsize=40:fontcolor=0xCCCCCC:x=(w-text_w)/2:y=625,fps=30")
        run(["-ss", str(t), "-i", src(CUP), "-frames:v", "1", "-vf", vf, os.path.join(P, f"{k:03d}.png")])
        run(["-loop", "1", "-framerate", "30", "-t", str(d), "-i", os.path.join(P, f"{k:03d}.png")] + ENC + [out])
        return
    if s == CUP:
        vf = "scale=1920:1080:flags=lanczos,fps=30,setsar=1"
    else:
        vf = ("split[a][b];[a]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,boxblur=30:2,eq=brightness=-0.25[bg];"
              "[b]scale=-2:1080:flags=lanczos[fg];[bg][fg]overlay=(W-w)/2:0,fps=30,setsar=1,"
              f"drawtext=fontfile='{FONT}':text='{esc(label)}':fontsize=42:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=14:x=40:y=h-100")
    run(["-ss", str(t), "-t", str(d), "-i", src(s), "-filter_complex", vf] + ENC + ["-t", str(d), out])


def video():
    jobs, start = [], 0.0
    for k, sh in enumerate(SHOTS):
        jobs.append((k, start, sh)); start = sh[0]
    with ThreadPoolExecutor(6) as ex:
        list(ex.map(lambda j: part(*j), jobs))
    with open(os.path.join(P, "list.txt"), "w") as f:
        for k, _, _ in jobs:
            f.write(f"file '{k:03d}.mp4'\n")
    run(["-f", "concat", "-safe", "0", "-i", os.path.join(P, "list.txt"), "-c", "copy", os.path.join(H, "video_mudo.mp4")])


def audio():
    V = os.path.join(H, "Voz")
    M = os.path.join(H, "musica")
    voz = [(V + r"\p1a.mp3", 0, 152.96), (None, 0, 0.4), (V + r"\p2a.mp3", 0, 73.70),
           (V + r"\salt_b.mp3", 0, 9.30), (V + r"\p2a.mp3", 86.82, 136.96)]
    mus = [(M + r"\InkwellIsleOnePiano.mp3", 0, 0.0, 105.4), (M + r"\FloralFury.mp3", 10, 105.4, 189.6),
           (M + r"\ClipJointCalamity.mp3", 5, 189.6, 247.9), (M + r"\DieHouse.mp3", 0, 247.9, 262.0),
           (M + r"\DontDealWiththeDevil.mp3", 0, 262.0, TOTAL)]
    ins, fc, n = [], "", 0
    vl = []
    for f, a, b in voz:
        if f is None:
            ins += ["-f", "lavfi", "-t", str(b), "-i", "anullsrc=r=48000:cl=stereo"]
        else:
            ins += ["-ss", str(a), "-t", str(round(b - a, 3)), "-i", f]
        fc += f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo[v{n}];"; vl.append(f"[v{n}]"); n += 1
    fc += "".join(vl) + f"concat=n={len(vl)}:v=0:a=1,apad,atrim=0:{TOTAL}[voz];"
    ml = []
    for f, off, a, b in mus:
        d = round(b - a, 3)
        ins += ["-ss", str(off), "-t", str(d + 1), "-i", f]
        fc += (f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:{d},afade=t=in:d=0.8,afade=t=out:st={d - 0.8:.3f}:d=0.8,"
               f"adelay={int(a * 1000)}|{int(a * 1000)},apad,atrim=0:{TOTAL}[m{n}];"); ml.append(f"[m{n}]"); n += 1
    fc += "".join(ml) + f"amix=inputs={len(ml)}:normalize=0,volume=0.13[mus];"
    fc += f"[voz][mus]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,atrim=0:{TOTAL}[a]"
    run(ins + ["-filter_complex", fc, "-map", "[a]", "-c:a", "pcm_s16le", os.path.join(H, "audio.wav")])


if __name__ == "__main__":
    import sys
    if "audio" not in sys.argv[1:]:
        video()
    audio()
    run(["-i", os.path.join(H, "video_mudo.mp4"), "-i", os.path.join(H, "audio.wav"), "-map", "0:v", "-map", "1:a",
         "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-t", str(TOTAL), "-movflags", "+faststart",
         os.path.join(H, "Cuphead_Referencias_PrimerCorte.mp4")])
    print("ok")
