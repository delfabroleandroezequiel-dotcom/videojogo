"""Miniatura del Chavo v2: misma fórmula que Simpson/Miniatura/v2 (fondo de la escena
desenfocado, antes en gris -> flecha verde -> después a color, recortes con borde blanco
estilo Alan, píldora amarilla corta). Uso: py -3.12 compose_chavo.py <dir v2>"""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageOps, ImageFont
from scipy import ndimage as ndi

V2 = sys.argv[1]
CUADROS = V2 + "/../cuadros"
W, H = 1280, 720


def vignette(im, strength=0.55):
    yy, xx = np.mgrid[0:H, 0:W]
    r = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
    v = 1 - strength * np.clip(r - 0.55, 0, 1) ** 1.6
    a = np.asarray(im).astype(np.float32) * v[..., None]
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def bg_scene(path, blur=7, dark=0.62, sat=1.15, focus=(0.5, 0.5), zoom=1.0):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    s = max(W / w, H / h) * zoom
    im = im.resize((round(w * s), round(h * s)), Image.LANCZOS)
    x = int((im.width - W) * focus[0]); y = int((im.height - H) * focus[1])
    im = im.crop((x, y, x + W, y + H)).filter(ImageFilter.GaussianBlur(blur))
    im = ImageEnhance.Color(im).enhance(sat)
    im = ImageEnhance.Brightness(im).enhance(dark)
    return vignette(im)


def outlined(path, crop=None, scale=2.0, outline=10, dark=3):
    """Recorte de remove.bg -> escalado, alfa limpio, borde blanco + filo oscuro."""
    co = Image.open(path).convert("RGBA")
    if crop: co = co.crop(crop)
    co = co.crop(co.getchannel("A").getbbox())
    co = co.resize((round(co.width * scale), round(co.height * scale)), Image.LANCZOS)
    co = co.filter(ImageFilter.UnsharpMask(radius=2, percent=90, threshold=2))
    pad = outline + dark + 4
    big = Image.new("RGBA", (co.width + 2 * pad, co.height + 2 * pad), (0, 0, 0, 0))
    big.paste(co, (pad, pad))
    rgb = np.asarray(big.convert("RGB")).copy()
    a = ndi.gaussian_filter(np.asarray(big.getchannel("A")).astype(np.float32), 1.0) > 128
    a = ndi.binary_fill_holes(ndi.binary_opening(a, iterations=2))
    dist = ndi.distance_transform_edt(~a)
    out = np.zeros(rgb.shape[:2] + (4,), np.uint8)
    out[(dist > outline) & (dist <= outline + dark)] = (20, 16, 18, 255)
    out[dist <= outline] = (255, 255, 255, 255)
    out[a, :3] = rgb[a]; out[a, 3] = 255
    out[..., 3] = ndi.gaussian_filter(out[..., 3].astype(np.float32), 0.8).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def gray(co, contrast=1.15):
    a = co.getchannel("A")
    l = ImageEnhance.Contrast(ImageOps.grayscale(co.convert("RGB"))).enhance(contrast)
    return Image.merge("RGBA", (l, l, l, a))


def pop(co, sat=1.25, con=1.08):
    a = co.getchannel("A")
    rgb = ImageEnhance.Contrast(ImageEnhance.Color(co.convert("RGB")).enhance(sat)).enhance(con)
    return Image.merge("RGBA", (*rgb.split(), a))


def place(canvas, co, height=None, width=None, x=0, bottom=H, shadow=True):
    co = co.crop(co.getbbox())
    s = (height / co.height) if height else (width / co.width)
    co = co.resize((round(co.width * s), round(co.height * s)), Image.LANCZOS)
    y = bottom - co.height
    if shadow:
        a = co.getchannel("A").filter(ImageFilter.GaussianBlur(14))
        sh = Image.new("RGBA", co.size, (0, 0, 0, 0)); sh.putalpha(a.point(lambda v: int(v * 0.55)))
        canvas.alpha_composite(sh, (int(x) + 10, int(y) + 12))
    canvas.alpha_composite(co, (int(x), int(y)))


def arrow(canvas, p0, p1, p2, color=(70, 225, 60), w=30, head=78):
    d = ImageDraw.Draw(canvas)
    pts = []
    for i in range(61):
        t = i / 60
        pts.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    end = pts[-1]; prev = pts[-6]
    ang = math.atan2(end[1] - prev[1], end[0] - prev[0])
    body = pts[:-8]

    def tri(sz):
        tip = (end[0] + math.cos(ang) * sz * 0.25, end[1] + math.sin(ang) * sz * 0.25)
        b = (end[0] - math.cos(ang) * sz * 0.75, end[1] - math.sin(ang) * sz * 0.75)
        n = (-math.sin(ang), math.cos(ang))
        return [tip, (b[0] + n[0] * sz * 0.6, b[1] + n[1] * sz * 0.6), (b[0] - n[0] * sz * 0.6, b[1] - n[1] * sz * 0.6)]
    dense = []
    for a_, b_ in zip(body, body[1:]):
        for j in range(6): dense.append((a_[0] + (b_[0] - a_[0]) * j / 6, a_[1] + (b_[1] - a_[1]) * j / 6))
    for r_, col, t_ in (((w + 14) / 2, (0, 0, 0), head + 20), (w / 2, color, head)):
        for p in dense: d.ellipse([p[0] - r_, p[1] - r_, p[0] + r_, p[1] + r_], fill=col)
        d.polygon(tri(t_), fill=col)


def bubble(canvas, text, cx, cy, size=58, pad=(34, 18), fill=(250, 214, 72)):
    f = ImageFont.truetype("C:/Windows/Fonts/impact.ttf", size)
    d = ImageDraw.Draw(canvas)
    bb = d.textbbox((0, 0), text, font=f); tw, th = bb[2] - bb[0], bb[3] - bb[1]
    x0, y0 = cx - tw / 2 - pad[0], cy - th / 2 - pad[1]; x1, y1 = cx + tw / 2 + pad[0], cy + th / 2 + pad[1]
    d.rounded_rectangle([x0 - 6, y0 - 6, x1 + 6, y1 + 6], radius=26, fill=(0, 0, 0))
    d.rounded_rectangle([x0, y0, x1, y1], radius=20, fill=fill)
    d.text((cx - tw / 2 - bb[0], cy - th / 2 - bb[1]), text, font=f, fill=(15, 12, 12))


R = V2 + "/recortes/"
triste = gray(outlined(R + "chavo_triste.png"))
sonrie = pop(outlined(R + "chavo_sonrie.png", crop=(0, 0, 498, 432)))   # sin el logo de abajo
senala = pop(outlined(R + "chavo_senala.png"))
bg = f"{CUADROS}/camion_alza.png"

# A: triste -> sonríe (igual que el Homero v2)
A = bg_scene(bg, focus=(0.5, 0.3), zoom=1.1).convert("RGBA")
place(A, triste, height=620, x=-30)
place(A, sonrie, height=630, x=690)
arrow(A, (390, 250), (520, 100), (680, 190))
A.convert("RGB").save(f"{V2}/Miniatura_Chavo_v2_A.png")

# B: triste -> señala (el gesto de "¿qué no ves?")
B = bg_scene(bg, focus=(0.5, 0.3), zoom=1.1).convert("RGBA")
place(B, triste, height=620, x=-30)
place(B, senala, width=780, x=540)
arrow(B, (390, 250), (520, 100), (680, 190))
B.convert("RGB").save(f"{V2}/Miniatura_Chavo_v2_B.png")
