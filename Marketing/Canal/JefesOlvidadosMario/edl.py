"""Lista de planos por párrafo de voz (Voz/tiempos.json, [01]..[12]).
Cada plano = (fuente, inicio_en_seg). La duración la reparte build.py en partes iguales dentro del párrafo.
Los planos 'R' son respiros: audio del juego, sin voz ni música, se insertan ANTES del párrafo.
Tramos medidos en hojas/ (contact sheets)."""

SML = 'sml_longplay'
# Tramos de Super Mario Land (longplay EV0qJd-phvQ)
TOTO, DRAG, HIYO, BIO, TATA, DAISY = 349, 698, 1203, 1728, 1752, 1800

PARRAFOS = {
    # [01] gancho: Biokinton y sus pollos, luego el final de SML
    1: [(SML, BIO), (SML, BIO + 4), (SML, BIO + 9), ('smb2_wart', 20), ('smg_kingfin', 16),
        ('sm64_eyerok', 44), ('yi_hookbill', 60), ('smrpg_smithy', 240), (SML, DAISY)],
    # [02] Wart
    2: [('smb2_wart', 10), ('smb2_wart', 22), ('ddp_mamu', 120), ('ddp_mamu', 200), ('smb2_wart', 32),
        ('smb2_wart', 44), ('smb2_wart', 56), ('smb2_wart', 4), ('pelicula_wart', 22),
        ('pelicula_wart', 28), ('pelicula_wart', 66), ('smb2_wart', 62)],
    # [03] Megaleg
    3: [('smg_megaleg', 27), ('smg_megaleg', 36), ('smg_megaleg', 42), ('smg_megaleg', 48), ('smg_megaleg', 54),
        ('smg_megaleg', 60), ('smg_megaleg', 68), ('smg_megaleg', 78), ('smg2_diggaleg', 6),
        ('smg2_diggaleg', 30), ('smg2_diggaleg', 60), ('pelicula_wart', 94)],
    # [04] Kingfin
    4: [('smg_kingfin', 16), ('smg_kingfin', 32), ('smg_kingfin', 54), ('smg_kingfin', 70),
        ('smg_kingfin', 100), ('smg_kingfin', 140), ('smg_kingfin', 152)],
    # [05] Eyerok
    5: [('sm64_eyerok', 30), ('sm64_eyerok', 40), ('sm64_eyerok', 46), ('sm64_eyerok', 52), ('sm64ds_eyerok', 10),
        ('sm64_eyerok', 80), ('sm64ds_eyerok', 25), ('mkds_eyerok', 6), ('mkds_eyerok', 40),
        ('mkds_eyerok', 64), ('mkds_eyerok', 66)],
    # [06] Hookbill
    6: [('yi_hookbill', 33), ('yi_hookbill', 45), ('yi_hookbill', 54), ('yi_hookbill', 63), ('yi_hookbill', 72),
        ('yi_hookbill', 84), ('tetris_hookbill', 40), ('tetris_hookbill', 80), ('yi_hookbill', 96),
        ('yi_hookbill', 108)],
    # [07] Smithy
    7: [('smrpg_smithy', 10), ('smrpg_smithy', 40), ('smrpg_smithy', 96), ('smrpg_smithy', 232),
        ('smrpg_smithy', 250), ('smrpg_smithy', 290), ('smrpg_smithy', 330), ('smrpg_smithy', 410),
        ('smrpg_remake', 120), ('smrpg_remake', 240), ('smrpg_remake', 360)],
    # [08] Clawgrip
    8: [('smb2_clawgrip', 4), ('smb2_clawgrip', 14), ('smb2_clawgrip', 24), ('ddp_mouser3', 6),
        ('ddp_mouser3', 20), ('ddp_mouser3', 36), ('smb2_clawgrip', 36), ('smb2_clawgrip', 48),
        ('pelicula_wart', 8), ('smb2_clawgrip', 52)],
    # [09] Tatanga
    9: [(SML, DAISY), (SML, TATA), (SML, TATA + 6), (SML, TATA + 12), ('mk8_daisy', 20), ('mk8_daisy', 60),
        ('mk8_daisy', 100), ('sml2_tatanga', 6), ('sml2_tatanga', 16), ('sml2_tatanga', 26), (SML, TATA + 20),
        (SML, 662), (SML, TATA + 28)],
    # [10] Totomesu, Dragonzamasu, Hiyoihoi
    10: [(SML, TOTO), (SML, TOTO + 4), (SML, DRAG), (SML, DRAG + 5), (SML, HIYO), (SML, HIYO + 8),
         (SML, HIYO + 16), (SML, TOTO + 2), ('odyssey_arena', 82), ('odyssey_arena', 100), (SML, DRAG + 9)],
    # [11] Biokinton: planos largos
    11: [(SML, BIO - 6), (SML, BIO), (SML, BIO + 6), (SML, BIO + 12), (SML, DAISY)],
    # [12] cierre: montaje rápido de los diez
    12: [('smb2_wart', 40), ('smg_megaleg', 54), ('smg_kingfin', 140), ('sm64_eyerok', 50), ('yi_hookbill', 72),
         ('smrpg_smithy', 260), ('smb2_clawgrip', 30), (SML, TATA + 10), (SML, HIYO + 10), (SML, BIO + 6)],
}

# Respiros: (antes del párrafo, fuente, inicio, duración) con el audio del juego
RESPIROS = [
    (2, 'smb2_wart', 60, 3.0),      # Wart cae
    (3, 'smg_megaleg', 9, 3.0),     # Bowser Jr. presenta a Megaleg
    (4, 'smg_kingfin', 16, 2.5),    # rugido del tiburón
    (5, 'sm64_eyerok', 80, 3.0),    # Eyerok habla con la voz del Rey Whomp
    (6, 'yi_hookbill', 36, 2.5),    # Hookbill aparece
    (7, 'smrpg_smithy', 232, 3.0),  # Smithy se transforma
    (8, 'smb2_clawgrip', 50, 2.5),  # Clawgrip cae
    (9, SML, TATA + 30, 3.0),       # Tatanga explota
    (10, SML, TOTO + 6, 2.5),       # Totomesu explota
    (11, SML, BIO - 2, 3.0),        # entra la nube
]
