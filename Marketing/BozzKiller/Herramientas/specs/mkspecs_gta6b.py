"""Specs: GTA 6 lote 2 (2026-11-25). Fuente en ingles: T5G "30 Secrets & Easter Eggs In GTA 6!" V9JgeA64VYE (1080p30,
gameplay del juego ya salido; sin marca de canal, minimapa abajo-izq, plata arriba-der). Subtitulos de YouTube dieron 429:
transcripcion con faster-whisper small en Descargas/V9JgeA64VYE/fuente_tiempos.txt. Salida en GTA/Short08-12."""
import json
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
FONT = P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf"
W, Y, WY = "white", "yellow", "white,yellow"
cr = lambda t, d, x=656: dict(t=t, d=d, mode="crop", x=x)
fit = lambda t, d, **k: dict(dict(t=t, d=d, mode="fit"), **k)
st = lambda t, d, x=656, y=0, w=608, h=1080, z=0.12: dict(t=t, d=d, mode="still", x=x, y=y, w=w, h=h, zoom=z)
base = dict(src=P + r"\Descargas\V9JgeA64VYE\fuente.mp4", intro=P + r"\Plantillas\Intro_GTA.mp4",
            cats=P + r"\Plantillas\Gatitos_GTA.mp4", font=FONT)
S = {
 "h1": ("Short08_HuevoAntena", 7.3,
   [cr(12.0, 1.4, 450), cr(16.5, 1.0), cr(18.0, 0.9), cr(19.4, 1.0), cr(23.0, 1.3, 1000), fit(26.0, 1.0), cr(31.3, 0.7, 900)],
   [["SABIAS QUE", W, 0, 0.52], ["EN GTA 6", W, 0.52, 1.36], ["PUEDES SUBIR A UNA|ANTENA GIGANTE", WY, 1.36, 3.18],
    ["TARDAS|6 MINUTOS", W, 3.3, 4.14], ["Y ARRIBA|EN UN NIDO", W, 4.14, 5.64], ["HAY UN HUEVO DE|PASCUA DE VERDAD", WY, 5.64, 7.2]]),
 "h2": ("Short09_BarcoMichael", 8.25,
   [cr(47.5, 1.5, 500), fit(49.2, 1.5), cr(53.0, 1.5, 400), fit(56.0, 1.0), cr(64.0, 1.2, 600), fit(68.6, 1.55)],
   [["SABIAS QUE", W, 0, 0.54], ["EN GTA 6", W, 0.54, 1.38], ["FRENTE A LA|CASA DE JASON", W, 1.5, 2.8], ["ESTA EL BARCO", W, 2.9, 3.54],
    ["QUE LE ROBARON|A MICHAEL", WY, 3.54, 4.46], ["EN GTA 5", Y, 4.46, 5.46], ["Y SU GPS|TODAVIA MUESTRA", W, 5.46, 7.36],
    ["LOS SANTOS", Y, 7.36, 8.2]]),
 "h3": ("Short10_DepartamentoDexter", 7.7,
   [cr(95.5, 1.3, 470), cr(100.6, 1.5, 480), cr(102.6, 1.2, 450), cr(105.6, 1.6, 600), cr(108.0, 1.0, 700), cr(109.0, 1.1, 650)],
   [["SABIAS QUE", W, 0, 0.52], ["EN GTA 6", W, 0.52, 1.36], ["PUEDES ENCONTRAR|EL DEPARTAMENTO", W, 1.36, 2.98], ["DE DEXTER", Y, 2.98, 3.7],
    ["EN EL TECHO", W, 3.8, 4.3], ["ESTAN SU|CUCHILLO", WY, 4.3, 5.1], ["SUS CUERDAS Y|SU PLASTICO", W, 5.2, 6.6], ["EL KIT COMPLETO", Y, 6.7, 7.6]]),
 "h4": ("Short11_SkunkApe", 7.95,
   [cr(241.0, 1.4), fit(252.0, 1.5), cr(254.6, 1.5, 450), cr(256.2, 1.3, 400), cr(263.0, 1.3, 450), cr(265.5, 0.95, 500)],
   [["SABIAS QUE", W, 0, 0.56], ["EN GTA 6", W, 0.56, 1.42], ["EXISTE EL|SKUNK APE", WY, 1.42, 2.95], ["EL PIE GRANDE|DE FLORIDA", WY, 3.0, 4.2],
    ["CAZA ANIMALES|EN EL PANTANO", W, 4.5, 6.1], ["Y HAY CARTELES|QUE TE ADVIERTEN", W, 6.2, 7.85]]),
 "h5": ("Short12_PinaBobEsponja", 7.12,
   [cr(314.5, 1.3, 1000), cr(315.8, 1.3, 1000), st(319.8, 1.6, x=1150), cr(317.1, 1.2, 950), cr(318.3, 1.0, 1000), st(318.6, 0.72, x=1000, z=0.15)],
   [["SABIAS QUE", W, 0, 0.54], ["EN GTA 6", W, 0.54, 1.42], ["HAY UNA PARADA|DE AUTOBUS", W, 1.42, 2.62],
    ["IGUALITA A LA CASA|DE BOB ESPONJA", WY, 2.62, 4.2], ["UNA PIÑA|GIGANTE", WY, 4.3, 5.24], ["EN PLENA|VICE CITY", W, 5.24, 7.0]]),
}
for k, (name, body, clips, caps) in S.items():
    vd = P + r"\GTA6\voces"
    d = dict(base, voice=vd + rf"\{k}.mp3", out=P + rf"\GTA\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(vd + rf"\{k}.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
