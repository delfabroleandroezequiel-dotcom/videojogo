"""Specs del lote Mario Wonder (fuente: Nintenduo World '25 SECRETS in SUPER MARIO BROS WONDER', IuiFRG3nY_w, 1080p60, en ingles).
Correr desde MarioWonder/voces (w01..w03.mp3): genera w01..w03.json ahi."""
import json, os
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
base = dict(src=P + r"\Descargas\IuiFRG3nY_w\fuente.mp4", intro=P + r"\Plantillas\Intro_Mario.mp4",
            cats=P + r"\Plantillas\Gatitos_Mario.mp4", font=P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf")
W, Y = "white", "yellow"
fit = lambda t, d: dict(t=t, d=d, mode="fit")
crop = lambda t, d, x=656: dict(t=t, d=d, mode="crop", x=x)
box = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="box", x=x, y=y, w=w, h=h)
still = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="still", x=x, y=y, w=w, h=h)
S = {
 "w01": ("Short01_DaisyHijas", 7.0, [fit(446.0, 1.6), fit(451.0, 1.9), fit(455.0, 1.5), fit(447.6, 2.0)],
   [["SABIAS QUE", W, 0, 0.54], ["DAISY", Y, 0.54, 1.04], ["ESTA EN|MARIO WONDER", W, 1.04, 1.66],
    ["PORQUE|LAS HIJAS", W, 1.66, 2.42], ["DEL DIRECTOR", W, 2.42, 2.92], ["SE PELEABAN", Y, 2.92, 3.84],
    ["POR JUGAR|CON PEACH", "white,yellow", 3.84, 4.72], ["ASI QUE|PAPA", W, 4.72, 5.40],
    ["METIO OTRA|PRINCESA", "white,yellow", 5.40, 7.0]]),
 "w02": ("Short02_HongoAlucinogeno", 7.9, [fit(479.6, 1.1), crop(468.3, 1.7), crop(482.0, 2.0),
    crop(499.2, 1.0, 620), crop(502.0, 1.3, 620), crop(470.0, 0.8)],
   [["SABIAS QUE", W, 0, 0.56], ["MARIO WONDER", W, 0.56, 1.06], ["IBA A TENER|UN HONGO", "white,yellow", 1.06, 1.98],
    ["ALUCINOGENO", Y, 1.98, 2.84], ["PERO PARA QUE|NO LOS ACUSARAN", W, 2.84, 3.92],
    ["DE PROMOVER|DROGAS", "white,yellow", 3.92, 4.96], ["NINTENDO|LO CAMBIO", W, 4.96, 5.70],
    ["POR UNA FLOR", Y, 5.70, 6.20], ["QUE TAMBIEN|TE HACE", W, 6.20, 7.06], ["ALUCINAR", Y, 7.06, 7.9]]),
 "w03": ("Short03_NivelLastima", 7.6, [fit(128.8, 1.5), fit(132.0, 1.4), fit(134.0, 1.0), fit(145.6, 1.2),
    box(141.0, 2.5, 950, 200, 500, 880)],
   [["SABIAS QUE", W, 0, 0.5], ["EN MARIO|WONDER", W, 0.5, 1.14], ["HAY UN NIVEL|SECRETO", "white,yellow", 1.14, 2.18],
    ["LLENO DE|MONEDAS", "white,yellow", 2.18, 2.88], ["QUE SOLO|APARECE", W, 2.88, 3.50],
    ["SI PIERDES|TODAS TUS VIDAS", "white,yellow", 3.50, 5.14], ["Y ENCIMA|TE REGALAN", W, 5.14, 6.04],
    ["5 VIDAS", Y, 6.04, 6.64], ["POR LASTIMA", Y, 6.64, 7.6]]),
 "w04": ("Short04_FloresVictimas", 7.5, [still(82.6, 1.3, 250, 300, 550, 978), fit(63.0, 1.4), fit(72.4, 1.3), fit(74.9, 0.85), fit(87.4, 1.6),
    still(88.2, 1.05, 560, 0, 608, 1080)],
   [["SABIAS QUE", W, 0, 0.52], ["SI DESACTIVAS", W, 0.52, 1.16], ["LAS FLORES|QUE HABLAN", "white,yellow", 1.16, 2.02],
    ["EN MARIO|WONDER", W, 2.02, 2.60], ["SE HACEN LAS|VICTIMAS", "white,yellow", 2.60, 3.88], ["Y TE DICEN", W, 3.88, 4.68],
    ["FUE ALGO|QUE DIJE?", Y, 4.68, 5.32], ["SOLO PARA QUE|TE SIENTAS", W, 5.32, 6.64], ["CULPABLE", Y, 6.64, 7.5]]),
 "w05": ("Short05_MarioRealista", 7.5, [fit(252.5, 1.0), fit(242.2, 1.8), crop(245.0, 0.95, 900), fit(254.0, 1.9),
    crop(256.0, 0.95, 750), fit(257.1, 0.9)],
   [["SABIAS QUE", W, 0, 0.52], ["MARIO WONDER", W, 0.52, 1.02], ["CASI TIENE|UN MARIO", W, 1.02, 1.82],
    ["REALISTA", Y, 1.82, 2.40], ["DE TAMAÑO|HUMANO", "white,yellow", 2.40, 3.02], ["QUE TARAREABA|LA MUSICA", W, 3.02, 4.08],
    ["MIENTRAS|SALTABA", W, 4.08, 5.34], ["POR SUERTE", Y, 5.34, 5.88], ["NINTENDO|LO DESCARTO", "white,yellow", 5.88, 7.5]]),
 "w06": ("Short06_GoombaMuerde", 7.45, [fit(385.0, 0.95), fit(389.0, 1.0), fit(392.0, 1.5), fit(394.0, 1.9), fit(396.2, 2.1)],
   [["SABIAS QUE", W, 0, 0.5], ["EN CASI|40 AÑOS", "white,yellow", 0.5, 1.56], ["NUNCA VIMOS", W, 1.56, 2.32],
    ["COMO ATACA|UN GOOMBA", "white,yellow", 2.32, 3.44], ["HASTA|MARIO WONDER", W, 3.44, 4.36], ["DONDE|POR FIN", W, 4.36, 5.2],
    ["TE MUERDEN", Y, 5.2, 5.72], ["A MENOS QUE|ESTEN DORMIDOS", "white,yellow", 5.72, 7.45]]),
 "w07": ("Short07_YoshiSandia", 7.7, [fit(341.0, 1.4), fit(342.4, 1.5), fit(350.5, 1.4), still(351.3, 1.0, 500, 200, 500, 880),
    fit(346.0, 2.4)],
   [["SABIAS QUE", W, 0, 0.52], ["EN MARIO|WONDER", W, 0.52, 1.44], ["SI YOSHI|SE COME", "white,yellow", 1.44, 2.12],
    ["UNA PLANTA|PIRAÑA SANDIA", "white,yellow", 2.12, 3.56], ["EMPIEZA A|ESCUPIR", W, 3.56, 4.36], ["SEMILLAS", Y, 4.36, 4.98],
    ["COMO|AMETRALLADORA", "white,yellow", 4.98, 6.04], ["IGUAL QUE EN|YOSHIS ISLAND", "white,yellow", 6.04, 7.7]]),
}
os.makedirs(P + r"\MarioWonder", exist_ok=True)
for k, (name, body, clips, caps) in S.items():
    d = dict(base, voice=os.path.join(os.getcwd(), k + ".mp3"), out=P + rf"\MarioWonder\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(k + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
