"""Primer corte de "Los jefes de Sonic que NADIE recuerda" (Bozz Killer). Copia del build de Mario (mismo pipeline).
Uso: py -3.12 build.py  |  py -3.12 build.py audio"""
import os, sys, json, subprocess
from concurrent.futures import ThreadPoolExecutor
from edl import PARRAFOS, RESPIROS

H = os.path.dirname(os.path.abspath(__file__))
FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
FONT = "C\\:/Windows/Fonts/arialbd.ttf"
ANTON = "C\\:/Users/delfa/Desktop/Videojogo/Marketing/Recursos/Fuentes/Anton-Regular.ttf"
P = os.path.join(H, "parts"); os.makedirs(P, exist_ok=True)
ENC = ["-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p", "-r", "30"]
GAP = 0.35          # silencio entre párrafos
PAUSA_BIO = 1.6     # silencio largo después de Biokinton (sin música)

LABEL = {
    'rivals2_ifrit': 'Sonic Rivals 2 (2007)', 'tt_fang': 'Sonic Triple Trouble (1994)', 'superstars_fang': 'Sonic Superstars (2023)',
    'fighters_fang': 'Sonic the Fighters (1996)', 'unl_darkgaia': 'Sonic Unleashed (2008)', 'terranigma_darkgaia': 'Terranigma (1995)',
    'sr_erazor': 'Sonic and the Secret Rings (2007)', 'heroes_metalmadness': 'Sonic Heroes (2004)',
    'bk_bosses': 'Sonic and the Black Knight (2009)', 'colorsds_negawisp': 'Sonic Colors DS (2010)',
    'boom_lyric': 'Sonic Boom: Rise of Lyric (2014)', 'spinball_bosses': 'Sonic Spinball (1993)',
    'sky_witchcart': "Tails' Skypatrol (1995)", 'sky_darkcastle': "Tails' Skypatrol (1995)",
}
PIXEL = {'tt_fang', 'terranigma_darkgaia', 'spinball_bosses', 'sky_witchcart', 'sky_darkcastle', 'colorsds_negawisp'}

# [TEXTO]: (párrafo, palabra de la frase donde aparece, texto)
TEXTOS = [(1, 'diecinueve', '19 AÑOS'), (2, 'veintisiete', '27 AÑOS'), (8, 'borraron', 'LO BORRARON'),
          (10, 'recopilaciones', 'CERO JUEGOS'), (11, 'única aparición', 'ÚNICA APARICIÓN')]

# Música por párrafos: (archivo, desde_párrafo, hasta_párrafo, offset)
# Temas clásicos de Sonic (los mismos de Gigantes de Sonic, que el autor aprobó), en escalada de ritmo.
MUSICA = [('c_springyard', 1, 2, 0), ('c_emeraldhill', 3, 4, 0), ('c_boss1', 5, 6, 0), ('c_boss2', 7, 8, 0),
          ('c_finalboss3', 9, 10, 0), ('c_doomsday', 11, 11, 0), ('c_imhere', 12, 12, 0)]


def lufs(nombre):
    """Sonoridad integrada de los primeros 60 s de un tema (cacheada en musica/lufs.json)."""
    cache = os.path.join(H, "musica", "lufs.json")
    d = json.load(open(cache)) if os.path.exists(cache) else {}
    if nombre not in d:
        r = subprocess.run([FF, "-hide_banner", "-t", "60", "-i", os.path.join(H, "musica", nombre + ".m4a"), "-af", "ebur128",
                            "-f", "null", "-"], capture_output=True, text=True).stderr
        d[nombre] = float(r.split("Integrated loudness:")[1].split("I:")[1].split("LUFS")[0])
        json.dump(d, open(cache, "w"))
    return d[nombre]


def run(a):
    subprocess.run([FF, "-hide_banner", "-loglevel", "error", "-y"] + a, check=True)


def esc(s):
    return s.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\u2019").replace("%", "\\%")


def src(n, ext="mp4"):
    return os.path.join(H, "fuentes", f"{n}.{ext}")


def tiempos():
    return json.load(open(os.path.join(H, "Voz", "tiempos.json"), encoding="utf-8"))


def linea():
    """Devuelve segmentos: dict(tipo, t0, dur, ...) en orden."""
    T = tiempos(); segs = []; t = 0.0
    resp = {r[0]: r for r in RESPIROS}
    for i in range(1, 13):
        if i in resp:
            _, s, a, d = resp[i]
            segs.append(dict(tipo='R', t0=t, dur=d, src=s, ss=a)); t += d
        p = T[i - 1]
        a, b = max(0, p['start'] - 0.05), p['end'] + 0.12
        extra = GAP + (PAUSA_BIO if i == 11 else 0)
        segs.append(dict(tipo='V', i=i, t0=t, dur=round(b - a + extra, 3), take=p['take'], a=a, b=b, sent=p['sent']))
        t += b - a + extra
    return segs, round(t, 3)


def vf_shot(s, d, txt=None):
    flags = "neighbor" if s in PIXEL else "lanczos"
    vf = ("split[a][b];[a]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,boxblur=30:2,eq=brightness=-0.3[bg];"
          f"[b]scale=-2:1080:flags={flags},scale='min(iw,1920)':-2[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2,fps=30,setsar=1,"
          f"drawtext=fontfile='{FONT}':text='{esc(LABEL.get(s, s))}':fontsize=38:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=12:x=40:y=h-96")
    return vf


def render(job):
    k, s, ss, d, con_audio = job
    out = os.path.join(P, f"{k:03d}.mp4")
    if os.path.exists(out) and os.path.getmtime(out) > os.path.getmtime(os.path.join(H, "edl.py")):
        return
    run(["-ss", str(ss), "-t", str(d), "-i", src(s), "-filter_complex", vf_shot(s, d)] + ENC + ["-an", "-t", str(d), out])


def video(segs):
    jobs, k = [], 0
    for g in segs:
        if g['tipo'] == 'R':
            jobs.append((k, g['src'], g['ss'], g['dur'], True)); k += 1
        else:
            shots = PARRAFOS[g['i']]; n = len(shots)
            base = round(g['dur'] / n, 3); acc = 0.0
            for j, (s, ss) in enumerate(shots):
                d = base if j < n - 1 else round(g['dur'] - acc, 3)
                jobs.append((k, s, ss, d, False)); k += 1; acc += d
    with ThreadPoolExecutor(6) as ex:
        list(ex.map(render, jobs))
    with open(os.path.join(P, "list.txt"), "w") as f:
        for j in jobs:
            f.write(f"file '{j[0]:03d}.mp4'\n")
    # -c copy funciona; el corte salía corto solo cuando algún plano pedía un segundo fuera de su fuente (parte vacía)
    for j in jobs:
        f = os.path.join(P, f"{j[0]:03d}.mp4")
        if os.path.getsize(f) < 2000:
            raise SystemExit(f"plano vacío {j}: el inicio pasa el largo de la fuente")
    run(["-f", "concat", "-safe", "0", "-i", os.path.join(P, "list.txt"), "-c", "copy", os.path.join(H, "video_mudo.mp4")])


def audio(segs, total):
    V = os.path.join(H, "Voz"); M = os.path.join(H, "musica")
    ins, fc, n, voz = [], "", 0, []
    juego = []
    for g in segs:
        if g['tipo'] == 'V':
            ins += ["-ss", str(g['a']), "-t", str(round(g['b'] - g['a'], 3)), "-i", os.path.join(V, g['take'] + ".mp3")]
            fc += (f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,apad,atrim=0:{g['dur']},"
                   f"adelay={int(g['t0'] * 1000)}|{int(g['t0'] * 1000)}[v{n}];"); voz.append(f"[v{n}]"); n += 1
        # pista del juego: un wav por tramo (silencio durante la voz, clip en los respiros) y concat demuxer.
        # Un solo filtro con todas las entradas (adelay o concat) hacía fallar/enmudecer a ffmpeg 9.
        w = os.path.join(P, f"j{len(juego):02d}.wav")
        if g['tipo'] == 'R' and os.path.exists(src(g['src'], "m4a")):
            run(["-ss", str(g['ss']), "-t", str(g['dur']), "-i", src(g['src'], "m4a"), "-af",
                 f"aresample=48000,aformat=channel_layouts=stereo,apad=whole_dur={g['dur']},afade=t=in:d=0.15,"
                 f"afade=t=out:st={g['dur'] - 0.3:.3f}:d=0.3,volume=0.8", "-ac", "2", "-ar", "48000", "-c:a", "pcm_s16le", w])
        else:
            run(["-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-t", str(g['dur']), "-c:a", "pcm_s16le", w])
        juego.append(w)
    T = {g['i']: g for g in segs if g['tipo'] == 'V'}
    ml = []
    for f, i0, i1, off in MUSICA:
        path = os.path.join(M, f + ".m4a")
        if not os.path.exists(path):
            continue
        a = T[i0]['t0']; b = T[i1]['t0'] + T[i1]['dur'] - (PAUSA_BIO + GAP if i1 == 11 else 0)
        d = round(b - a, 3)
        ins += ["-stream_loop", "-1", "-ss", str(off), "-i", path]
        fc += (f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:{d},asetpts=N/SR/TB,afade=t=in:d=0.6,"
               f"afade=t=out:st={d - 0.6:.3f}:d=0.6,volume={-16 - lufs(f):.1f}dB,adelay={int(a * 1000)}|{int(a * 1000)}[m{n}];")
        ml.append(f"[m{n}]"); n += 1
    fc += "".join(voz) + f"amix=inputs={len(voz)}:normalize=0,apad,atrim=0:{total}[voz];"
    fc += "".join(ml) + f"amix=inputs={len(ml)}:normalize=0,volume=0.12,apad,atrim=0:{total}[mus];"
    # la pista del juego se arma aparte (concat secuencial) y entra a la mezcla como un solo archivo
    jwav = os.path.join(P, "juego.wav")
    with open(os.path.join(P, "juego.txt"), "w") as f:
        for w in juego:
            f.write(f"file '{os.path.basename(w)}'\n")
    run(["-f", "concat", "-safe", "0", "-i", os.path.join(P, "juego.txt"), "-c", "copy", jwav])
    ins += ["-i", jwav]
    fc += f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,apad,atrim=0:{total}[jue];"
    mezcla = ["[voz]", "[mus]", "[jue]"]
    fc += "".join(mezcla) + f"amix=inputs={len(mezcla)}:normalize=0,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,atrim=0:{total}[a]"
    run(ins + ["-filter_complex", fc, "-map", "[a]", "-c:a", "pcm_s16le", os.path.join(H, "audio.wav")])


def textos(segs):
    T = {g['i']: g for g in segs if g['tipo'] == 'V'}
    f = []
    for i, clave, txt in TEXTOS:
        g = T[i]; s = next(x for x in g['sent'] if clave.lower() in x[2].lower())
        a = round(g['t0'] + (s[0] - g['a']), 2); b = a + 2.6
        f.append(f"drawtext=fontfile='{ANTON}':text='{esc(txt)}':fontsize=110:fontcolor=0xFFD23C:borderw=8:bordercolor=black:"
                 f"x=(w-text_w)/2:y=h*0.16:enable='between(t,{a},{b})'")
    return ",".join(f)


if __name__ == "__main__":
    segs, total = linea()
    json.dump(segs, open(os.path.join(H, "linea.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("duración", total)
    if "audio" not in sys.argv[1:]:
        video(segs)
    audio(segs, total)
    run(["-i", os.path.join(H, "video_mudo.mp4"), "-i", os.path.join(H, "audio.wav"), "-filter_complex", f"[0:v]{textos(segs)}[v]",
         "-map", "[v]", "-map", "1:a"] + ENC + ["-c:a", "aac", "-b:a", "192k", "-t", str(total), "-movflags", "+faststart",
         os.path.join(H, "JefesOlvidadosSonic_PrimerCorte.mp4")])
    print("ok")
