"""Planos por párrafo de voz (Voz/tiempos.json, [01]..[12]) para "Los jefes de Sonic que NADIE recuerda".
Cada plano = (fuente, inicio_s); build.py reparte la duración en partes iguales. Tramos medidos en hojas/.
Largos de las fuentes: tt_fang 166, sr_erazor 182, heroes 236, sky_witchcart 43, rivals2_ifrit 339 (pelea 6–270)."""

PARRAFOS = {
    # [01] gancho: Ifrit
    1: [('rivals2_ifrit', 8), ('rivals2_ifrit', 40), ('rivals2_ifrit', 80), ('unl_darkgaia', 60), ('sr_erazor', 40),
        ('heroes_metalmadness', 70), ('boom_lyric', 30), ('sky_witchcart', 10), ('rivals2_ifrit', 130)],
    # [02] Fang
    2: [('tt_fang', 6), ('tt_fang', 20), ('tt_fang', 40), ('tt_fang', 60), ('tt_fang', 80), ('fighters_fang', 30),
        ('fighters_fang', 120), ('fighters_fang', 300), ('superstars_fang', 30), ('superstars_fang', 270),
        ('superstars_fang', 330), ('superstars_fang', 420)],
    # [03] Dark Gaia (+ Terranigma)
    3: [('unl_darkgaia', 0), ('unl_darkgaia', 20), ('unl_darkgaia', 45), ('unl_darkgaia', 90), ('unl_darkgaia', 130),
        ('terranigma_darkgaia', 220), ('terranigma_darkgaia', 300), ('unl_darkgaia', 170), ('unl_darkgaia', 210),
        ('unl_darkgaia', 250), ('unl_darkgaia', 300), ('unl_darkgaia', 340), ('unl_darkgaia', 380), ('unl_darkgaia', 420)],
    # [04] Erazor Djinn
    4: [('sr_erazor', 4), ('sr_erazor', 14), ('sr_erazor', 30), ('sr_erazor', 50), ('sr_erazor', 70), ('sr_erazor', 90),
        ('sr_erazor', 110), ('sr_erazor', 135), ('sr_erazor', 160)],
    # [05] Metal Madness
    5: [('heroes_metalmadness', 12), ('heroes_metalmadness', 20), ('heroes_metalmadness', 40), ('heroes_metalmadness', 64),
        ('heroes_metalmadness', 90), ('heroes_metalmadness', 120), ('heroes_metalmadness', 150),
        ('heroes_metalmadness', 175), ('heroes_metalmadness', 200), ('heroes_metalmadness', 220)],
    # [06] Caballero Negro
    6: [('bk_bosses', 184), ('bk_bosses', 176), ('bk_bosses', 240), ('bk_bosses', 252), ('bk_bosses', 264),
        ('bk_bosses', 276), ('bk_bosses', 288), ('bk_bosses', 296)],
    # [07] Nega-Mother Wisp
    7: [('colorsds_negawisp', 90), ('colorsds_negawisp', 120), ('colorsds_negawisp', 150), ('colorsds_negawisp', 160),
        ('colorsds_negawisp', 230), ('colorsds_negawisp', 240), ('colorsds_negawisp', 270), ('colorsds_negawisp', 300),
        ('colorsds_negawisp', 330)],
    # [08] Lyric
    8: [('boom_lyric', 16), ('boom_lyric', 40), ('boom_lyric', 120), ('boom_lyric', 200), ('boom_lyric', 280),
        ('boom_lyric', 360), ('boom_lyric', 440), ('boom_lyric', 520)],
    # [09] Scorpius
    9: [('spinball_bosses', 22), ('spinball_bosses', 26), ('spinball_bosses', 30), ('spinball_bosses', 34),
        ('spinball_bosses', 38), ('spinball_bosses', 42), ('spinball_bosses', 46), ('spinball_bosses', 50),
        ('spinball_bosses', 54)],
    # [10] Witchcart
    10: [('sky_darkcastle', 20), ('sky_witchcart', 2), ('sky_witchcart', 6), ('sky_witchcart', 10), ('sky_witchcart', 14),
         ('sky_witchcart', 18), ('sky_witchcart', 22), ('sky_witchcart', 26), ('sky_witchcart', 30)],
    # [11] Ifrit: planos largos
    11: [('rivals2_ifrit', 6), ('rivals2_ifrit', 50), ('rivals2_ifrit', 100), ('rivals2_ifrit', 160), ('rivals2_ifrit', 210),
         ('rivals2_ifrit', 245)],
    # [12] cierre: montaje rápido
    12: [('tt_fang', 50), ('unl_darkgaia', 100), ('sr_erazor', 60), ('heroes_metalmadness', 130), ('bk_bosses', 270),
         ('colorsds_negawisp', 200), ('boom_lyric', 300), ('spinball_bosses', 40), ('sky_witchcart', 20),
         ('rivals2_ifrit', 150)],
}

# Respiros: (antes del párrafo, fuente, inicio, duración) con el audio del juego
RESPIROS = [
    (2, 'tt_fang', 6, 3.0),
    (3, 'unl_darkgaia', 0, 3.0),
    (4, 'sr_erazor', 14, 3.0),
    (5, 'heroes_metalmadness', 70, 3.0),
    (6, 'bk_bosses', 276, 2.5),
    (7, 'colorsds_negawisp', 100, 2.5),
    (8, 'boom_lyric', 8, 3.0),
    (9, 'spinball_bosses', 24, 2.5),
    (10, 'sky_witchcart', 4, 2.5),
    (11, 'rivals2_ifrit', 6, 3.0),
    (12, 'rivals2_ifrit', 262, 3.5),   # Ifrit cae: respiro antes del cierre (lleva el corte a >6 min)
]
