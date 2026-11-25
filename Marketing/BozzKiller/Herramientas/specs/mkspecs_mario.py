import json, os
B = os.path.dirname(os.getcwd())
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
base = dict(src=os.path.join(B,"src_smb3.mp4"), intro=P+r"\Plantillas\Intro_Mario.mp4",
            cats=P+r"\Plantillas\Gatitos_Mario.mp4", font=P[:-10]+r"Recursos\Fuentes\Anton-Regular.ttf")
W, Y = "white", "yellow"
box = lambda t,d,x,y=0,w=607,h=1080: dict(t=t,d=d,mode="box",x=x,y=y,w=w,h=h)
fit = lambda t,d: dict(t=t,d=d,mode="fit")
S = {
 "m01": ("Short01_SilbatoZelda", 7.4, [box(85.6,1.5,880), fit(88.5,1.3), fit(98.0,2.6), fit(113.0,2.0)],
   [["SABIAS QUE",W,0,0.52],["EL SILBATO",Y,0.52,1.10],["DE MARIO 3",W,1.10,1.52],["TOCA LAS|PRIMERAS NOTAS",W,1.52,3.02],
    ["DE LA CANCION|DE ZELDA","white,yellow",3.02,4.12],["OCARINA|OF TIME",Y,4.12,5.48],["10 AÑOS ANTES",W,5.48,6.12],["DE QUE|SALIERA",W,6.12,7.4]]),
 "m02": ("Short02_BooEsposa", 6.85, [box(367.0,1.5,600), fit(377.0,1.9), fit(395.0,1.5), fit(388.3,1.0), fit(396.4,0.95)],
   [["SABIAS QUE",W,0,0.54],["BOO",Y,0.54,0.9],["ESTA|INSPIRADO",W,0.9,1.5],["EN LA ESPOSA",Y,1.5,1.98],["DE UN|DISEÑADOR",W,1.98,2.88],
    ["DEL JUEGO",W,2.88,3.54],["ERA MUY|TIMIDA","white,yellow",3.54,4.38],["HASTA QUE|UN DIA",W,4.38,5.04],["EXPLOTO|CON EL","yellow,white",5.04,6.85]]),
 "m03": ("Short03_MarioCentauro", 6.85, [fit(553.0,1.78), fit(567.0,2.16), dict(t=572.0,d=1.5,mode="still",x=620,y=0,w=607,h=1080), fit(583.0,1.41)],
   [["SABIAS QUE",W,0,0.5],["ANTES DEL|TRAJE TANUKI","white,yellow",0.5,1.78],["MARIO SE IBA A|TRANSFORMAR",W,1.78,2.98],
    ["EN UN|CENTAURO","white,yellow",2.98,3.94],["MITAD|CABALLO",Y,3.94,5.02],["MITAD|FONTANERO",Y,5.02,6.85]]),
 "m04": ("Short04_ChompLibre", 7.3, [box(637.0,2.5,330), fit(640.0,0.64), fit(645.9,1.4), fit(648.3,0.48), fit(651.0,2.28)],
   [["SABIAS QUE",W,0,0.52],["SI DEJAS AL|CHOMP CADENAS","white,yellow",0.52,1.58],["TIRANDO UNAS|50 VECES","white,yellow",1.58,3.14],
    ["ROMPE LA|CADENA",Y,3.14,4.06],["Y SALE|VOLANDO",W,4.06,5.02],["Y EN MARIO|MAKER 2",W,5.02,6.02],["TAMBIEN PASA",Y,6.02,7.3]]),
 "m05": ("Short05_DetrasDelEscenario", 7.1, [box(282.0,1.0,700,150,500,889), dict(t=285.0,d=1.98,mode="still",x=700,y=150,w=500,h=889),
    fit(289.0,1.62), fit(290.5,2.5)],
   [["SABIAS QUE",W,0,0.56],["SI TE AGACHAS|UN RATO",W,0.56,1.54],["SOBRE UN|BLOQUE BLANCO","white,yellow",1.54,2.98],
    ["MARIO SE CAE",W,2.98,3.74],["DETRAS DEL|ESCENARIO",Y,3.74,4.6],["Y PUEDE PASAR|EL NIVEL",W,4.6,5.88],["POR ATRAS",Y,5.88,7.1]]),
}
os.makedirs(P+r"\Mario", exist_ok=True)
for k,(name,body,clips,caps) in S.items():
    d = dict(base, voice=os.path.join(os.getcwd(),k+".mp3"), out=P+rf"\Mario\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(k+".json","w",encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
