"""Recorta jefes de cuadros del juego con rembg (fondo quitado) -> arte/<nombre>.png"""
from rembg import remove
from PIL import Image
J = [('fr/unl_darkgaia_2.png', None, 'DarkGaia'), ('fr2/sr_erazor_8.png', None, 'Erazor')]
for src, box, name in J:
    im = Image.open(src).convert('RGB')
    if box: im = im.crop(box)
    out = remove(im)
    bb = out.getchannel('A').point(lambda a: 255 if a > 30 else 0).getbbox()
    out.crop(bb).save(f'arte/{name}.png'); print(name, out.crop(bb).size)
