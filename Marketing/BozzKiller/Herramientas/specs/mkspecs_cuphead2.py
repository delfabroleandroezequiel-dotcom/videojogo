"""Specs: Cuphead lote 2 (fuente JustoAllDay '18 Random Cuphead Facts', r72F3rf-UDA, 1080p30, en ingles).
La fuente tiene subtitulos quemados abajo (y>~900) y el numero de dato arriba a la derecha (x>~1590): los recortes 'fit'
usan y=0..880 y x<=1560. Correr desde cualquier carpeta (rutas absolutas)."""
import json
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
FONT = P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf"
W, Y = "white", "yellow"
f = lambda t, d, **k: dict(dict(t=t, d=d, mode="fit", x=60, w=1500, y=0, h=880), **k)
box = lambda t, d, x, y, w, h: dict(t=t, d=d, mode="box", x=x, y=y, w=w, h=h)
base = dict(src=P + r"\Descargas\r72F3rf-UDA\fuente.mp4", intro=P + r"\Plantillas\Intro_Cuphead.mp4",
            cats=P + r"\Plantillas\Gatitos_Cuphead.mp4", font=FONT)
S = {
 "c01b": ("Short07_PezCadaver", 7.3, [f(501.0, 1.2), f(519.0, 2.0), f(523.6, 2.4), f(526.4, 1.7)],
   [["SABIAS QUE", W, 0, 0.50], ["EN CUPHEAD", W, 0.50, 1.10], ["SI MATAS AL|PEZ GLOBO", "white,yellow", 1.20, 2.30],
    ["DE CALA MARIA", W, 2.30, 3.30], ["PUEDES HACERLE|PARRY", "white,yellow", 3.40, 4.40], ["A SU CADAVER", Y, 4.40, 5.80],
    ["MORBOSO", W, 5.90, 6.55], ["PERO FUNCIONA", Y, 6.55, 7.3]]),
 "c02a": ("Short08_PepitasOro", 7.35, [f(584.0, 1.5), f(588.0, 1.8), f(590.6, 1.3), f(594.2, 1.6), f(595.8, 1.15)],
   [["SABIAS QUE", W, 0, 0.48], ["LOS GNOMOS|DE GLUMSTONE", "white,yellow", 0.48, 1.44], ["NO TE DISPARAN", W, 1.44, 2.32],
    ["BOLAS DE|ENERGIA", W, 2.32, 3.30], ["SINO PEPITAS|DE ORO", "white,yellow", 3.36, 4.50], ["EN LA INTRO", W, 4.60, 5.10],
    ["LOS VES|MINANDO", W, 5.10, 6.00], ["TE ATACAN CON|SU SUELDO", "white,yellow", 6.05, 7.35]]),
 "c03a": ("Short09_PajaritosMueren", 6.9, [f(443.0, 1.2), f(455.0, 2.3), f(449.5, 1.8),
    f(463.0, 1.6, x=1195, w=660, y=560, h=340)],
   [["SABIAS QUE", W, 0, 0.56], ["EN CUPHEAD", W, 0.56, 1.20], ["SI NO LE|DISPARAS", W, 1.30, 2.10],
    ["A LOS PAJARITOS|DE WALLY WARBLES", "white,yellow", 2.10, 3.60], ["IGUAL SE|MUEREN", "white,yellow", 3.80, 4.40],
    ["FUERA DE|PANTALLA", W, 4.40, 5.30], ["SE ESCUCHA|COMO MUEREN", "white,yellow", 5.45, 6.9]]),
 "c05a": ("Short10_ChauAbeja", 6.75, [f(529.2, 1.4), f(531.0, 1.7), f(543.0, 1.9), f(556.0, 0.7), f(557.0, 1.05)],
   [["SABIAS QUE", W, 0, 0.50], ["PUEDES SALTARTE|LA ULTIMA FASE", "white,yellow", 0.50, 2.00],
    ["DE RUMOR|HONEYBOTTOMS", W, 2.00, 3.10], ["SI LE HACES|SUFICIENTE DAÑO", "white,yellow", 3.20, 4.50],
    ["MIENTRAS SE|TRANSFORMA", W, 4.50, 5.65], ["CHAU ABEJA", Y, 5.70, 6.75]]),
 "c06a": ("Short11_TresChalice", 7.1, [f(348.4, 1.4, x=340, w=1240, h=700), f(350.0, 1.5, x=340, w=1240, h=700),
    f(353.0, 2.8, x=280, w=1310, y=100, h=740), box(357.0, 1.4, 760, 110, 405, 720)],
   [["SABIAS QUE", W, 0, 0.50], ["SI ACEPTAS EL|TRATO DEL DIABLO", "white,yellow", 0.50, 1.80],
    ["CON MS CHALICE", W, 1.80, 2.80], ["ELLA APARECE|EN EL FINAL MALVADO", "white,yellow", 2.88, 4.35],
    ["JUNTO A CUPHEAD|Y MUGMAN", W, 4.35, 5.85], ["COMO EXISTEN|LOS TRES?", "white,yellow", 5.95, 7.1]]),
}
for k, (name, body, clips, caps) in S.items():
    vd = P + r"\Cuphead\voces2"
    d = dict(base, voice=vd + rf"\{k}.mp3", out=P + rf"\Cuphead\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(vd + rf"\{k}.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
