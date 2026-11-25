"""v2 del largo de Cuphead (>6 min): toma los planos del primer corte (edl.SHOTS, tiempos globales del corte 1) y los
reparte en bloques de voz, intercalando 3 entradas nuevas (A: Oswald/Bimbo, B: Root Pack, H: hecho a mano) y 5 respiros
con el audio original del dibujo (la música se apaga en los respiros)."""
import os, subprocess
from concurrent.futures import ThreadPoolExecutor
import build as B
from edl import SHOTS, CUP, L, SWING, SKEL, COOKIE, MINNIE, HELL, BIMBO

H = B.H
VZ = os.path.join(H, "Voz")
TROLLEY, ONION, HOWMADE = "trolley", "onion", "howmade1939"
L.update({TROLLEY: "Trolley Troubles (1927)", ONION: "The Tears of an Onion (1938)",
          HOWMADE: "How Walt Disney Cartoons Are Made (1939)"})


def old(a, b):
    """Planos del corte 1 entre los tiempos globales a y b, en tiempo relativo al bloque."""
    out, start = [], 0.0
    for sh in SHOTS:
        end, s, t, lab = sh
        if end <= a + 1e-6 or start >= b - 1e-6:
            start = end; continue
        s0, s1 = max(start, a), min(end, b)
        out.append((round(s1 - a, 3), s, t + (s0 - start), lab))
        start = end
    return out


def C(end, t): return (end, CUP, t, None)
def O(end, src, t): return (end, src, t, L[src])


# bloques: ("V", [(archivo, a, b), ...], planos relativos) | ("R", fuente, entrada, duración)
SEG = [
 ("V", [("p1a", 0, 21.8)], old(0, 21.8)),
 ("V", [("p1a", 21.8, 42.3)], old(21.8, 42.3)),
 ("V", [("ins_A", 0, 20.95)], [C(5.3, 47.4), O(12.0, TROLLEY, 144.0), C(16.9, 4943.0), O(20.95, BIMBO, 104.0)]),
 ("V", [("p1a", 42.3, 54.2)], old(42.3, 54.2)),
 ("R", SWING, 363.5, 5.5),
 ("V", [("p1a", 54.2, 78.7)], old(54.2, 78.7)),
 ("V", [("ins_B", 0, 17.15)], [C(3.2, 357.0), C(6.4, 382.0), O(10.5, ONION, 94.0), O(13.3, ONION, 140.0), C(17.15, 384.0)]),
 ("V", [("p1a", 78.7, 105.4)], old(78.7, 105.4)),
 ("R", SKEL, 166.0, 5.5),
 ("V", [("p1a", 105.4, 118.2)], old(105.4, 118.2)),
 ("R", COOKIE, 260.5, 5.0),
 ("V", [("p1a", 118.2, 152.96), (None, 0, 0.4)], old(118.2, 153.36)),
 ("V", [("p2a", 0, 36.24)], old(153.36, 189.6)),
 ("R", MINNIE, 120.0, 6.0),
 ("V", [("p2a", 36.24, 73.70), ("salt_b", 0, 9.30), ("p2a", 86.82, 125.46)], old(189.6, 275.0)),
 ("R", HELL, 161.0, 5.5),
 ("V", [("p2a", 125.46, 133.56)], old(275.0, 283.1)),
 ("V", [("ins_H", 0, 14.6)], [O(3.1, HOWMADE, 150.5), C(6.7, 1131.0), O(9.2, HOWMADE, 171.8), O(12.6, HOWMADE, 405.0),
                              C(14.6, 3754.0)]),
 ("V", [("p2a", 133.56, 136.96), (None, 0, 1.2)], [C(4.6, 4934.0)]),
]


def main():
    jobs, voz, resp, t0 = [], [], [], 0.0
    for seg in SEG:
        if seg[0] == "V":
            _, pieces, shots = seg
            dur = round(sum(b - a for _, a, b in pieces), 3)
            assert abs(shots[-1][0] - dur) < 0.06, (pieces, shots[-1][0], dur)
            st = 0.0
            for end, s, t, lab in shots:
                end = min(end, dur)
                jobs.append((t0 + st, (t0 + end, s, t, lab))); st = end
            voz += pieces
        else:
            _, s, t, d = seg
            jobs.append((t0, (t0 + d, s, t, L[s])))
            voz.append(("R", s, t, d)); resp.append((t0, t0 + d)); dur = d
        t0 = round(t0 + dur, 3)
    total = t0
    print("duración total", total)
    with ThreadPoolExecutor(6) as ex:
        list(ex.map(lambda kj: B.part(kj[0], kj[1][0], kj[1][1]), enumerate(jobs)))
    with open(os.path.join(B.P, "list2.txt"), "w") as f:
        for k in range(len(jobs)):
            f.write(f"file '{k:03d}.mp4'\n")
    B.run(["-f", "concat", "-safe", "0", "-i", os.path.join(B.P, "list2.txt"), "-c", "copy", os.path.join(H, "video_mudo2.mp4")])

    ins, fc, n, vl = [], "", 0, []
    for p in voz:
        if p[0] == "R":
            _, s, t, d = p
            ins += ["-ss", str(t), "-t", str(d), "-i", B.src(s)]
            fc += f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,volume=0.9,afade=t=in:d=0.2,afade=t=out:st={d - 0.3}:d=0.3,apad,atrim=0:{d}[v{n}];"
        elif p[0] is None:
            ins += ["-f", "lavfi", "-t", str(p[2]), "-i", "anullsrc=r=48000:cl=stereo"]
            fc += f"[{n}:a]anull[v{n}];"
        else:
            f, a, b = p
            ins += ["-ss", str(a), "-t", str(round(b - a, 3)), "-i", os.path.join(VZ, f + ".mp3")]
            fc += f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,apad,atrim=0:{round(b - a, 3)}[v{n}];"
        vl.append(f"[v{n}]"); n += 1
    fc += "".join(vl) + f"concat=n={len(vl)}:v=0:a=1,apad,atrim=0:{total}[voz];"
    T = lambda i: sum((sum(b - a for _, a, b in s[1]) if s[0] == "V" else s[3]) for s in SEG[:i])
    M = os.path.join(H, "musica")
    mus = [(M + r"\InkwellIsleOnePiano.mp3", 0, 0.0, T(6)), (M + r"\FloralFury.mp3", 10, T(6), T(12)),
           (M + r"\ClipJointCalamity.mp3", 5, T(12), total - 41.0), (M + r"\DontDealWiththeDevil.mp3", 0, total - 41.0, total)]
    ml = []
    for f, off, a, b in mus:
        d = round(b - a, 3)
        ins += ["-ss", str(off), "-t", str(d + 1), "-i", f]
        fc += (f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:{d},afade=t=in:d=0.8,afade=t=out:st={d - 0.8:.3f}:d=0.8,"
               f"adelay={int(a * 1000)}|{int(a * 1000)},apad,atrim=0:{total}[m{n}];"); ml.append(f"[m{n}]"); n += 1
    gate = "+".join(f"between(t,{a - 0.2:.2f},{b + 0.2:.2f})" for a, b in resp)
    fc += "".join(ml) + f"amix=inputs={len(ml)}:normalize=0,volume=0.13,volume='if({gate},0,1)':eval=frame[mus];"
    fc += f"[voz][mus]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,atrim=0:{total}[a]"
    B.run(ins + ["-filter_complex", fc, "-map", "[a]", "-c:a", "pcm_s16le", os.path.join(H, "audio2.wav")])
    B.run(["-i", os.path.join(H, "video_mudo2.mp4"), "-i", os.path.join(H, "audio2.wav"), "-map", "0:v", "-map", "1:a",
           "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-t", str(total), "-movflags", "+faststart",
           os.path.join(H, "Cuphead_Referencias_Corte2.mp4")])
    print("ok", total)


if __name__ == "__main__":
    main()
