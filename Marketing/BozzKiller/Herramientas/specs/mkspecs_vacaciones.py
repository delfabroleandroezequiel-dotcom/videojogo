"""Specs: lote vacaciones 2026-11-25 (fuentes en ingles).
- Mario 64: Nintenduo World "25 SECRETS in SUPER MARIO 64" xAhXaaxYhdg (1080p60; numero arriba-izq fuera del 4:3, logo abajo-der ~x1650).
- Cuphead: The Leaderboard "Cuphead - Easter Eggs, Secrets and References" 1slEGU4AX0E (1080p24, sin marca). c2 usa un plano de SMW de WhPwDlickWM.
- GTA 5: IGN "GTA 5: 29 Secrets and Easter Eggs" yVbPvycZ_Xg (720p30 -> fuente1080.mp4; numero arriba-izq, logo IGN abajo-der).
Voces en <Juego>/voces*/<k>_f.mp3 (algunas con atempo 1.03-1.08). Correr desde cualquier carpeta."""
import json
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
FONT = P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf"
W, Y, WY, YW = "white", "yellow", "white,yellow", "yellow,white"
SMW = P + r"\Descargas\WhPwDlickWM\fuente.mp4"
cr = lambda t, d, x=656: dict(t=t, d=d, mode="crop", x=x)
fit = lambda t, d, **k: dict(dict(t=t, d=d, mode="fit"), **k)
box = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="box", x=x, y=y, w=w, h=h)
st = lambda t, d, x=656, y=0, w=608, h=1080, z=0.12: dict(t=t, d=d, mode="still", x=x, y=y, w=w, h=h, zoom=z)
gfit = lambda t, d: fit(t, d, x=360, w=1200, y=80, h=860)   # GTA: sin numero ni logo IGN

GAMES = {
 "m": (P + r"\Descargas\xAhXaaxYhdg\fuente.mp4", "Mario", "Mario64", "voces"),
 "c": (P + r"\Descargas\1slEGU4AX0E\fuente.mp4", "Cuphead", "Cuphead", "voces3"),
 "g": (P + r"\Descargas\yVbPvycZ_Xg\fuente1080.mp4", "GTA", "GTA5", "voces"),
}
S = {
 # ---------------- MARIO 64 (cuerpo 6.81-7.95) ----------------
 "m1": ("Short01_RisaBoo", 6.87,
   [fit(193.0, 1.4), cr(195.0, 1.2, 650), st(198.0, 1.0), st(199.5, 0.9), st(201.5, 1.1), st(205.0, 1.27)],
   [["SABIAS QUE", W, 0, 0.52], ["EN MARIO 64", W, 0.52, 1.52], ["LA RISA|DE LOS BOO", WY, 1.6, 2.46],
    ["ES LA RISA|DE BOWSER", WY, 2.46, 3.46], ["ACELERADA", Y, 3.46, 4.4], ["SI LA PONES|LENTA", W, 4.5, 5.45],
    ["SE ESCUCHA|CLARITO", WY, 5.5, 6.77]]),
 "m2": ("Short02_PinguinoGordo", 7.53,
   [fit(117.6, 1.6), fit(121.5, 1.9, x=300, w=1440, y=0, h=980), cr(123.0, 1.6, 950), cr(125.5, 1.4, 800), st(125.0, 1.03, x=1100)],
   [["SABIAS QUE", W, 0, 0.52], ["EN MARIO 64", W, 0.52, 1.62], ["SI CONSIGUES|TODAS LAS ESTRELLAS", WY, 1.62, 3.5],
    ["Y RETAS AL|PINGÜINO", W, 3.5, 4.6], ["DE LA CARRERA", W, 4.6, 5.12], ["APARECE MUCHO|MAS GORDO", WY, 5.12, 6.5],
    ["SE LAS COMIO", Y, 6.6, 7.43]]),
 "m3": ("Short03_EscaleraInfinita", 7.95,
   [cr(822.0, 1.5), cr(825.0, 0.9), cr(829.0, 1.2), cr(836.0, 2.0), cr(841.0, 1.4), cr(843.0, 0.95)],
   [["SABIAS QUE", W, 0, 0.6], ["LA ESCALERA|INFINITA", WY, 0.6, 1.5], ["DE MARIO 64", W, 1.5, 2.36],
    ["ES UNA TRAMPA", Y, 2.36, 3.4], ["REPITE EL|MISMO TRAMO", W, 3.6, 4.72], ["UNA Y OTRA VEZ", Y, 4.72, 5.64],
    ["Y LA MUSICA|PARECE SUBIR", W, 5.64, 7.0], ["PARA SIEMPRE", Y, 7.2, 7.85]]),
 "m4": ("Short04_ConejoMips", 7.95,
   [cr(340.0, 1.6), cr(344.0, 1.0), st(352.0, 1.0, x=200), st(351.5, 0.7, x=1120), cr(356.0, 1.0), cr(342.2, 1.6), cr(357.0, 1.05)],
   [["SABIAS QUE", W, 0, 0.5], ["EL CONEJO|AMARILLO", WY, 0.5, 1.4], ["DE MARIO 64", W, 1.4, 2.36],
    ["SE LLAMA|MIPS", WY, 2.36, 3.2], ["POR EL|PROCESADOR", W, 3.2, 4.05], ["DE LA|NINTENDO 64", W, 4.05, 5.26],
    ["ERA SOLO UN|PERSONAJE DE PRUEBA", WY, 5.3, 6.86], ["Y SE QUEDO", Y, 6.9, 7.85]]),
 "m5": ("Short05_MariposasRuleta", 7.95,
   [cr(661.0, 1.6), cr(662.6, 1.4), fit(664.0, 1.0), fit(665.0, 1.5), cr(668.0, 0.8, 900), cr(673.4, 1.65, 500)],
   [["SABIAS QUE", W, 0, 0.64], ["EN MARIO 64", W, 0.64, 1.64], ["ALGUNAS|MARIPOSAS", WY, 1.64, 2.58],
    ["SON UNA|RULETA", WY, 2.58, 3.8], ["SI LAS|GOLPEAS", W, 4.0, 4.84], ["PUEDEN DARTE UNA|VIDA EXTRA", WY, 4.84, 6.3],
    ["O EXPLOTARTE|EN LA CARA", WY, 6.5, 7.85]]),
 # ---------------- CUPHEAD (cuerpo 6.54-7.69) ----------------
 "c1": ("Short12_TazaPropaganda", 6.6,
   [cr(46.0, 1.0, 700), fit(51.0, 1.6), fit(53.0, 1.5, x=330, w=1260, y=60, h=960), fit(54.0, 1.1, x=330, w=1260, y=60, h=960),
    fit(55.5, 1.4, x=300, w=1300, y=40, h=1000)],
   [["SABIAS QUE|CUPHEAD", WY, 0, 0.92], ["SE INSPIRO EN|UNA CARICATURA", W, 0.92, 2.1], ["JAPONESA", Y, 2.1, 2.68],
    ["DONDE UN HOMBRE", W, 2.68, 3.3], ["CON CABEZA|DE TAZA", WY, 3.3, 4.22], ["SE VUELVE|TANQUE", WY, 4.22, 5.2],
    ["Y ATACA A|MICKEY MOUSE", WY, 5.2, 6.5]]),
 "c2": ("Short13_MismaCancion", 7.4,
   [fit(503.0, 1.0), fit(504.2, 0.9), dict(fit(98.0, 1.6), src=SMW), fit(507.0, 1.6), st(511.0, 1.0, x=690, y=0, w=540, h=960), fit(509.0, 1.3)],
   [["SABIAS QUE", W, 0, 0.48], ["LA MUSICA|DE LA FERIA", W, 0.48, 1.32], ["DE CUPHEAD", W, 1.32, 1.84],
    ["SUENA COMO LA DE|SUPER MARIO WORLD", WY, 1.84, 3.52], ["ES PORQUE|LAS DOS", W, 3.66, 4.28],
    ["SALEN DE LA|MISMA CANCION", WY, 4.28, 5.4], ["DE 1899", Y, 5.4, 7.1]]),
 "c3": ("Short14_RanasStreetFighter", 7.65,
   [fit(472.0, 1.0), cr(475.0, 1.3, 1260), cr(476.3, 1.2, 1250), fit(478.0, 1.4, x=380, w=1180, y=90, h=900),
    fit(480.0, 1.0, x=320, w=1240, y=90, h=900), fit(481.5, 1.75)],
   [["SABIAS QUE", W, 0, 0.52], ["LAS RANAS|BOXEADORAS", WY, 0.52, 1.6], ["DE CUPHEAD", W, 1.6, 2.3],
    ["RIBBY Y|CROAKS", W, 2.44, 3.34], ["POSAN|IGUALITO", W, 3.34, 4.14], ["QUE KEN|Y AKUMA", WY, 4.14, 5.12],
    ["DE STREET|FIGHTER", W, 5.12, 6.0], ["SOLO LES FALTA|EL HADOUKEN", WY, 6.2, 7.55]]),
 "c4": ("Short15_NavePayaso", 7.0,
   [fit(535.0, 1.0), fit(540.5, 1.5), cr(541.5, 1.2, 1050), cr(546.6, 1.5, 820), fit(543.0, 1.8)],
   [["SABIAS QUE", W, 0, 0.5], ["EL DOCTOR KAHL|DE CUPHEAD", YW, 0.5, 1.66], ["HUYE VOLANDO", W, 1.66, 2.4],
    ["EN LA MISMA|NAVE PAYASO", WY, 2.4, 3.44], ["DE BOWSER", Y, 3.44, 3.86], ["EN SUPER|MARIO WORLD", W, 3.86, 5.24],
    ["HASTA LOS VILLANOS|SE COPIAN", WY, 5.24, 6.9]]),
 "c5": ("Short16_TrenLego", 7.67,
   [fit(709.0, 1.3), fit(711.0, 1.0), cr(712.6, 1.3, 700), st(713.3, 0.6, x=780, y=300, w=405, h=720),
    fit(716.6, 2.02), fit(720.5, 1.45, x=220, w=1500, y=110, h=850)],
   [["SABIAS QUE", W, 0, 0.48], ["EL ULTIMO VAGON|DEL TREN FANTASMA", WY, 0.48, 1.84], ["DE CUPHEAD", W, 1.84, 2.38],
    ["LLEVA EL|NUMERO", W, 2.38, 3.02], ["4561", Y, 3.02, 4.2], ["ES EL MISMO|NUMERO", W, 4.46, 5.22],
    ["DE UN TREN|DE LEGO", WY, 5.22, 6.1], ["COINCIDENCIA", W, 6.2, 7.0], ["NO CREO", Y, 7.1, 7.6]]),
 # ---------------- GTA 5 (cuerpo 7.12-8.27) ----------------
 "g1": ("Short01_FantasmaMonteGordo", 8.2,
   [cr(341.0, 1.4, 560), cr(342.4, 1.1, 560), st(345.8, 1.3, z=0.18), cr(347.0, 1.3, 560), cr(348.6, 1.3, 560), cr(350.5, 1.0, 760), cr(352.0, 0.8, 800)],
   [["SABIAS QUE", W, 0, 0.5], ["EN GTA 5", W, 0.5, 1.36], ["A LAS 11|DE LA NOCHE", W, 1.36, 2.48],
    ["APARECE UN|FANTASMA", WY, 2.48, 3.78], ["EN LA CIMA DEL|MONTE GORDO", W, 3.78, 4.9], ["SI TE ACERCAS", W, 5.1, 5.82],
    ["DESAPARECE", Y, 5.82, 6.42], ["Y DEJA ESCRITO|UN NOMBRE", W, 6.42, 7.5], ["JACK", Y, 7.6, 8.1]]),
 "g2": ("Short02_AlienCongelado", 7.55,
   [gfit(202.0, 1.0), cr(203.0, 1.5), cr(204.5, 0.9), cr(205.4, 1.0), cr(206.4, 0.7, 700), st(207.8, 2.45, x=780, y=280, w=360, h=640, z=0.25)],
   [["SABIAS QUE", W, 0, 0.52], ["EN EL PROLOGO|DE GTA 5", W, 0.52, 1.86], ["SI PASAS POR|DEBAJO DEL PUENTE", W, 2.12, 3.64],
    ["HAY UN ALIEN|CONGELADO", WY, 3.88, 4.9], ["EN EL HIELO", Y, 4.9, 5.42], ["Y NADIE DICE|ABSOLUTAMENTE NADA", WY, 5.42, 7.4]]),
 "g3": ("Short03_AsesinoEnSerie", 7.85,
   [cr(269.0, 1.2, 900), cr(271.0, 1.2, 900), st(274.6, 1.4, x=650), gfit(276.0, 1.45), gfit(283.0, 1.9), st(284.2, 0.7, x=500, y=200, w=450, h=800)],
   [["SABIAS QUE", W, 0, 0.56], ["GTA 5", Y, 0.56, 1.22], ["ESCONDE PISTAS", W, 1.22, 1.98], ["DE UN ASESINO|EN SERIE", WY, 1.98, 2.82],
    ["POR TODO|EL MAPA", W, 2.82, 3.7], ["SI LAS SIGUES|TODAS", W, 3.92, 4.64], ["ENCUENTRAS DONDE|ESCONDIO LOS CUERPOS", WY, 4.64, 6.6],
    ["BAJO EL AGUA", Y, 6.8, 7.75]]),
 "g4": ("Short04_PandillaGrove", 7.7,
   [cr(63.5, 1.6, 400), cr(65.2, 1.55, 360), box(71.0, 1.3, 1200, 100, 480, 850), box(72.3, 1.3, 1200, 100, 480, 850),
    box(74.0, 1.95, 700, 0, 520, 920)],
   [["SABIAS QUE", W, 0, 0.52], ["EN GTA 5", W, 0.52, 1.34], ["CUANDO FRANKLIN|VA A GROVE STREET", WY, 1.5, 2.9],
    ["PASAN EN BICI", W, 3.0, 3.62], ["TRES TIPOS|IDENTICOS", W, 3.62, 4.56], ["A CJ", Y, 4.56, 5.1],
    ["BIG SMOKE|Y SWEET", Y, 5.2, 6.4], ["LA PANDILLA|SIGUE AHI", WY, 6.5, 7.6]]),
 "g5": ("Short05_MichaelMaxPayne", 7.55,
   [cr(100.0, 1.5, 560), cr(102.5, 1.6, 700), cr(105.0, 1.5, 700), cr(107.0, 0.9, 560), cr(108.2, 1.0, 660), box(111.6, 1.05, 1020, 0, 600, 1065)],
   [["SABIAS QUE", W, 0, 0.54], ["EN GTA 5", W, 0.54, 1.32], ["SI A MICHAEL|LE PONES", W, 1.52, 2.4], ["CAMISA|HAWAIANA", WY, 2.4, 3.4],
    ["LA CABEZA|RAPADA", W, 3.54, 4.45], ["BARBA Y LENTES|DE AVIADOR", W, 4.6, 5.7], ["SE CONVIERTE|EN MAX PAYNE", WY, 5.88, 7.45]]),
}
for k, (name, body, clips, caps) in S.items():
    src, tpl, folder, vd = GAMES[k[0]]
    vdir = P + rf"\{folder}\{vd}"
    d = dict(src=src, intro=P + rf"\Plantillas\Intro_{tpl}.mp4", cats=P + rf"\Plantillas\Gatitos_{tpl}.mp4", font=FONT,
             voice=vdir + rf"\{k}_f.mp3", out=P + rf"\{folder}\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(vdir + rf"\{k}.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
