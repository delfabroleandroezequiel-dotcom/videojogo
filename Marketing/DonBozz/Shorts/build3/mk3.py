r"""Shorts A de Don Bozz, lote 3 (2026-11-25): 3 de Patricio (largo del 29 nov) + 2 del Chavo (largo de dic).
Frases propias con la voz del canal ("Bozz Killer" seOn15qLHcKLOg5XXTjt, eleven_v3, flow "Don Bozz - Shorts"), tomas en voz/.
Planos de las fuentes limpias (sin textos del largo): SP = uRLmcO5ffVk (16:9 HD, el logo de Nick arriba-der queda fuera del 1440),
NAV = tuieZWj9lq4 (4:3 con bordes decorados, el recorte 1440 en x=240 los saca). Reusa build() de build2/mk2.py."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "build2"))
from mk2 import build
DL = r"C:\Users\delfa\Downloads"
SP = os.path.join(DL, "Patricio", "uRLmcO5ffVk.mp4")
NAV = os.path.join(DL, "Chavo", "tuieZWj9lq4.mp4")
V = lambda n: os.path.join(HERE, "voz", n + ".mp3")
NL = "\\N"
S = {
 "Short_A_Patricio_EligioTonto": dict(vsrc=SP, voice=[(V("p1"), 0.0, 7.75)], mus=5.0,
   shots=[(386.0, 2.0), (412.0, 1.9), (562.0, 1.8), (570.0, 2.05)],
   cards=[(0, 2.95, f"Patricio tuvo la oportunidad{NL}de ser el más inteligente"), (2.95, 4.2, "de Fondo de Bikini."),
          (4.2, 6.0, f"Y eligió volver{NL}a ser tonto…"), (6.0, 7.75, f"para no perder{NL}a su amigo.")]),
 "Short_A_Patricio_LoQueImporta": dict(vsrc=SP, voice=[(V("p2"), 0.0, 7.0)], mus=18.0,
   shots=[(348.0, 1.8), (376.0, 1.6), (566.0, 1.8), (576.0, 1.8)],
   cards=[(0, 2.9, f"De chico pensabas que Patricio{NL}era el tonto del grupo."), (2.9, 5.0, f"De grande entiendes{NL}que era el único que sabía"),
          (5.0, 7.0, "lo que de verdad importa.")]),
 "Short_A_Patricio_SeQuedoSolo": dict(vsrc=SP, voice=[(V("p3"), 0.0, 7.3)], mus=33.0,
   shots=[(388.0, 1.9), (500.0, 1.9), (452.0, 1.4), (530.0, 2.1)],
   cards=[(0, 1.95, f"Cuando Patricio{NL}se volvió un genio,"), (1.95, 4.1, f"empezó a tratar a todos{NL}desde arriba,"),
          (4.1, 5.2, "igual que Calamardo."), (5.2, 7.3, f"Y en un solo día…{NL}se quedó solo.")]),
 "Short_A_Chavo_TortaDeJamon": dict(vsrc=NAV, voice=[(V("c1"), 0.0, 7.0)], mus=10.0,
   shots=[(117.2, 1.75), (124.0, 1.6), (127.0, 1.7), (108.0, 1.95)],
   cards=[(0, 2.85, f"El Chavo le pidió a Santa Claus{NL}una torta de jamón."), (2.85, 3.8, "No un juguete."), (3.8, 4.55, "La cena."),
          (4.55, 7.0, f"Y de chicos,{NL}eso nos parecía un chiste.")]),
 "Short_A_Chavo_NinoPobre": dict(vsrc=NAV, voice=[(V("c2"), 0.0, 7.3)], mus=45.0,
   shots=[(808.0, 1.9), (826.0, 1.5), (856.5, 1.8), (871.0, 2.1)],
   cards=[(0, 2.95, f"El Chavo recibió un solo regalo{NL}esa Navidad."), (2.95, 5.15, f"Y se lo dio al bebé{NL}de la portera."),
          (5.15, 7.3, f"Porque, como él dijo,{NL}era un niño pobre.")]),
}
if __name__ == "__main__":
    os.chdir(HERE)
    for n in (sys.argv[1:] or S):
        build(n, S[n])
