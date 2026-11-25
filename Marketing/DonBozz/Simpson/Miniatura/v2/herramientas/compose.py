import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageOps, ImageFont
from scipy import ndimage as ndi
S=sys.argv[1]; OUT=sys.argv[2]
W,H=1280,720

def bg_scene(path, blur=7, dark=0.62, sat=1.15, box=(106,0,746,480), focus=(0.5,0.5), zoom=1.0):
    im=Image.open(path).convert("RGB").crop(box)
    w,h=im.size; s=max(W/w,H/h)*zoom
    im=im.resize((round(w*s),round(h*s)),Image.LANCZOS)
    x=int((im.width-W)*focus[0]); y=int((im.height-H)*focus[1])
    im=im.crop((x,y,x+W,y+H)).filter(ImageFilter.GaussianBlur(blur))
    im=ImageEnhance.Color(im).enhance(sat)
    im=ImageEnhance.Brightness(im).enhance(dark)
    return vignette(im)

def vignette(im, strength=0.55):
    yy,xx=np.mgrid[0:H,0:W]
    r=np.sqrt(((xx-W/2)/(W/2))**2+((yy-H/2)/(H/2))**2)
    v=1-strength*np.clip(r-0.55,0,1)**1.6
    a=np.asarray(im).astype(np.float32)*v[...,None]
    return Image.fromarray(a.clip(0,255).astype(np.uint8))

def gray(co, contrast=1.15):
    r,g,b,a=co.split()
    l=ImageEnhance.Contrast(ImageOps.grayscale(co.convert("RGB"))).enhance(contrast)
    return Image.merge("RGBA",(l,l,l,a))

def pop(co, sat=1.2, con=1.08):
    r,g,b,a=co.split()
    arr=np.asarray(co.convert("RGB")).astype(np.int16)
    lav=(arr.min(-1)>170)&(arr[...,2]-arr[...,0]<60)
    arr[lav]=(250,250,252)
    rgb=Image.fromarray(arr.astype(np.uint8))
    rgb=ImageEnhance.Contrast(ImageEnhance.Color(rgb).enhance(sat)).enhance(con)
    return Image.merge("RGBA",(*rgb.split(),a))

def place(canvas, co, height=None, x=0, bottom=H, crop=None, width=None, shadow=True):
    if crop: co=co.crop(crop)
    bb=co.getbbox(); co=co.crop(bb)
    s=(height/co.height) if height else (width/co.width)
    co=co.resize((round(co.width*s),round(co.height*s)),Image.LANCZOS)
    y=bottom-co.height
    if shadow:
        a=co.split()[3].filter(ImageFilter.GaussianBlur(14))
        sh=Image.new("RGBA",co.size,(0,0,0,0)); sh.putalpha(a.point(lambda v:int(v*0.55)))
        canvas.alpha_composite(sh,(int(x)+10,int(y)+12))
    canvas.alpha_composite(co,(int(x),int(y)))
    return (int(x),int(y),int(x)+co.width,int(y)+co.height)

def arrow(canvas, p0, p1, p2, color=(70,225,60), w=30, head=78):
    """Quadratic bezier p0->p2 with control p1; black outline, Alan-style."""
    d=ImageDraw.Draw(canvas)
    pts=[]
    for i in range(61):
        t=i/60
        x=(1-t)**2*p0[0]+2*(1-t)*t*p1[0]+t*t*p2[0]
        y=(1-t)**2*p0[1]+2*(1-t)*t*p1[1]+t*t*p2[1]
        pts.append((x,y))
    end=pts[-1]; prev=pts[-6]
    ang=math.atan2(end[1]-prev[1],end[0]-prev[0])
    body=pts[:-8]
    def tri(sz):
        tip=(end[0]+math.cos(ang)*sz*0.25,end[1]+math.sin(ang)*sz*0.25)
        b=(end[0]-math.cos(ang)*sz*0.75,end[1]-math.sin(ang)*sz*0.75)
        n=(-math.sin(ang),math.cos(ang))
        return [tip,(b[0]+n[0]*sz*0.6,b[1]+n[1]*sz*0.6),(b[0]-n[0]*sz*0.6,b[1]-n[1]*sz*0.6)]
    k=(0,0,0)
    dense=[]
    for a_,b_ in zip(body,body[1:]):
        for j in range(6): dense.append((a_[0]+(b_[0]-a_[0])*j/6,a_[1]+(b_[1]-a_[1])*j/6))
    for r_,col,t_ in (((w+14)/2,k,head+20),(w/2,color,head)):
        for p in dense: d.ellipse([p[0]-r_,p[1]-r_,p[0]+r_,p[1]+r_],fill=col)
        d.polygon(tri(t_),fill=col)

def bubble(canvas, text, cx, cy, size=64, pad=(34,18), fill=(250,214,72)):
    f=ImageFont.truetype("C:/Windows/Fonts/impact.ttf",size)
    d=ImageDraw.Draw(canvas)
    bb=d.textbbox((0,0),text,font=f); tw,th=bb[2]-bb[0],bb[3]-bb[1]
    x0,y0=cx-tw/2-pad[0],cy-th/2-pad[1]; x1,y1=cx+tw/2+pad[0],cy+th/2+pad[1]
    d.rounded_rectangle([x0-6,y0-6,x1+6,y1+6],radius=26,fill=(0,0,0))
    d.rounded_rectangle([x0,y0,x1,y1],radius=20,fill=fill)
    d.text((cx-tw/2-bb[0],cy-th/2-bb[1]),text,font=f,fill=(15,12,12))

def photo(path, box, border=10, rim=3):
    """A rectangular crop treated as a cut-out with the same white outline."""
    im=Image.open(path).convert("RGB").crop(box)
    w,h=im.size
    out=Image.new("RGBA",(w+2*(border+rim),h+2*(border+rim)),(20,16,18,255))
    ImageDraw.Draw(out).rectangle([rim,rim,out.width-rim-1,out.height-rim-1],fill=(255,255,255,255))
    out.paste(im,(border+rim,border+rim))
    return out

cut=lambda n: Image.open(f"{S}/cut_{n}.png").convert("RGBA")
bgpath=f"{S}/haz_158.5.png"

# ---------- A: antes -> después (modelo Don Eladio) ----------
A=bg_scene(bgpath,focus=(0.5,0.25),zoom=1.15).convert("RGBA")
place(A,gray(cut("88.5")),height=640,x=0,crop=(600,200,1400,1440))
place(A,pop(cut("129.5")),height=740,x=610,crop=(600,0,1800,1440))
arrow(A,(360,300),(470,170),(615,250))
bubble(A,"HAZLO POR ELLA",640,62,size=58)
A.convert("RGB").save(f"{OUT}/Miniatura_Homero_A.png")

# ---------- B: el objeto (modelo Lalo / Capitán América) ----------
B=bg_scene(bgpath,focus=(0.5,0.2),zoom=1.1,dark=0.5).convert("RGBA")
board=photo(f"{S}/clean_156.png",(0,180,1920,1440))   # cleaned 3x frame: the DO IT FOR HER wall
board=board.rotate(4,expand=True,resample=Image.BICUBIC)
place(B,board,width=820,x=-30,bottom=H+60)
place(B,pop(cut("129.5")),height=760,x=700,crop=(600,0,1800,1440))
B.convert("RGB").save(f"{OUT}/Miniatura_Homero_B.png")

# ---------- C: la placa de Burns -> Homero con Maggie ----------
C=bg_scene(bgpath,focus=(0.5,0.25),zoom=1.15).convert("RGBA")
place(C,gray(cut("22.5")),width=760,x=-60,bottom=H+10)
place(C,pop(cut("118")),height=730,x=720,crop=(900,90,1700,1260))
arrow(C,(520,250),(640,110),(770,190))
C.convert("RGB").save(f"{OUT}/Miniatura_Homero_C.png")
