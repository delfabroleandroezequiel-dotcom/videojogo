"""Shorts A de Don Bozz con Fallen Down (Undertale) debajo — 2026-11-23.
Video: planos del largo (4:3 al centro 1440x1080 -> 1080x810 sobre el mismo plano desenfocado), subtitulos Georgia Bold
arriba (estilo mk.py). Voz: Homero = tomas limpias de Simpson/Voz; Calamardo = audio del largo (ya trae Fallen Down a -27 dB),
con Fallen Down agregado en fase (posicion = t_largo - 385.42)."""
import subprocess, os
FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
FD = r"C:\Users\delfa\Downloads\Fallen Down [qH5hM1ILuFQ].mp3"
D = r"C:\Users\delfa\Desktop\Videojogo\Marketing\DonBozz"
HOM = D + r"\Simpson\Homero_DonBozz.mp4"
CAL = D + r"\Calamardo_DonBozz.mp4"
TV = D + r"\Simpson\Voz\toma{}.mp3"
MUS_VOL = 0.18
VZ = lambda n: os.path.join(D, "Shorts", "build2", "voz", n + ".mp3")

S = {
 "Short_A_Homero_Reescribio": dict(vsrc=HOM, voice=[(TV.format(7), 14.34, 21.95)], mus=0.0,
   shots=[(319.6, 1.92), (378.3, 2.8), (359.2, 1.75), (355.0, 1.14)],
   cards=[(0, 1.92, "Las letras que quedaron\\Nentre las fotos dicen…"), (1.92, 3.4, "“Do it for her.”"),
          (3.4, 4.7, "Hazlo por ella."), (4.7, 6.6, "Homero no arrancó la placa."), (6.6, 7.61, "La reescribió.")]),
 "Short_A_Homero_EligioQuedarse": dict(vsrc=HOM, voice=[(VZ("n2a"), 0.0, 7.25)], mus=30.0,
   shots=[(558.3, 1.75), (298.9, 2.2), (234.0, 2.0), (308.9, 1.3)],
   cards=[(0, 1.95, "Homero no es un vago\\Nque no se va."), (1.95, 4.0, "Es un hombre\\Nque eligió quedarse."), (4.0, 5.6, "El peor empleado de Springfield…"), (5.6, 7.25, "nunca estuvo ahí por él.")]),
 "Short_A_Homero_NuestrasFotos": dict(vsrc=HOM, voice=[(TV.format(3), 63.18, 65.62), (TV.format(3), 70.66, 72.02),
                                                      (TV.format(3), 74.36, 77.70)], mus=12.0,
   shots=[(331.0, 2.44), (444.1, 1.36), (450.0, 1.94), (464.0, 1.4)],
   cards=[(0, 2.44, "Muchos de nuestros padres\\Ntenían una placa así."), (2.44, 3.8, "Y nunca nos dijeron nada."),
          (3.8, 5.8, "Y en algún lugar,\\Ntapando la placa…"), (5.8, 7.14, "tenían nuestras fotos.")]),
 "Short_A_Homero_Gracias": dict(vsrc=HOM, voice=[(VZ("n4a"), 0.0, 8.05)], mus=22.0,
   shots=[(328.0, 1.8), (345.0, 1.5), (450.0, 1.6), (413.8, 2.3), (310.8, 0.85)],
   cards=[(0, 2.25, "Si alguien en tu casa\\Ntiene una placa así,"), (2.25, 3.55, "tapada con tu foto,"), (3.55, 5.85, "no esperes a ser grande\\Npara entenderlo."), (5.85, 8.05, "Hoy es un buen día\\Npara decirle gracias.")]),
 "Short_A_Homero_NadieAgradece": dict(vsrc=HOM, voice=[(VZ("n5a"), 0.0, 7.7)], mus=15.0,
   shots=[(504.0, 2.6), (558.3, 1.35), (234.0, 1.7), (530.8, 2.05)],
   cards=[(0, 2.6, "Homero hace esa elección\\Ntodos los días."), (2.6, 3.95, "Un trabajo que odia,"), (3.95, 5.65, "un jefe que no sabe su nombre…"), (5.65, 7.7, "y nadie se la agradece.")]),
 "Short_A_Calamardo_Amargado": dict(vsrc=CAL, voice=[(VZ("m1a"), 0.0, 9.0)], mus=3.0,
   shots=[(259.45, 2.1), (118.8, 1.9), (129.2, 1.8), (159.3, 3.2)],
   cards=[(0, 2.1, "Calamardo no es un\\Namargado porque sí."), (2.1, 5.8, "Es un artista que nunca tuvo\\Nun solo momento de reconocimiento."), (5.8, 7.1, "Y eso, con los años…"), (7.1, 9.0, "a cualquiera lo vuelve amargo.")]),
 "Short_A_Calamardo_UnicoAdulto": dict(vsrc=CAL, voice=[(VZ("k2b"), 0.0, 8.2)], mus=40.0,
   shots=[(439.0, 2.5), (390.0, 2.05), (133.6, 1.4), (255.0, 2.25)],
   cards=[(0, 2.5, "Calamardo es el único\\Nadulto de la serie."), (2.5, 4.55, "El único con un sueño\\Nque no se cumple,"), (4.55, 5.95, "un trabajo que odia…"), (5.95, 8.2, "y la conciencia de que\\Nel tiempo pasa.")]),
 "Short_A_Calamardo_SeSienteVisto": dict(vsrc=CAL, voice=[(VZ("m3a"), 0.0, 7.5)], mus=8.0,
   shots=[(339.0, 2.85), (359.0, 1.65), (397.5, 3.0)],
   cards=[(0, 2.85, "Su cara durante Dulce Victoria\\Nlo dice todo."), (2.85, 4.5, "No es la cara de\\Nalguien que ganó."), (4.5, 7.5, "Es la cara de alguien\\Nque por fin se siente visto.")]),
 "Short_A_Calamardo_ElMundo": dict(vsrc=CAL, voice=[(VZ("k4b"), 0.0, 9.22)], mus=26.0,
   shots=[(245.1, 2.3), (139.0, 2.05), (224.0, 2.65), (263.5, 2.22)],
   cards=[(0, 2.6, "Toda su vida esperó que el mundo\\Nle diera la razón,"), (2.6, 4.45, "y el mundo nunca se la dio."), (4.45, 7.3, "Entonces se cerró\\Ny despreció a todos"), (7.3, 9.22, "antes de que\\Nlo despreciaran a él.")]),
}

HDR = """[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Short,Georgia,78,&H00D0E8F6,&H00D0E8F6,&H00000000,&H96000000,1,0,0,0,100,100,0,0,1,6,3,8,60,60,330,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""


def ts(x):
    h = int(x // 3600); m = int(x % 3600 // 60); s = x - h * 3600 - m * 60
    return f"{h}:{m:02d}:{s:05.2f}"


def build(name, s):
    dur = round(sum(b - a for _, a, b in s["voice"]), 3)
    vdur = round(sum(d for _, d in s["shots"]), 3)
    assert abs(dur - vdur) < 0.03, (name, dur, vdur)
    with open(name + ".ass", "w", encoding="utf-8") as f:
        f.write(HDR)
        for t0, t1, txt in s["cards"]:
            f.write(f"Dialogue: 0,{ts(t0)},{ts(min(t1, dur))},Short,,0,0,0,,{txt}\n")
    ins = ["-i", s["vsrc"]]
    fc = ""
    for i, (t, d) in enumerate(s["shots"]):
        fc += f"[0:v]trim={t}:{t + d},setpts=PTS-STARTPTS,fps=60[v{i}];"
    fc += "".join(f"[v{i}]" for i in range(len(s["shots"]))) + f"concat=n={len(s['shots'])}:v=1:a=0[cv];"
    fc += ("[cv]crop=1440:1080:240:0,split=2[fg][bg];[bg]scale=-2:1920,crop=1080:1920,boxblur=28:2,eq=brightness=-0.18:saturation=0.8[bgb];"
           f"[fg]scale=1080:810[fgs];[bgb][fgs]overlay=0:(H-h)/2,subtitles={name}.ass,format=yuv420p,trim=0:{dur}[vout];")
    al = []
    for k, (src, a, b) in enumerate(s["voice"]):
        ins += ["-ss", str(a), "-t", str(round(b - a, 3)), "-i", src]
        fc += f"[{k + 1}:a]aresample=48000,aformat=channel_layouts=stereo,afade=t=in:d=0.03,afade=t=out:st={b - a - 0.05:.3f}:d=0.05[va{k}];"
        al.append(f"[va{k}]")
    fc += "".join(al) + f"concat=n={len(al)}:v=0:a=1[voz];"
    mpos = s["voice"][0][1] - 385.42 if s["mus"] == "sync" else s["mus"]
    m = len(s["voice"]) + 1
    ins += ["-ss", f"{mpos:.3f}", "-t", str(dur + 0.5), "-i", FD]
    fc += (f"[{m}:a]aresample=48000,aformat=channel_layouts=stereo,volume={MUS_VOL},afade=t=in:d=0.15,"
           f"afade=t=out:st={dur - 0.3:.3f}:d=0.3[mus];"
           f"[voz][mus]amix=inputs=2:normalize=0:duration=first,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,apad,atrim=0:{dur}[aout]")
    subprocess.run([FF, "-hide_banner", "-loglevel", "error", "-y"] + ins + ["-filter_complex", fc, "-map", "[vout]", "-map", "[aout]",
                    "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-r", "60", "-c:a", "aac", "-b:a", "192k",
                    "-movflags", "+faststart", "-t", str(dur), os.path.join("..", name + ".mp4")], check=True)
    print("ok", name, dur)


if __name__ == "__main__":
    import sys
    for n in (sys.argv[1:] or S):
        build(n, S[n])
