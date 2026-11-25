from PIL import Image, ImageDraw
import glob, os
fs = sorted(glob.glob('mini/fr2/*.png')); ims = [Image.open(f).convert('RGB') for f in fs]
for im in ims: im.thumbnail((480, 270))
s = Image.new('RGB', (480 * 4, 280 * ((len(ims) + 3) // 4))); d = ImageDraw.Draw(s)
for i, (f, im) in enumerate(zip(fs, ims)):
    s.paste(im, ((i % 4) * 480, (i // 4) * 280)); d.text(((i % 4) * 480 + 5, (i // 4) * 280 + 5), os.path.basename(f), fill=(255, 255, 0))
s.save('mini/_fr.jpg')
