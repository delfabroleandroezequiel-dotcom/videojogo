"""Specs del lote GTA 6 (fuente: 'GTA 6: Curiosidades y secretos del mapa', P_nTbkZPU3U, subido a 1080p).
Correr desde la carpeta de las voces (g01..g07.mp3): genera g01..g07.json ahi y los copia aca."""
import json, os, shutil
P = r"C:\Users\delfa\Desktop\Videojogo\Marketing\BozzKiller"
B = os.path.dirname(os.getcwd())
base = dict(src=os.path.join(B, "src_gta.mp4"), intro=P + r"\Plantillas\Intro_GTA.mp4",
            cats=P + r"\Plantillas\Gatitos_GTA.mp4", font=P[:-10] + r"Recursos\Fuentes\Anton-Regular.ttf")
W, Yl = "white", "yellow"
S = {
 "g01": ("Short01_FloresSangre", 8.25, [
    dict(t=912.5, d=1.6), dict(t=921.0, d=1.7), dict(t=926.0, d=1.5, mode="fit"),
    dict(t=937.5, d=1.6, mode="fit"), dict(t=942.0, d=1.85, mode="fit")],
   [["SABIAS QUE", W, 0, 0.58], ["LA MONTAÑA|MAS ALTA", W, 0.58, 1.66], ["DE GTA 6", W, 1.66, 2.58],
    ["DESDE EL|HELICOPTERO", W, 2.58, 3.70], ["PARECEN|CHARCOS", W, 3.70, 4.46], ["DE SANGRE", Yl, 4.46, 4.92],
    ["PERO CUANDO|ATERRIZAS", W, 4.92, 6.28], ["DESCUBRES|QUE SON", W, 6.28, 7.16], ["FLORES|ROJAS", Yl, 7.16, 8.25]]),
 "g02": ("Short02_AvionetaLlamas", 7.45, [
    dict(t=215.0, d=1.5), dict(t=228.5, d=1.4, mode="fit"), dict(t=233.5, d=1.2),
    dict(t=287.5, d=1.3), dict(t=312.0, d=2.05, mode="fit")],
   [["SABIAS QUE", W, 0, 0.52], ["CAMINO A|LA CIUDAD", W, 0.52, 1.50], ["UNA AVIONETA|EN LLAMAS", "white,yellow", 1.50, 2.64],
    ["SE ESTRELLA|FRENTE A TI", W, 2.64, 3.70], ["Y SI LA|REGISTRAS", W, 3.70, 4.82], ["EL SUELO|QUEDA LLENO", W, 4.82, 5.76],
    ["DE DINERO", Yl, 5.76, 6.16], ["Y ARMAS|GRATIS", Yl, 6.16, 7.45]]),
 "g03": ("Short03_CarcelLucia", 7.8, [
    dict(t=1091.0, d=1.4, mode="fit"), dict(t=1094.0, d=1.3, mode="fit"), dict(t=1096.5, d=1.2),
    dict(t=1116.0, d=1.2), dict(t=1123.0, d=1.4), dict(t=1129.5, d=1.3)],
   [["SABIAS QUE", W, 0, 0.54], ["PUEDES|LANZARTE", W, 0.54, 1.24], ["EN PARACAIDAS", W, 1.24, 1.86],
    ["DENTRO DE|LA CARCEL", "white,yellow", 1.86, 2.66], ["PARA|RESCATAR", W, 2.66, 3.34], ["A LUCIA", Yl, 3.34, 4.40],
    ["PERO CUANDO|POR FIN", W, 4.40, 5.30], ["ABRES|LA CELDA", W, 5.30, 6.40], ["LUCIA|NO ESTA", Yl, 6.40, 7.80]]),
 "g04": ("Short04_DiscotecaBoveda", 7.5, [
    dict(t=365.0, d=1.2), dict(t=370.5, d=1.5), dict(t=373.5, d=1.5),
    dict(t=395.0, d=1.3, mode="fit"), dict(t=397.0, d=1.0), dict(t=400.5, d=1.0)],
   [["SABIAS QUE", W, 0, 0.54], ["EN VICE CITY", W, 0.54, 1.16], ["HAY UNA|DISCOTECA", W, 1.16, 1.98],
    ["DECORADA|COMO", W, 1.98, 2.86], ["LA CAMARA|ACORAZADA", Yl, 2.86, 3.82], ["DE UN BANCO", Yl, 3.82, 4.78],
    ["CON MAS|DE CIEN", "white,yellow", 4.78, 5.30], ["PERSONAS|BAILANDO", W, 5.30, 6.38], ["AL MISMO|TIEMPO", W, 6.38, 7.50]]),
 "g05": ("Short05_CirculoPiedras", 7.15, [
    dict(t=926.5, d=1.4, mode="fit"), dict(t=949.0, d=1.3), dict(t=955.5, d=1.2, mode="fit"),
    dict(t=958.0, d=1.5, mode="fit"), dict(t=960.5, d=1.75, mode="fit")],
   [["SABIAS QUE", W, 0, 0.50], ["EN LA CIMA|MAS ALTA", W, 0.50, 1.50], ["DEL MAPA", W, 1.50, 1.80],
    ["HAY UNA ROCA", W, 1.80, 2.60], ["RODEADA|DE PIEDRAS", W, 2.60, 3.56], ["FORMANDO UN|CIRCULO", "white,yellow", 3.56, 4.68],
    ["Y TODAVIA|NADIE SABE", "white,yellow", 4.68, 5.90], ["QUE|SIGNIFICA", W, 5.90, 7.15]]),
 "g06": ("Short06_MonsterTruck", 7.15, [
    dict(t=1020.5, d=1.3, mode="fit"), dict(t=1025.0, d=1.0), dict(t=1030.0, d=1.4, mode="fit"),
    dict(t=1045.0, d=1.3), dict(t=1047.0, d=1.0), dict(t=1058.5, d=1.15)],
   [["SABIAS QUE", W, 0, 0.52], ["EN LO ALTO|DEL CAÑON", W, 0.52, 1.42], ["TE ESPERA UN", W, 1.42, 1.90],
    ["MONSTER|TRUCK", Yl, 1.90, 2.56], ["GIGANTE", Yl, 2.56, 3.50], ["PERO NO|VAS A PODER", W, 3.50, 4.48],
    ["ARRANCARLO", W, 4.48, 5.12], ["HASTA QUE|APRENDAS", W, 5.12, 5.88], ["A CLONAR|LLAVES", Yl, 5.88, 7.15]]),
 "g07": ("Short07_PistaKarts", 7.6, [
    dict(t=770.0, d=1.4), dict(t=773.0, d=1.3), dict(t=779.5, d=1.2, mode="fit"),
    dict(t=801.0, d=1.2, mode="fit"), dict(t=811.5, d=2.5, mode="fit")],
   [["SABIAS QUE", W, 0, 0.52], ["AL LADO DEL|CENTRO", W, 0.52, 1.20], ["COMERCIAL", W, 1.20, 1.68],
    ["HAY UNA PISTA|DE KARTS", "white,yellow", 1.68, 2.84], ["EN|CONSTRUCCION", W, 2.84, 3.74], ["CON|CARTELES DE", W, 3.74, 4.60],
    ["PROXIMAMENTE", Yl, 4.60, 5.60], ["HUELE A", W, 5.60, 6.00], ["GTA ONLINE", Yl, 6.00, 7.60]]),
}
for k, (name, body, clips, caps) in S.items():
    d = dict(base, voice=os.path.join(os.getcwd(), k + ".mp3"), out=P + rf"\GTA\{name}.mp4", body=body, clips=clips, caps=caps)
    json.dump(d, open(k + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    shutil.copy(k + ".json", os.path.join(P, "Herramientas", "specs", k + ".json"))
print("ok")
