"""Specs del lote Mario clasico NES (fuente: Nintenduo World '25 SECRETS of SUPER MARIO BROS', 5I9gkzpffh8, 1080p60, en ingles).
Correr desde MarioClasico/voces (n01..n04.mp3). Tiempos verificados con frames exactos (hoja2)."""
import json, os
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
base = dict(src=P + r"\Descargas\5I9gkzpffh8\fuente.mp4", intro=P + r"\Plantillas\Intro_Mario.mp4",
            cats=P + r"\Plantillas\Gatitos_Mario.mp4", font=P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf")
W, Y = "white", "yellow"
fit = lambda t, d: dict(t=t, d=d, mode="fit")
still = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="still", x=x, y=y, w=w, h=h)
S = {
 "n01": ("Short01_BowserFalsos", 7.92, [fit(121.0, 1.4), fit(124.0, 1.4), fit(128.0, 1.5), fit(130.0, 0.9), fit(131.0, 2.72)],
   [["SABIAS QUE", W, 0, 0.48], ["LOS BOWSER|DE LOS CASTILLOS", "yellow,white", 0.48, 2.20], ["DE MARIO BROS", W, 2.20, 2.80],
    ["SON FALSOS", Y, 2.80, 4.00], ["SI LOS MATAS|CON FUEGO", "white,yellow", 4.00, 4.98], ["DESCUBRES QUE", W, 4.98, 5.80],
    ["DEBAJO DEL|DISFRAZ", W, 5.80, 6.72], ["HAY UN|GOOMBA", "white,yellow", 6.72, 7.92]]),
 "n02": ("Short02_LadrillosToad", 7.85, [fit(165.5, 1.6), fit(171.0, 2.2), fit(174.0, 1.0), fit(175.0, 1.5), fit(184.0, 1.55)],
   [["SABIAS QUE", W, 0, 0.46], ["SEGUN EL|MANUAL", "white,yellow", 0.46, 1.18], ["DE MARIO BROS", W, 1.18, 1.98],
    ["BOWSER|CONVIRTIO", W, 1.98, 2.96], ["A LA GENTE|HONGO", "white,yellow", 2.96, 3.80], ["EN LOS|LADRILLOS", "white,yellow", 3.80, 4.48],
    ["QUE MARIO|ROMPE", W, 4.48, 5.14], ["A CABEZAZOS", Y, 5.14, 5.90], ["O SEA MARIO|ES UN ASESINO", "white,yellow", 5.90, 7.85]]),
 "n03": ("Short03_NubesArbustos", 7.82, [fit(314.0, 1.5), fit(318.0, 1.4), fit(324.0, 1.0), fit(325.0, 2.5), fit(321.0, 1.42)],
   [["SABIAS QUE", W, 0, 0.50], ["EN MARIO BROS", W, 0.50, 1.46], ["LAS NUBES Y|LOS ARBUSTOS", "white,yellow", 1.46, 2.56],
    ["SON EL MISMO|DIBUJO", "white,yellow", 2.56, 3.70], ["SOLO LES|CAMBIARON", W, 3.70, 4.42], ["EL COLOR", Y, 4.42, 5.18],
    ["PORQUE EN EL|CARTUCHO", W, 5.18, 6.04], ["NO ENTRABA|NADA MAS", "white,yellow", 6.04, 7.82]]),
 "n04": ("Short04_MundoMenosUno", 7.65, [fit(365.0, 1.2), still(361.0, 1.3, 680, 90, 506, 900), fit(367.0, 1.25), fit(372.0, 2.0),
    fit(374.0, 1.9)],
   [["SABIAS QUE", W, 0, 0.48], ["EN MARIO BROS|EXISTE", W, 0.48, 1.44], ["EL MUNDO -1", Y, 1.44, 2.74],
    ["UN NIVEL|SECRETO", "white,yellow", 2.74, 3.58], ["BAJO EL AGUA", W, 3.58, 4.24], ["DEL QUE ES|IMPOSIBLE SALIR", "white,yellow", 4.24, 5.70],
    ["MARIO NADA AHI", W, 5.70, 6.54], ["PARA SIEMPRE", Y, 6.54, 7.65]]),
}
os.makedirs(P + r"\MarioClasico", exist_ok=True)
for k, (name, body, clips, caps) in S.items():
    d = dict(base, voice=os.path.join(os.getcwd(), k + ".mp3"), out=P + rf"\MarioClasico\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(k + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
