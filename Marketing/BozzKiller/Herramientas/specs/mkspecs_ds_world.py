"""Specs: Dark Souls (fuente STAR RUN 'odd and obscure things in dark souls', 916iheNY3rE, 1080p30)
y Super Mario World (fuente Jump Of Time '30 AMAZING Secrets... Super Mario World', WhPwDlickWM, 1080p60).
Correr desde cualquier carpeta: usa rutas absolutas. Tiempos verificados con frames exactos (hoja2)."""
import json, os
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
FONT = P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf"
W, Y = "white", "yellow"
fit = lambda t, d: dict(t=t, d=d, mode="fit")
still = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="still", x=x, y=y, w=w, h=h)
LOTES = {
 "DarkSouls": (dict(src=P + r"\Descargas\916iheNY3rE\fuente.mp4", intro=P + r"\Plantillas\Intro_DarkSouls.mp4",
                    cats=P + r"\Plantillas\Gatitos_DarkSouls.mp4", font=FONT), {
  "d01": ("Short01_BansheeBebes", 7.45, [fit(240.0, 1.6), fit(242.0, 1.3), fit(247.5, 1.4), fit(249.0, 1.6),
     still(249.5, 1.55, 560, 0, 560, 996)],
    [["SABIAS QUE", W, 0, 0.50], ["EN LAS RUINAS|DE NUEVO LONDO", W, 0.50, 1.64], ["HAY FANTASMAS", Y, 1.64, 2.64],
     ["QUE CARGAN|BEBES EN BRAZOS", "white,yellow", 2.64, 3.76], ["Y TE ATACAN", W, 3.76, 4.44],
     ["DISPARANDOTE|RAYOS", "white,yellow", 4.44, 5.30], ["CON LOS BEBES", Y, 5.30, 6.10], ["SI, CON|LOS BEBES", "white,yellow", 6.10, 7.45]]),
  "d02": ("Short02_MariposaSeCura", 7.75, [fit(521.0, 1.3), fit(524.0, 1.0), fit(527.0, 1.9), fit(529.0, 1.6), fit(531.0, 1.95)],
    [["SABIAS QUE", W, 0, 0.50], ["LA MARIPOSA|LUNAR", "white,yellow", 0.50, 1.42], ["DE DARK SOULS", W, 1.42, 2.06],
     ["SE CURA", Y, 2.06, 2.76], ["CADA VEZ QUE|SE POSA", W, 2.76, 3.70], ["EN EL PUENTE", Y, 3.70, 4.44],
     ["CASI NADIE|LO NOTA", W, 4.44, 5.50], ["PORQUE TIENE|TAN POCA VIDA", W, 5.50, 6.50], ["QUE MUERE|ANTES", "white,yellow", 6.50, 7.75]]),
  "d03": ("Short03_NieblaSeath", 7.6, [fit(375.0, 1.5), fit(379.5, 1.5), fit(385.5, 1.5), fit(388.0, 1.5), fit(398.2, 1.6)],
    [["SABIAS QUE", W, 0, 0.50], ["LA NIEBLA|DE SEATH", "white,yellow", 0.50, 1.30], ["ES LA UNICA|DE DARK SOULS", W, 1.30, 2.54],
     ["DE LA QUE|PUEDES SALIR", "white,yellow", 2.54, 3.58], ["DANDOTE|LA VUELTA", W, 3.58, 4.72], ["PERO DA IGUAL", W, 4.72, 5.48],
     ["ESA PELEA|ESTA HECHA", W, 5.48, 6.52], ["PARA QUE|MUERAS", "white,yellow", 6.52, 7.6]]),
 }),
 "MarioWorld": (dict(src=P + r"\Descargas\WhPwDlickWM\fuente.mp4", intro=P + r"\Plantillas\Intro_Mario.mp4",
                     cats=P + r"\Plantillas\Gatitos_Mario.mp4", font=FONT), {
  "m01": ("Short01_BooPompis", 7.0, [fit(146.0, 1.4), fit(148.0, 1.3), fit(150.0, 1.3), fit(152.5, 1.5), fit(157.0, 1.5)],
    [["SABIAS QUE", W, 0, 0.50], ["EN SUPER|MARIO WORLD", W, 0.50, 1.36], ["PUEDES MATAR", W, 1.36, 1.98],
     ["A ENEMIGOS|INVENCIBLES", "white,yellow", 1.98, 2.98], ["COMO EL|GRAN BOO", "white,yellow", 2.98, 4.00],
     ["SOLO|DESLIZANDOTE", W, 4.00, 4.88], ["CUESTA ABAJO", W, 4.88, 5.50], ["CON LAS|POMPIS", "white,yellow", 5.50, 7.0]]),
  "m02": ("Short02_BooTeMira", 6.85, [fit(462.0, 1.6), fit(463.6, 1.4), fit(467.0, 1.6), fit(469.0, 0.95), fit(470.0, 1.3)],
    [["SABIAS QUE", W, 0, 0.50], ["SI TE QUEDAS|MIRANDO", W, 0.50, 1.40], ["AL GRAN BOO", Y, 1.40, 1.92],
     ["DE MARIO WORLD", W, 1.92, 2.46], ["DURANTE|25 SEGUNDOS", "white,yellow", 2.46, 4.08], ["SE DESTAPA|LA CARA", "white,yellow", 4.08, 5.06],
     ["Y TE MIRA|FIJO", "white,yellow", 5.06, 6.85]]),
  "m03": ("Short03_YoshiEnNES", 7.65, [fit(111.0, 1.3), fit(104.0, 1.4), fit(114.0, 1.4), fit(119.0, 1.5), fit(133.0, 2.05)],
    [["SABIAS QUE", W, 0, 0.48], ["MIYAMOTO|QUERIA", "yellow,white", 0.48, 1.32], ["METER A YOSHI", Y, 1.32, 1.90],
     ["DESDE EL PRIMER|MARIO BROS", W, 1.90, 3.34], ["PERO LA NES|NO AGUANTABA", W, 3.34, 4.68], ["UN DINOSAURIO", Y, 4.68, 5.52],
     ["ASI QUE TUVO|QUE ESPERAR", W, 5.52, 6.38], ["A MARIO WORLD", Y, 6.38, 7.65]]),
 }),
}
for carpeta, (base, S) in LOTES.items():
    vd = P + rf"\{carpeta}\voces"
    for k, (name, body, clips, caps) in S.items():
        d = dict(base, voice=vd + rf"\{k}.mp3", out=P + rf"\{carpeta}\{name}.mp4", body=body, clips=clips, caps=caps)
        json.dump(d, open(vd + rf"\{k}.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
