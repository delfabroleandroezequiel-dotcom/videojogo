"""Miniaturas de "Los jefes de Mario que NADIE recuerda" con la fórmula de Alan (BibliaLargos):
un solo fondo continuo de la obra (arte de Sarasaland, desenfocado y oscurecido), recortes grandes con contorno,
Mario a color pensando ("¿y estos quiénes son?"), los jefes olvidados en GRIS detrás. 0–3 palabras.
Arte oficial bajado de Super Mario Wiki en mini/arte/."""
import os
from PIL import Image, ImageOps, ImageFilter, ImageEnhance, ImageDraw, ImageFont

H0 = os.path.dirname(os.path.abspath(__file__)); A = os.path.join(H0, "arte")
FONT = r"C:\Users\delfa\Desktop\Videojogo\Marketing\Recursos\Fuentes\Anton-Regular.ttf"
W, H = 1280, 720


def fondo():
    bg = Image.open(os.path.join(A, "SML_Sarasaland_Artwork.png")).convert("RGB")
    bg = ImageOps.fit(bg, (W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(9))
    bg = ImageEnhance.Brightness(bg).enhance(0.55)
    # viñeta
    v = Image.new("L", (W, H), 0); d = ImageDraw.Draw(v)
    d.ellipse((-260, -200, W + 260, H + 200), fill=255)
    v = v.filter(ImageFilter.GaussianBlur(120))
    return Image.composite(bg, Image.new("RGB", (W, H), (0, 0, 0)), v)


def recorte(nombre, alto, gris=False, borde=6, color_borde=(255, 255, 255), espejo=False):
    im = Image.open(os.path.join(A, nombre)).convert("RGBA")
    bb = im.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox(); im = im.crop(bb)
    if espejo:
        im = ImageOps.mirror(im)
    im = im.resize((int(im.width * alto / im.height), alto), Image.LANCZOS)
    if gris:
        a = im.getchannel("A"); g = ImageEnhance.Contrast(ImageOps.grayscale(im.convert("RGB"))).enhance(1.15)
        g = ImageEnhance.Brightness(g).enhance(0.9)
        im = Image.merge("RGBA", (g, g, g, a))
    else:
        rgb = ImageEnhance.Color(im.convert("RGB")).enhance(1.25); rgb = ImageEnhance.Contrast(rgb).enhance(1.08)
        im = Image.merge("RGBA", (*rgb.split(), im.getchannel("A")))
    # contorno
    a = im.getchannel("A"); pad = borde + 2
    big = Image.new("L", (im.width + 2 * pad, im.height + 2 * pad), 0); big.paste(a, (pad, pad))
    edge = big.filter(ImageFilter.MaxFilter(2 * borde + 1))
    out = Image.new("RGBA", big.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", big.size, color_borde + (255,)), (0, 0), edge)
    out.alpha_composite(im, (pad, pad))
    return out


def sombra(im, r=18, op=160):
    s = Image.new("RGBA", im.size, (0, 0, 0, 0)); s.putalpha(im.getchannel("A").point(lambda a: min(op, a)))
    return s.filter(ImageFilter.GaussianBlur(r))


def pegar(base, im, x, y):
    base.alpha_composite(sombra(im), (x + 10, y + 14)); base.alpha_composite(im, (x, y))


def texto(base, txt, y=H - 150, size=110):
    d = ImageDraw.Draw(base); f = ImageFont.truetype(FONT, size)
    w = d.textlength(txt, font=f); x = (W - w) / 2
    d.text((x, y), txt, font=f, fill=(255, 210, 60), stroke_width=8, stroke_fill=(0, 0, 0))


def mini(nombre, gris_izq, gris_der, txt=None):
    base = fondo().convert("RGBA")
    a, (xa, ya) = gris_izq
    pegar(base, a, xa, ya)
    b, (xb, yb) = gris_der
    pegar(base, b, xb, yb)
    mario = recorte("Mario-ponder-large.png", 1000, borde=7)
    pegar(base, mario, W - mario.width + 10 if txt is None else 430, -30)
    if txt:
        texto(base, txt)
    out = base.convert("RGB"); out.save(os.path.join(H0, nombre), quality=92)
    out.resize((160, 90), Image.LANCZOS).save(os.path.join(H0, "_160_" + nombre))
    print(nombre)


if __name__ == "__main__":
    wart = recorte("SMA_Wart_Artwork.png", 600, gris=True)
    tat = recorte("SML_-_Tatanga_cover_art.png", 470, gris=True)
    king = recorte("SMG_Kingfin_Artwork.png", 520, gris=True, espejo=True)
    # A: Wart y Tatanga en gris a la izquierda, Mario pensando a la derecha. Sin texto.
    mini("Miniatura_Olvidados_A.jpg", (tat, (-20, 40)), (wart, (300, 150)))
    # B: igual, con Kingfin en lugar de Tatanga (jefe 3D más reconocible)
    mini("Miniatura_Olvidados_B.jpg", (king, (-40, 60)), (wart, (330, 150)))
    # C: Mario al centro, gris a los costados, 3 palabras arriba
    mini("Miniatura_Olvidados_C.jpg", (tat, (-30, 230)), (recorte("SMA_Wart_Artwork.png", 520, gris=True), (870, 230)),
         txt="¿LOS RECUERDAS?")
