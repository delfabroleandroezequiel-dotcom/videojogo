"""Specs de los shorts de Jekyria de noviembre 2026 (material ya grabado). Genera un .json por short."""
import json, os
H = os.path.dirname(os.path.abspath(__file__))
V = r"C:\Users\delfa\Videos"
FONT = r"C:\Users\delfa\Desktop\Videojogo\Marketing\Recursos\Fuentes\Anton-Regular.ttf"
MIDIR_REF = r"C:\Users\delfa\Downloads\Midir Is Still the Best Dragon Even After Elden Ring [D3w4oAecVE0].mp4"
MID_A = V + r"\2026-10-22 00-16-55.mp4"     # Midir sin HUD
MID_B = V + r"\2026-10-22 02-43-26.mp4"     # Midir con HUD, laser 97-99
GIG = V + r"\2026-11-05 20-35-23.mp4"       # Gigante: pecho, cuello, ojos 30-41
ART = V + r"\2026-11-16 12-08-09.mp4"       # Artemisa: flor 54.9 y 99.3
PUENTE = V + r"\2026-11-04 08-21-43.mp4"    # puente con mazas y arqueros 26-45
TOUR = V + r"\2026-11-05 20-24-21.mp4"      # puerto, Nix, bosque
W, Y = "white", "yellow"
BRIGHT = "eq=brightness=0.04:contrast=1.15:saturation=1.25"


def c(src, t, d, x, **k):
    return dict(src=src, t=t, d=d, x=x, **k)


S = {
 "Short1_Midir2D": dict(voice="voces/j1b.mp3", clips=[
    c(MIDIR_REF, 33.8, 2.3, 1100, w=1215, vol=0.0),
    c(MID_A, 38.9, 2.0, 1080, eq=BRIGHT),
    c(MID_B, 87.3, 1.6, 150, w=540, y=120, eq=BRIGHT),
    c(MID_A, 52.0, 1.4, 1300, eq=BRIGHT),
    c(MID_B, 96.6, 2.7, 200, mode="fit", w=1100, eq=BRIGHT)],
  caps=[["DARK SOULS 3\u2019S|HARDEST DRAGON", "white,yellow", 0.15, 2.45], ["BUT 2D", Y, 2.6, 3.9],
        ["SAME CAVE", W, 3.95, 5.1], ["SAME FIRE", W, 5.15, 6.4], ["AND YES...", W, 6.45, 7.35], ["THE LASER", Y, 7.4, 10.0]]),
 "Short2_GiganteOjos": dict(voice="voces/j2a.mp3", clips=[
    c(GIG, 12.6, 1.9, 620),
    c(GIG, 15.9, 1.3, 650),
    c(GIG, 28.4, 0.8, 700),
    c(GIG, 30.2, 5.0, 660, mode="fit", w=1000)],
  caps=[["THIS BOSS|IS A MOUNTAIN", "white,yellow", 0.15, 1.8], ["YOU HAVE|TO CLIMB IT", W, 2.25, 3.55],
        ["AND IT\u2019S|WATCHING YOU", "white,yellow", 3.7, 6.2]]),
 "Short3_FlorEscarlata": dict(voice="voces/j4b.mp3", clips=[
    c(ART, 54.75, 1.3, 1300),
    c(ART, 57.6, 1.7, 760),
    c(ART, 59.4, 1.6, 1000),
    c(ART, 98.4, 2.8, 250),
    c(ART, 102.2, 1.8, 500)],
  caps=[["MALENIA\u2019S|SCARLET AEONIA", "white,yellow", 0.15, 1.95], ["IN MY|2D GAME", W, 2.0, 3.4],
        ["WHEN THE|FLOWER BLOOMS", "white,yellow", 3.45, 5.2], ["RUN", Y, 5.25, 6.8]]),
 "Short4_PuenteTrampas": dict(voice="voces/j5a.mp3", clips=[
    c(PUENTE, 37.6, 1.6, 600, eq=BRIGHT),
    c(PUENTE, 27.0, 1.6, 600, eq=BRIGHT),
    c(PUENTE, 29.7, 2.0, 600, eq=BRIGHT),
    c(PUENTE, 33.8, 2.0, 600, eq=BRIGHT),
    c(PUENTE, 40.4, 2.0, 600, eq=BRIGHT)],
  caps=[["WOULD YOU MAKE IT|ACROSS THIS BRIDGE?", "white,yellow", 0.15, 1.8], ["ARROWS", Y, 2.35, 3.25],
        ["SPIKED MACES", Y, 3.3, 4.6], ["ONE WRONG|STEP", "white,yellow", 5.0, 6.8]]),
 "Short5_MundoSolo": dict(voice="voces/j6b.mp3", music=dict(src=r"C:\Users\delfa\Desktop\Videojogo\assets\audio\Musica\Llamas\HuidaLlamas.mp3", t=20, vol=0.55),
  clips=[
    c(GIG, 30.9, 0.9, 1000, vol=0),
    c(ART, 99.2, 0.9, 250, vol=0),
    c(MID_B, 97.2, 0.9, 700, vol=0, eq=BRIGHT),
    c(TOUR, 86.0, 0.9, 600, vol=0),
    c(TOUR, 96.5, 0.9, 600, vol=0),
    c(TOUR, 155.5, 0.9, 600, vol=0),
    c(MID_A, 39.3, 0.9, 1080, vol=0, eq=BRIGHT),
    c(TOUR, 225.0, 0.9, 650, vol=0),
    c(GIG, 13.0, 1.0, 620, vol=0),
    c(GIG, 36.4, 1.3, 660, mode="fit", w=1000, vol=0)],
  caps=[["ONE DEVELOPER", W, 0.15, 1.3], ["ONE YEAR", Y, 1.4, 2.7],
        ["THIS IS THE WORLD|I\u2019M BUILDING", "white,yellow", 2.8, 4.95], ["ALONE", Y, 5.0, 6.8]]),
}
for k, d in S.items():
    d = dict(d, font=FONT, voice=os.path.join(H, d["voice"]), out=os.path.join(H, k + ".mp4"), voice_start=0.15)
    json.dump(d, open(os.path.join(H, k + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("ok")
