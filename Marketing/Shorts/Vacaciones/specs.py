"""Shorts de Jekyria, lote vacaciones (2026-11-25). Render con ../Noviembre/render_jk.py.
Voz: Lily Wolff (qBDvhofpxp92JgXJxDjB), eleven_v4, una toma por short (voces/k1-k5.mp3).
Fuentes nuevas (no usadas en Noviembre):
  OPEN = 2026-11-24 22-40-24  pantalla de titulo, entrenamiento con Arthur (parry 62-84), aldea en llamas (203-237)
  ART  = 2026-11-16 11-52-01  arena de dia de Artemisa (estatuas, arbol rojo, pilares carmesi 56-85)
  GIG  = 2026-11-05 20-34-05  Gigante: trepada por el pecho con laser y orbes verdes
Recortes 9:16 con y>=80 y alto <=853: dejan afuera los carteles de controles en espanol (arriba)
y los dialogos en espanol + "Activar Windows" (abajo)."""
import json, os
H = os.path.dirname(os.path.abspath(__file__))
V = r"C:\Users\delfa\Videos"
FONT = r"C:\Users\delfa\Desktop\Videojogo\Marketing\Recursos\Fuentes\Anton-Regular.ttf"
OPEN = V + r"\2026-11-24 22-40-24.mp4"
ART = V + r"\2026-11-16 11-52-01.mp4"
GIG = V + r"\2026-11-05 20-34-05.mp4"
W, Y, WY = "white", "yellow", "white,yellow"
BRIGHT = "eq=brightness=0.05:contrast=1.15:saturation=1.25"


def c(src, t, d, x, w=450, h=800, y=130, **k):
    return dict(src=src, t=t, d=d, x=x, w=w, h=h, y=y, **k)


def a(t, d, x):  # Artemisa: un poco mas grande
    return c(ART, t, d, x, w=480, h=853, y=80)


def a2(t, d, x):  # tramos con dialogo en espanol abajo (y>~870): cortar antes
    return c(ART, t, d, x + 20, w=416, h=740, y=130)


S = {
 "Short1_Apertura": dict(voice="voces/k1.mp3", clips=[
    dict(src=OPEN, t=2.0, d=1.4, x=0, w=1100, mode="fit"),
    c(OPEN, 50.0, 1.5, 900),
    c(OPEN, 56.0, 1.0, 670),
    c(OPEN, 225.0, 1.3, 600, eq=BRIGHT),
    c(OPEN, 227.0, 1.4, 1000, eq=BRIGHT),
    c(OPEN, 235.6, 1.4, 300, eq=BRIGHT)],
  caps=[["MY GAME STARTS WITH|A SWORD LESSON", W, 0.15, 1.95], ["FROM YOUR|OWN TEACHER", W, 2.3, 3.3],
        ["THEN YOUR WHOLE|VILLAGE BURNS", WY, 3.89, 7.6]]),
 "Short2_ParryMaestro": dict(voice="voces/k2.mp3", clips=[
    c(OPEN, 64.2, 1.6, 620), c(OPEN, 66.0, 1.4, 650), c(OPEN, 68.5, 1.2, 800),
    c(OPEN, 76.2, 1.6, 780), c(OPEN, 83.4, 1.7, 880)],
  caps=[["YOUR FIRST BOSS|IS YOUR TEACHER", WY, 0.15, 2.0], ["AND HE WON\u2019T|GO DOWN", W, 2.25, 3.6],
        ["UNTIL YOU|LEARN TO PARRY", WY, 3.65, 7.2]]),
 "Short3_ArtemisaEspera": dict(voice="voces/k3.mp3", clips=[
    a2(17.0, 1.5, 640), a2(27.0, 1.5, 700), a2(36.0, 1.4, 700), a2(44.0, 1.0, 650), a2(45.6, 1.6, 1250)],
  caps=[["SHE HAS WAITED|FOR CENTURIES", WY, 0.15, 2.8], ["NO BATTLE HAS EVER|SEEN HER FALL", WY, 3.1, 6.8]]),
 "Short4_PilaresCarmesi": dict(voice="voces/k4.mp3", clips=[
    a(57.5, 1.3, 150), a(62.0, 1.3, 700), a(74.0, 1.4, 550), a(77.0, 1.2, 1150), a(81.5, 1.8, 420)],
  caps=[["A VALKYRIE WHO|FILLS THE SKY", W, 0.15, 2.0], ["WITH CRIMSON|PILLARS", WY, 2.0, 3.4],
        ["COULD YOU|DODGE THEM ALL?", WY, 3.6, 6.8]]),
 "Short5_GiganteNivel": dict(voice="voces/k5.mp3", clips=[
    c(GIG, 12.0, 1.4, 750, w=608, h=1080, y=0), c(GIG, 15.0, 1.2, 650, w=608, h=1080, y=0),
    dict(src=GIG, t=22.0, d=1.4, x=300, w=1300, mode="fit"),
    c(GIG, 35.0, 1.3, 800, w=608, h=1080, y=0),
    dict(src=GIG, t=47.0, d=1.7, x=300, w=1300, mode="fit")],
  caps=[["THIS ISN\u2019T|A LEVEL", W, 0.15, 1.5], ["IT\u2019S A GIANT", Y, 1.8, 3.1],
        ["AND IT\u2019S SHOOTING|AT YOU", W, 3.2, 4.4], ["WHILE YOU CLIMB", Y, 4.4, 6.8]]),
}
for k, s in S.items():
    d = dict(s, font=FONT, out=os.path.join(H, k + ".mp4"), voice=os.path.join(H, s["voice"]), voice_start=0.15)
    json.dump(d, open(os.path.join(H, k + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
