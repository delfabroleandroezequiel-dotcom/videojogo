import numpy as np
from PIL import Image
from scipy import ndimage as ndi
from scipy.cluster.vq import kmeans2

def load(path, box=(106,0,746,480)):
    return Image.open(path).convert("RGB").crop(box)

def clean(img, scale=3, k=16, smooth=None, seed=1, ink_delta=55, ink_max=140):
    """Flat-colour re-render: upscale, cluster colours into a palette, then
    majority-smooth the label map so edges come out crisp and round."""
    w,h=img.size
    big=np.asarray(img.resize((w*scale,h*scale),Image.LANCZOS)).astype(np.float32)
    big=ndi.median_filter(big,size=(scale+2,scale+2,1))
    px=big.reshape(-1,3)
    rng=np.random.default_rng(seed)
    sample=px[rng.choice(len(px),40000,replace=False)]
    pal,_=kmeans2(sample,k,seed=seed,minit="++")
    d=((px[:,None,:]-pal[None,:,:])**2).sum(-1)
    lab=d.argmin(1).reshape(big.shape[:2])
    s=smooth or scale*0.9
    score=np.stack([ndi.gaussian_filter((lab==i).astype(np.float32),s) for i in range(k)])
    lab=score.argmax(0)
    out=pal[lab]
    # ink: pixels clearly darker than their surroundings in the original
    raw=np.asarray(img.resize((w*scale,h*scale),Image.BICUBIC)).astype(np.float32)@np.array([.299,.587,.114],np.float32)
    raw=ndi.gaussian_filter(raw,scale*0.35)
    bg=ndi.maximum_filter(ndi.gaussian_filter(raw,scale*1.2),size=scale*5)
    ink=(raw<bg-ink_delta)&(raw<ink_max)
    ink=ndi.gaussian_filter(ink.astype(np.float32),scale*0.5)>0.42
    out[ink]=(30,24,26)
    return Image.fromarray(out.clip(0,255).astype(np.uint8)), lab, pal

def mask_from_seeds(lab, seeds, scale=3, grow=4, exclude=()):
    """Connected regions (same palette label) under the seed points, in source-frame px;
    then grow over dark ink and fill holes."""
    comp=np.zeros(lab.shape,bool)
    for x,y in seeds:
        yy,xx=int(y*scale),int(x*scale)
        l=lab[yy,xx]
        cc,_=ndi.label(lab==l)
        comp|=cc==cc[yy,xx]
    m=ndi.binary_dilation(comp,iterations=grow*scale)
    m=ndi.binary_fill_holes(ndi.binary_closing(m,iterations=2*scale))
    m=ndi.binary_erosion(m,iterations=(grow-2)*scale)
    return m

def cutout(clean_img, mask, outline=10, dark=3, feather=1.2):
    """RGBA cut-out with Alan-style white outline (+ thin dark rim outside)."""
    m=mask.astype(np.float32)
    m=ndi.gaussian_filter(m,feather)
    a=(m>0.5)
    dist=ndi.distance_transform_edt(~a)
    white=dist<=outline
    rim=(dist>outline)&(dist<=outline+dark)
    rgb=np.asarray(clean_img.convert("RGB")).copy()
    out=np.zeros(rgb.shape[:2]+(4,),np.uint8)
    out[rim]=(20,16,18,255)
    out[white]=(255,255,255,255)
    out[a,:3]=rgb[a]; out[a,3]=255
    # soft edges
    alpha=ndi.gaussian_filter(out[...,3].astype(np.float32),0.8)
    out[...,3]=alpha.clip(0,255).astype(np.uint8)
    return Image.fromarray(out,"RGBA")

def mask_from_polygon(clean_img, polys, band=14, scale=3, beta=4000, bg_seeds=(), r=5):
    """Rough hand polygon(s) in source px -> precise mask: random walker between
    a shrunk polygon (sure fg) and the outside of a grown one (sure bg)."""
    from PIL import ImageDraw
    from skimage.segmentation import random_walker
    w,h=clean_img.size[0]//scale, clean_img.size[1]//scale
    pm=Image.new("L",(w,h),0); d=ImageDraw.Draw(pm)
    for p in polys: d.polygon(p,fill=255)
    pm=np.asarray(pm)>0
    fg=ndi.binary_erosion(pm,iterations=band)
    bg=~ndi.binary_dilation(pm,iterations=band)
    # frame edges that the polygon touches stay fg (character cut by the frame)
    yy,xx=np.mgrid[0:h,0:w]
    for (x,y) in bg_seeds:
        disk=(xx-x)**2+(yy-y)**2<=r*r
        bg|=disk; fg&=~ndi.binary_dilation(disk,iterations=3)
    markers=np.zeros((h,w),np.int32); markers[bg]=2; markers[fg]=1
    small=np.asarray(clean_img.convert("RGB").resize((w,h),Image.LANCZOS)).astype(np.float32)/255
    prob=random_walker(small,markers,beta=beta,mode="cg_j",channel_axis=-1,return_full_prob=True)[0]
    big=np.asarray(Image.fromarray((prob*255).astype(np.uint8)).resize((w*scale,h*scale),Image.BICUBIC)).astype(np.float32)/255
    big=ndi.gaussian_filter(big,scale*0.6)
    m=big>0.5
    m=ndi.binary_fill_holes(m)
    lab,n=ndi.label(m)
    if n>1:
        sizes=ndi.sum(m,lab,range(1,n+1)); m=lab==(1+int(np.argmax(sizes)))
    return m
