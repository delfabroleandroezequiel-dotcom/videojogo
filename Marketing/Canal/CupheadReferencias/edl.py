# Lista de planos del largo "Los DIBUJOS OLVIDADOS detrás de cada jefe de Cuphead".
# Cada plano: (fin_en_timeline, fuente, entrada_en_fuente, etiqueta o None). El plano empieza donde terminó el anterior.
# Fuentes: CUP = partida de Cuphead (4K); el resto son ids de fuentes/<id>.mp4. Tiempos de la voz: Voz/p1a.txt y p2a.txt.
CUP = "kXkMQvGsjTA"
SWING, HELL, SKEL, FLOW = "apRStT6zlkU", "vNMxnK8dM9U", "h03QBNVwX8Q", "_NKcsg8vE_U"
OLD, TAILOR, MINNIE, SNOWW = "v1iujW0Wz2k", "22Fh-AB2pRQ", "SdBedVCBuPY", "cKOSJ5AAwfc"
MIRROR, COOKIE, MOTH, TORT = "yUDc78znrLM", "SRB2YlQOSBI", "YouBj4K7104", "2DrKmpuKhKE"
COBWEB, ALADDIN, SINDBAD = "JcPpHSSZIdQ", "AlnZq_D_tAg", "QIwUpvf2jXY"
SNOWMAN, BIMBO, ANTS, ENEMY = "CH0cTe0WSDM", "FSm07stzHZQ", "goqpnR2Kfg4", "MUWGXM6rld8"

L = {SWING: "Swing You Sinners! (1930)", HELL: "Hell's Bells (1929)", SKEL: "The Skeleton Dance (1929)",
     FLOW: "Flowers and Trees (1932)", OLD: "The Old Man of the Mountain (1933)", TAILOR: "Brave Little Tailor (1938)",
     MINNIE: "Minnie the Moocher (1932)", SNOWW: "Snow-White (1933)", MIRROR: "Thru the Mirror (1936)",
     COOKIE: "The Cookie Carnival (1935)", MOTH: "The Moth and the Flame (1938)", TORT: "The Tortoise and the Hare (1935)",
     COBWEB: "The Cobweb Hotel (1936)", ALADDIN: "Aladdin and His Wonderful Lamp (1939)",
     SINDBAD: "Popeye the Sailor Meets Sindbad (1936)", SNOWMAN: "The Snowman (1933)", BIMBO: "Bimbo's Initiation (1931)",
     ANTS: "Ants in the Plants (1940)", ENEMY: "The Public Enemy (1931)"}

def C(end, t): return (end, CUP, t, None)
def O(end, src, t): return (end, src, t, L[src])

SHOTS = [
 # Gancho
 C(5.2, 5996.0), O(10.8, OLD, 261.0),
 C(12.2, 4790.0), O(13.6, HELL, 291.0), C(15.0, 1110.6), O(16.4, FLOW, 157.0),
 C(17.8, 3910.0), O(19.2, SKEL, 166.0), C(20.5, 4709.0), O(21.8, MINNIE, 75.0),
 # 1. Swing You Sinners
 C(28.3, 40.0), O(34.1, SWING, 20.0), O(39.0, SWING, 52.0), O(42.3, SWING, 298.0),
 # 2. Cagney, el baile
 C(47.6, 1110.4), O(54.2, SWING, 363.0),
 # 3. Cagney, el nombre
 C(58.0, 1146.0), O(62.0, ENEMY, 30.8), C(65.1, 1160.0),
 # 4. Flowers and Trees
 C(69.7, 1131.0), O(74.0, FLOW, 40.0), O(78.7, FLOW, 157.0),
 # 5. Goopy
 C(81.9, 778.0), C(84.9, 803.0), O(88.5, SWING, 232.0), O(91.6, SWING, 380.0),
 # 6. Phantom Express
 C(96.4, 3910.0), O(102.4, SKEL, 155.0), C(105.4, 3933.0),
 # 7. Bonbon
 C(110.0, 1708.0), O(113.2, COOKIE, 21.9), O(115.8, COOKIE, 240.0), C(118.2, 1643.5),
 # 8. Cala Maria
 C(123.0, 3754.0), O(126.9, SNOWW, 87.0), O(131.8, MOTH, 117.0),
 # 9. La tortuga
 C(134.4, 3773.0), O(139.8, TORT, 145.0),
 # 10. Ribby y Croaks
 C(143.6, 483.5), O(147.8, COBWEB, 100.0), O(151.6, COBWEB, 238.0), O(153.4, COBWEB, 120.0),
 # 11. Djimmi (p2a +153.36)
 C(158.4, 1403.5), O(162.5, ALADDIN, 689.0), O(165.9, ALADDIN, 742.0),
 # 12. Brineybeard
 C(169.6, 3489.5), O(173.9, SINDBAD, 875.0),
 # 13. King Dice
 C(178.8, 4709.5), O(184.0, MINNIE, 75.0), O(187.5, SNOWW, 297.0), C(189.6, 4733.0),
 # 14. Cartas
 C(193.5, 4712.0), O(198.1, MIRROR, 263.0),
 # 15. Mortimer
 C(203.3, 5843.0), O(207.0, SNOWMAN, 222.0), O(210.2, SNOWMAN, 307.0),
 # 16. El culto
 C(214.5, 5823.0), O(219.5, BIMBO, 181.0), O(223.3, BIMBO, 194.0), O(227.06, BIMBO, 372.0),
 # 17. Saltbaker (toma nueva salt_b)
 C(230.5, 6838.0), C(233.3, 6893.0), O(236.4, BIMBO, 143.0),
 # 18. Moonshine Mob (p2a +149.54)
 C(241.5, 5367.0), O(245.0, ANTS, 211.0), O(247.9, ANTS, 315.5),
 # 19. Glumstone
 C(251.5, 5975.5), O(255.2, OLD, 216.5), O(259.0, TAILOR, 295.0), O(261.6, TAILOR, 441.0),
 # 20. El Diablo
 C(266.5, 4765.0), C(268.7, 4799.0), O(272.0, HELL, 262.0), O(275.0, HELL, 291.0),
 (279.4, "ACH", 4911.0, None), O(283.1, SWING, 19.5),
 # Cierre
 C(287.6, 4934.0),
]
TOTAL = 287.6
