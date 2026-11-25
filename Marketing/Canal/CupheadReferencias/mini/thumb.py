"""Miniaturas del largo de Cuphead (fórmula de Alan: gris vs color, flecha verde, ≤3 palabras).
Izquierda: dibujo original de los años 30 (byn) con el año; derecha: el jefe de Cuphead a color."""
import math, subprocess, os
from PIL import Image, ImageOps, ImageDraw, ImageFilter, ImageEnhance, ImageFont

FF = r"C:\Users\delfa\Downloads\ffmpeg.exe"
H0 = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(H0, "..", "fuentes")
FONT = r"C:\Users\delfa\Desktop\Videojogo\Marketing\Recursos\Fuentes\Anton-Regular.ttf"
W, H = 1280, 720


def frame(vid, t):
    p = os.path.join(H0, f"_{vid}_{t}.png")
    if not os.path.exists(p):
        subprocess.run([FF, "-v", "error", "-ss", str(t), "-i", os.path.join(SRC, vid + ".mp4"), "-frames:v", "1", "-y", p], check=True)
    im = Image.open(p).convert("RGB")
    bb = ImageOps.grayscale(im).point(lambda v: 255 if v > 24 else 0).getbbox()   # saca franjas negras
    return im.crop(bb) if bb else im


def panel(im, box, gray):
    w, h = im.size
    c = im.crop((int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h))).resize((640, 720), Image.LANCZOS)
    if gray:
        g = ImageEnhance.Contrast(ImageOps.grayscale(c)).enhance(1.2)
        c = Image.merge("RGB", (g, g, g))
    else:
        c = ImageEnhance.Color(c).enhance(1.3)
    return c.filter(ImageFilter.UnsharpMask(2, 80, 2))


def arrow(d):
    cx, cy, r = 640, 140, 220
    pts = [(cx + r * math.cos(math.radians(a)), cy - r * 0.4 * math.sin(math.radians(a))) for a in range(150, 38, -2)]
    d.line(pts, fill=(0, 0, 0), width=40, joint="curve")
    d.line(pts, fill=(60, 205, 90), width=26, joint="curve")
    tip = (cx + r * math.cos(math.radians(22)), cy - r * 0.4 * math.sin(math.radians(22)))
    base = pts[-1]; ang = math.atan2(tip[1] - base[1], tip[0] - base[0])
    def tri(sz):
        return [(tip[0] + math.cos(ang) * sz * 0.35, tip[1] + math.sin(ang) * sz * 0.35),
                (base[0] + math.cos(ang + math.pi / 2) * sz * 0.6 - math.cos(ang) * sz * 0.2,
                 base[1] + math.sin(ang + math.pi / 2) * sz * 0.6 - math.sin(ang) * sz * 0.2),
                (base[0] - math.cos(ang + math.pi / 2) * sz * 0.6 - math.cos(ang) * sz * 0.2,
                 base[1] - math.sin(ang + math.pi / 2) * sz * 0.6 - math.sin(ang) * sz * 0.2)]
    d.polygon(tri(124), fill=(0, 0, 0)); d.polygon(tri(90), fill=(60, 205, 90))


def make(name, left, lbox, right, rbox, year):
    out = Image.new("RGB", (W, H))
    out.paste(panel(frame(*left), lbox, True), (0, 0))
    out.paste(panel(frame(*right), rbox, False), (640, 0))
    d = ImageDraw.Draw(out)
    d.rectangle((636, 0, 643, H), fill=(0, 0, 0))
    arrow(d)
    f = ImageFont.truetype(FONT, 150)
    d.text((34, 545), year, font=f, fill=(255, 210, 60), stroke_width=10, stroke_fill=(0, 0, 0))
    out.save(os.path.join(H0, name))
    out.resize((320, 180), Image.LANCZOS).save(os.path.join(H0, "_160_" + name))   # cómo se ve en el feed
    print("ok", name)


make("Miniatura_Cuphead_A_Diablo.jpg", ("vNMxnK8dM9U", 262.3), (0.20, 0.0, 0.70, 0.75),
     ("kXkMQvGsjTA", 4783.0), (0.32, 0.0, 0.68, 0.72), "1929")
make("Miniatura_Cuphead_B_Gigante.jpg", ("v1iujW0Wz2k", 262.0), (0.10, 0.0, 0.60, 0.75),
     ("kXkMQvGsjTA", 5949.2), (0.58, 0.08, 0.94, 0.80), "1933")
make("Miniatura_Cuphead_C_Esqueleto.jpg", ("h03QBNVwX8Q", 253.0), (0.25, 0.0, 0.75, 0.75),
     ("kXkMQvGsjTA", 3910.6), (0.33, 0.0, 0.69, 0.72), "1929")
