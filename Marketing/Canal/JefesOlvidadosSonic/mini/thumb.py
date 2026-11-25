"""Miniaturas de "Los jefes de Mario que NADIE recuerda" con la fórmula de Alan (BibliaLargos):
un solo fondo continuo de la obra (arte de Sarasaland, desenfocado y oscurecido), recortes grandes con contorno,
Mario a color pensando ("¿y estos quiénes son?"), los jefes olvidados en GRIS detrás. 0–3 palabras.
Arte oficial bajado de Super Mario Wiki en mini/arte/."""
import os
from PIL import Image, ImageOps, ImageFilter, ImageEnhance, ImageDraw, ImageFont

H0 = os.path.dirname(os.path.abspath(__file__)); A = os.path.join(H0, "arte")
FONT = r"C:\Users\delfa\Desktop\Videojogo\Marketing\Recursos\Fuentes\Anton-Regular.ttf"
W, H = 1280, 720


def fondo(archivo=None):
    bg = Image.open(archivo) if archivo else Image.open(os.path.join(H0, "fr2", "rivals2_ifrit_262.png")).convert("RGB")
    bg = ImageOps.fit(bg, (W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(9))
    bg = ImageEnhance.Brightness(bg).enhance(0.55)
    # viñeta
    v = Image.new("L", (W, H), 0); d = ImageDraw.Draw(v)
    d.ellipse((-260, -200, W + 260, H + 200), fill=255)
    v = v.filter(ImageFilter.GaussianBlur(120))
    return Image.composite(bg, Image.new("RGB", (W, H), (0, 0, 0)), v)


def recorte(nombre, alto, gris=False, borde=6, color_borde=(255, 255, 255), espejo=False, pixel=False):
    im = Image.open(os.path.join(A, nombre)).convert("RGBA")
    bb = im.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox(); im = im.crop(bb)
    if espejo:
        im = ImageOps.mirror(im)
    im = im.resize((int(im.width * alto / im.height), alto), Image.NEAREST if pixel else Image.LANCZOS)
    if gris:
        a = im.getchannel("A"); g = ImageEnhance.Contrast(ImageOps.grayscale(im.convert("RGB"))).enhance(1.35)
        g = ImageEnhance.Brightness(g).enhance(1.2)
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
    gaia = recorte("DarkGaia.png", 640, gris=True)
    era = recorte("Erazor.png", 430, gris=True)
    sonic = recorte("Sonic_SSBU.png", 760, borde=7)

    def mini_s(nombre, txt=None, centro=False):
        base = fondo().convert("RGBA")
        if centro:
            pegar(base, gaia, -260, 60); pegar(base, era, W - era.width + 30, 290)
            pegar(base, sonic, (W - sonic.width) // 2, -10)
        else:
            pegar(base, gaia, 120, -40); pegar(base, era, -40, 300)
            pegar(base, sonic, W - sonic.width + 40, -20)
        if txt:
            texto(base, txt)
        out = base.convert("RGB"); out.save(os.path.join(H0, nombre), quality=92)
        out.resize((160, 90), Image.LANCZOS).save(os.path.join(H0, "_160_" + nombre)); print(nombre)

    mini_s("Miniatura_OlvidadosSonic_A.jpg")

    # B (imágenes que bajó el autor): Sonic pensando (Sonic X) a color, Scorpius y Dark Gaia en gris, Green Hill de fondo.
    base = fondo(os.path.join(A, "fondo_greenhill.webp")).convert("RGBA")
    pegar(base, recorte("DarkGaia.png", 520, gris=True), -190, -20)
    pegar(base, recorte("Scorpius.webp", 360, gris=True, pixel=True), -20, 330)
    # la imagen viene cortada arriba, abajo y a la izquierda: espejada (mira a los jefes) y con los bordes cortados fuera del cuadro
    son = recorte("Sonic_pensando.png", 730, borde=7, espejo=True)
    pegar(base, son, W - son.width + 70, -5)
    out = base.convert("RGB"); out.save(os.path.join(H0, "Miniatura_OlvidadosSonic_B.jpg"), quality=92)
    out.resize((160, 90), Image.LANCZOS).save(os.path.join(H0, "_160_Miniatura_OlvidadosSonic_B.jpg")); print("B")
    mini_s("Miniatura_OlvidadosSonic_C.jpg", txt="¿LOS RECUERDAS?", centro=True)
