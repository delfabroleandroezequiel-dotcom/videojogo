"""Miniatura del Patricio v2: misma fórmula que el Chavo v2 (fondo de la escena desenfocado,
antes en gris -> flecha verde -> después a color, recortes con borde blanco).
Los recortes de remove.bg vienen en 666x375: se usa su alfa, escalado, como máscara sobre
los fotogramas originales en 1920x1080 (genio.png, amigos.png) para no perder nitidez.
Uso: py -3.12 compose_patricio.py  (desde esta carpeta)"""
import importlib.util
import os

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location(
    "chavo", os.path.join(HERE, "..", "..", "Chavo", "Miniatura", "v2", "herramientas", "compose_chavo.py"))
src = open(spec.origin, encoding="utf-8").read().split("R = V2 +")[0]  # solo las funciones
ns = {"__name__": "compose_lib", "sys": __import__("sys")}
ns["sys"].argv = ["x", HERE]
exec(src, ns)


def full_res_cutout(frame, cut, out, erase=None):
    fr = Image.open(os.path.join(HERE, frame)).convert("RGB")
    a = Image.open(os.path.join(HERE, cut)).getchannel("A").resize(fr.size, Image.LANCZOS)
    a = a.filter(ImageFilter.GaussianBlur(0.8)).point(lambda v: 255 if v > 110 else 0)
    if erase:  # rectángulo (x0, y0, x1, y1) en 1920x1080 que remove.bg dejó pegado (la flor del cielo)
        a = a.copy(); a.paste(0, erase)
    # remove.bg a veces deja flores del cielo sueltas: quedarse con las piezas grandes
    from scipy import ndimage as ndi
    m = np.asarray(a) > 0
    lab, n = ndi.label(m)
    if n > 1:
        sizes = ndi.sum(m, lab, range(1, n + 1))
        keep = [i + 1 for i, sz in enumerate(sizes) if sz >= 0.08 * sizes.max()]
        a = Image.fromarray((np.isin(lab, keep) * 255).astype(np.uint8))
    if erase:  # rectángulo (x0, y0, x1, y1) en 1920x1080 que remove.bg dejó pegado (la flor del cielo)
        a = a.copy(); a.paste(0, erase)
    co = fr.convert("RGBA")
    co.putalpha(a)
    co.save(os.path.join(HERE, out))
    return os.path.join(HERE, out)


genio_p = full_res_cutout("genio.png", "genio-removebg-preview.png", "recorte_genio.png")
amigos_p = full_res_cutout("amigos.png", "amigos-removebg-preview.png", "recorte_amigos.png", erase=(1000, 0, 1920, 385))
genio = ns["gray"](ns["outlined"](genio_p, scale=1.0, outline=12, dark=4))
amigos = ns["pop"](ns["outlined"](amigos_p, scale=1.0, outline=12, dark=4))

for name, bgframe, focus in (("A", "fondo_coral.png", (0.5, 0.6)), ("B", "fondo_coral.png", (0.2, 0.6))):
    c = ns["bg_scene"](os.path.join(HERE, bgframe), focus=focus, zoom=1.1).convert("RGBA")
    ns["place"](c, genio, height=700, x=-60)
    ns["place"](c, amigos, height=610, x=650, bottom=720)
    ns["arrow"](c, (430, 300), (560, 120), (720, 200))
    c.convert("RGB").save(os.path.join(HERE, f"Miniatura_Patricio_v2_{name}.png"))
    c.convert("RGB").save(os.path.join(HERE, f"Miniatura_Patricio_v2_{name}.jpg"), quality=92)
print("ok")
