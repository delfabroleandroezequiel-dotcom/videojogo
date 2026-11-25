import json, os
B = os.path.dirname(os.getcwd())
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
base = dict(src=os.path.join(B,"src_cuphead_dlc.mp4"), intro=P+r"\Plantillas\Intro_Cuphead.mp4",
            cats=P+r"\Plantillas\Gatitos_Cuphead.mp4", font=P[:-10]+r"Recursos\Fuentes\Anton-Regular.ttf")
W, Yl = "white", "yellow"
S = {
 "c02": ("Short02_PasitoAPasito", 7.52, [
    dict(t=113.6,d=2.5,mode="fit"), dict(t=118.0,d=1.9,mode="fit"), dict(t=122.0,d=0.7),
    dict(t=126.0,d=2.42)],
   [["SABIAS QUE",W,0,0.48],["SI PIERDES|CONTRA",W,0.48,1.10],["LOS PEONES|DEL REY",Yl,1.10,2.54],
    ["TE DICEN",W,2.54,3.04],["PASITO A|PASITO",Yl,3.04,5.14],["UNA|REFERENCIA",W,5.14,6.08],["A|DESPACITO","white,yellow",6.08,7.5]]),
 "c03": ("Short03_AlfilTrinidad", 6.7, [
    dict(t=245.5,d=1.7,x=640), dict(t=248.0,d=1.9,mode="fit"), dict(t=250.0,d=2.0,mode="fit"), dict(t=242.6,d=1.1,x=640)],
   [["SABIAS QUE",W,0,0.54],["EL ALFIL|DE TRES CARAS","white,yellow",0.54,1.62],["REPRESENTA A",W,1.62,2.36],
    ["LA SANTISIMA|TRINIDAD",Yl,2.36,3.8],["EL PADRE",W,3.8,4.48],["EL HIJO",W,4.48,5.14],["Y EL ESPIRITU|SANTO",Yl,5.14,6.7]]),
 "c04": ("Short04_CabezasCortadas", 6.6, [
    dict(t=231.5,d=1.2,mode="fit"), dict(t=233.5,d=0.75,x=420), dict(t=235.5,d=2.45,mode="fit"), dict(t=238.5,d=2.2,mode="fit")],
   [["SABIAS QUE",W,0,0.5],["LAS CABEZAS|CORTADAS",Yl,0.5,1.46],["DE LA TORRE",W,1.46,1.96],["SON DEL REY",W,1.96,2.42],
    ["LUIS XVI",Yl,2.42,3.12],["Y MARIA|ANTONIETA",Yl,3.12,4.54],["LOS DOS|FUERON",W,4.54,5.16],["DECAPITADOS",Yl,5.16,6.6]]),
 "c05": ("Short05_PastelMentira", 6.8, [
    dict(t=459.0,d=2.0,x=1050), dict(t=461.5,d=1.3,x=900), dict(t=476.0,d=1.2,x=1000),
    dict(t=471.5,d=2.3,mode="box",x=720,y=0,w=506,h=900)],
   [["SABIAS QUE",W,0,0.56],["EL PASTEL",Yl,0.56,1.12],["DEL CHEF|SALEROSO",W,1.12,1.92],["ES UNA|MENTIRA","white,yellow",1.92,3.16],
    ["IGUAL QUE|EN PORTAL","white,yellow",3.16,4.44],["EL PASTEL|NUNCA EXISTIO",Yl,4.44,6.8]]),
 "c06": ("Short06_ReyHielo", 6.6, [
    dict(t=506.0,d=1.8,mode="box",x=1300,y=0,w=500,h=889), dict(t=508.5,d=1.7,mode="box",x=180,y=0,w=500,h=889), dict(t=509.5,d=1.5,mode="box",x=0,y=180,w=500,h=889),
    dict(t=511.0,d=1.6,mode="fit")],
   [["SABIAS QUE",W,0,0.56],["GENOVEVO|EL GELIDO",Yl,0.56,1.54],["SE PARECE",W,1.54,2.10],["SOSPECHOSAMENTE",W,2.10,2.98],
    ["AL MISMISIMO",W,2.98,4.14],["REY HIELO",Yl,4.14,4.96],["DE HORA DE|AVENTURAS","white,yellow",4.96,6.6]]),
}
for k,(name,body,clips,caps) in S.items():
    d = dict(base, voice=os.path.join(os.getcwd(),k+".mp3"), out=P+rf"\Cuphead\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(k+".json","w",encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
