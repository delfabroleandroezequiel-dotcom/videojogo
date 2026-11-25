"""Miniatura del Patricio: receta de ../../Shorts/build/thumb.py (izquierda B/N, flecha verde, derecha a color)."""
from PIL import Image, ImageOps, ImageDraw, ImageFilter, ImageEnhance
import math
W, H = 1280, 720
out = Image.new("RGB", (W, H))
# izquierda: el Patricio genio, mirando desde arriba, en blanco y negro
g = ImageOps.grayscale(Image.open("genio.png").convert("RGB").crop((300, 0, 1260, 1080)))
g = ImageEnhance.Contrast(g).enhance(1.2).resize((640, 720), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 80, 2))
out.paste(Image.merge("RGB", (g, g, g)), (0, 0))
# derecha: Patricio de vuelta, al lado de Bob Esponja, a color
e = Image.open("amigos.png").convert("RGB").crop((420, 40, 1380, 1080)).resize((640, 720), Image.LANCZOS)
e = ImageEnhance.Color(e).enhance(1.25).filter(ImageFilter.UnsharpMask(2, 90, 2))
out.paste(e, (640, 0))
d = ImageDraw.Draw(out)
cx, cy, r = 615, 400, 175
pts = [(cx + r * math.cos(math.radians(a)), cy - r * 0.45 * math.sin(math.radians(a))) for a in range(150, 38, -2)]
d.line(pts, fill=(0, 0, 0), width=38, joint="curve")
d.line(pts, fill=(60, 205, 90), width=24, joint="curve")
tip = (cx + r * math.cos(math.radians(22)), cy - r * 0.45 * math.sin(math.radians(22)))
base = pts[-1]
ang = math.atan2(tip[1] - base[1], tip[0] - base[0])


def tri(sz):
    return [(tip[0] + math.cos(ang) * sz * 0.35, tip[1] + math.sin(ang) * sz * 0.35),
            (base[0] + math.cos(ang + math.pi / 2) * sz * 0.6 - math.cos(ang) * sz * 0.2,
             base[1] + math.sin(ang + math.pi / 2) * sz * 0.6 - math.sin(ang) * sz * 0.2),
            (base[0] - math.cos(ang + math.pi / 2) * sz * 0.6 - math.cos(ang) * sz * 0.2,
             base[1] - math.sin(ang + math.pi / 2) * sz * 0.6 - math.sin(ang) * sz * 0.2)]


d.polygon(tri(120), fill=(0, 0, 0))
d.polygon(tri(88), fill=(60, 205, 90))
out.save("Miniatura_Patricio.png")
out.convert("RGB").save("Miniatura_Patricio.jpg", quality=92)
