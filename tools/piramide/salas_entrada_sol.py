# La Pirámide — salas de la entrada, el hub y la Rama del Sol.
# Coordenadas de MUNDO (y hacia abajo). y = 0 es el piso del Pórtico (la superficie del desierto).
# Cada sala lleva su idea y sus 4 tiempos (presentación segura, desarrollo, giro, cierre) en notas.
from piramide_lib import Room, NP, Res, link, EXTERNAL

SOL = (1.0, 0.8, 0.38, 1)

# ==================================================================================================
# P01 · PÓRTICO — la escala de la pirámide, silencio, y la primera vista de su lenguaje:
# la luz del sol marca lo importante (un rayo que no quema cae sobre un cofre chico).
# Cerraduras a la vista: la trampilla del atajo (se abre desde abajo) y un nicho tapado por un
# muro cargado arriba a la derecha (premio de vuelta apenas Ovre te da el cargado).
# ==================================================================================================
R = Room('P01', 'Portico', 0, -1000, 2400, 200, 'Entrada desde el desierto')
R.floor(0, 1940, 0).floor(2060, 2400, 0)
R.ramp(600, 0, 920, -160, 'RampaEstrado').block(920, -160, 1480, 0, 'Estrado').ramp(1480, -160, 1800, 0, 'RampaEstrado')
R.ent('rayo', 'LuzDelPortico', 1200, -990, Direction=(0, 1), Hurts=False, Mode=0)
R.cofre(1200, -160, 80, 'CofreDeLaLuz')
R.shelf(980, 1100, -300, 'Columna').shelf(1300, 1420, -440, 'Columna').block(1600, -580, 2400, -540, 'CornisaAlta')
R.ceil(1600, 2400, -780, 'TechoNicho')
R.ent('muro', 'MuroNichoPortico', 2240, -680, Width=40.0, Height=200.0, CustomPersistenceId='piramide_muro_portico')
R.cofre(2330, -580, 400, 'CofreNicho')
R.door('TrampillaAtajo', 2000, 16, 120, horizontal=True, pid='piramide_atajo_portico')
R.ent('escorpion', 'Escorpion', 420, -30)
R.ent('bicho', 'BichoArena', 2250, -30)
R.note(150, -400, 'P01 PÓRTICO. Presentación: la luz del sol marca lo importante (rayo sin daño sobre un cofre chico). '
       'Trampilla de la derecha = atajo desde el Pozo de Sogas (se abre desde abajo). Nicho arriba: muro cargado (vuelta tras Ovre).')

EXTERNAL['CaminoDesierto'] = ('res://scenes/world/Mapas/CaminoDesierto.tscn', {'spawn': (833, 228)})
link('P01', 'W', 0, 'CaminoDesierto', 'X', name_a='HaciaDesierto')

# ==================================================================================================
# P02 · GALERÍA DE LOS DARDOS — idea: las placas disparan dardos.
# Abajo (pasillo): 1) placa VISIBLE y un dardo solo, de frente (presentación) · 2) placas ocultas,
# andanadas · 3) giro: la andanada viene de ATRÁS mientras un escorpión tapa el frente ·
# 4) cierre: la última placa suelta un relevo de 3 andanadas y la salida es tirarse por el hueco.
# Arriba (galería): ruta alternativa más lenta, murciélagos y un cofre de miga; una losa basculante
# disimulada te tira al pasillo de abajo justo sobre las placas (presentación segura de la losa).
# ==================================================================================================
R = Room('P02', 'GaleriaDardos', 2400, -700, 5600, 100, 'Placas y dardos, dos pisos')
R.floor(2400, 5290, 0).floor(5410, 5600, 0)
R.block(2700, -260, 4440, -220, 'LosaGaleria').block(4560, -260, 5100, -220, 'LosaGaleria')
R.ent('losa', 'LosaFalsa', 4500, -260, Width=120.0, Thickness=40.0, Disimulada=True, FloorColor=(0.42, 0.34, 0.23, 1))
R.shelf(2450, 2650, -130, 'Escalon')
R.ceil(2400, 5600, -560, 'TechoGaleria')
# 1) presentación
R.ent('placa', 'PlacaVisible', 3000, 0, Targets=NP('DardoSolo'), Delay=0.25)
R.ent('flecha', 'DardoSolo', 3560, -40, Direction=(-1, 0), OnlyWhenActivated=True, VolleyCount=1, Damage=12, MaxDistance=900.0, ProjectileSpeed=360.0)
# 2) desarrollo: ocultas
R.ent('placa', 'PlacaOculta', 3700, 0, Targets=NP('DardosFrente'), Hidden=True, OneShot=False)
R.ent('flecha', 'DardosFrente', 4300, -40, Direction=(-1, 0), OnlyWhenActivated=True, VolleyCount=3, Damage=12, MaxDistance=1000.0)
# 3) giro: desde atrás
R.ent('placa', 'PlacaOculta', 4150, 0, Targets=NP('DardosAtras'), Hidden=True, OneShot=False)
R.ent('flecha', 'DardosAtras', 3300, -40, Direction=(1, 0), OnlyWhenActivated=True, VolleyCount=3, Damage=12, MaxDistance=1400.0)
R.ent('escorpion', 'EscorpionPasillo', 4400, -30)
# 4) cierre: relevo de andanadas, la salida es el hueco
R.ent('placa', 'PlacaFinal', 4750, 0, Targets=NP('RelevoFinal'), Hidden=True)
R.ent('relevo', 'RelevoFinal', 4750, -100, Targets=NP('DardosCierre', 'DardosCierre2'), Pulses=3, PulseInterval=0.7)
R.ent('flecha', 'DardosCierre', 4200, -40, Direction=(1, 0), OnlyWhenActivated=True, VolleyCount=2, Damage=12, MaxDistance=1300.0)
R.ent('flecha', 'DardosCierre2', 4200, -100, Direction=(1, 0), OnlyWhenActivated=True, VolleyCount=1, Damage=12, MaxDistance=1300.0)
R.cofre(4000, -260, 150, 'CofreGaleria')
R.ent('murcielago', 'Murcielago', 3600, -450)
R.ent('murcielago', 'Murcielago', 4800, -430)
R.note(2500, -680, 'P02. Abajo: placa visible → dardo solo; placas ocultas; andanada desde atrás + escorpión; '
       'relevo final y te tirás por el hueco. Arriba: ruta lenta con murciélagos y cofre; la losa disimulada te tira al pasillo.')
link('P01', 'E', 0, 'P02', 'W')

# ==================================================================================================
# P03 · RAMPA DE LA ROCA — idea: la roca rodante (Indiana), bajada en diagonal 2:1 hacia el oeste.
# 1) llegás arriba: una placa visible y la roca asomando en su nicho (se lee antes de pisar) ·
# 2) bajás con la roca detrás: refugio a 150 px en el descanso · 3) giro: la segunda rampa tiene una
# roca en bucle que baja sola, se cruza leyendo el ritmo · 4) cierre: la zanja final se traga la
# roca. Secreto con propósito: una losa disimulada en el último descanso baja a una cámara con cofre
# (y escalera de vuelta): es la primera vez que la losa premia en vez de castigar.
# ==================================================================================================
R = Room('P03', 'RampaRoca', 2400, 100, 5600, 1400, 'Roca rodante en diagonal')
R.floor(5000, 5600, 300, 'Descanso1')
R.ramp(5000, 300, 4400, 600, 'Rampa1')
R.floor(3900, 4400, 600, 'Descanso2')
R.shelf(4060, 4240, 450, 'Refugio')
R.ramp(3900, 600, 3100, 1000, 'Rampa2')
R.floor(3000, 3100, 1080, 'ZanjaFondo')   # la zanja (3000..3100) se traga la roca
R.floor(2900, 3000, 1000, 'Descanso3')
# último descanso con cámara secreta debajo
R.block(2400, 1000, 2480, 1060, 'Descanso3').block(2560, 1000, 2600, 1060, 'Descanso3').block(2700, 1000, 2900, 1060, 'Descanso3')
R.shelf(2480, 2560, 1000, 'SalidaCamara')
R.ent('losa', 'LosaSecreta', 2650, 1000, Width=100.0, Thickness=60.0, Disimulada=True, FloorColor=(0.42, 0.34, 0.23, 1))
R.block(2400, 1060, 2480, 1400, 'ParedCamara').floor(2480, 2900, 1300, 'PisoCamara')
R.ladder(2520, 990, 1300, 'EscaleraCamara')
R.cofre(2800, 1300, 300, 'CofreCamara')
R.ceil(5420, 5600, 150, 'TechoNicho')
R.ent('placa', 'PlacaRoca', 4930, 300, Targets=NP('RocaPerseguidora'), Delay=0.1)
R.ent('roca', 'RocaPerseguidora', 5480, 246, Trigger=1, Direction=-1, Speed=300.0, RespawnDelay=2.0)
R.ent('roca', 'RocaBucle', 3960, 546, Trigger=0, Direction=-1, Speed=280.0, RespawnDelay=2.6, WarningTime=0.9)
R.ent('escorpion', 'Escorpion', 2780, 960)
R.note(4400, 150, 'P03. Placa visible en el descanso de arriba → la roca baja detrás tuyo: refugio a 150 px en el descanso 2. '
       'Rampa 2: roca en bucle, cruzar leyendo el ritmo. La zanja final se traga la roca. Losa disimulada en el último descanso: cámara con cofre.')
link('P02', 'B', 5350, 'P03', 'T', spawn_b=(5350, 240))

# ==================================================================================================
# P04 · POZO DE LAS SOGAS — idea: la cascada de arena (nueva). Bajada vertical con cuerdas.
# 1) presentación: la primera cascada cae sobre el descanso donde aterrizás (te empuja contra el
# piso: no pasa nada) · 2) desarrollo: una cascada cae SOBRE la cuerda: si te agarra colgado te
# suelta al foso de pinchos → esperar a que pare · 3) giro: el foso del fondo · 4) cierre: palanca
# al pie que abre la chimenea de la escalera (atajo de vuelta arriba, desde el otro lado).
# Arriba del descanso de llegada: escalera al rellano de la trampilla y su palanca (atajo al Pórtico).
# ==================================================================================================
R = Room('P04', 'PozoSogas', 1600, 200, 2400, 2400, 'Cascadas de arena y cuerdas')
R.block(1640, 380, 2160, 420, 'RellanoTrampilla')
R.ent('lever', 'PalancaTrampilla', 1780, 380, SingleUse=True)
R.door('TrampillaAtajo', 2000, 266, 120, horizontal=True, pid='piramide_atajo_portico')
R.connect('PalancaTrampilla', 'Activated', 'TrampillaAtajo', 'Open')
R.ladder(2200, 370, 1000, 'EscaleraRellano')
R.block(2100, 1000, 2280, 1040, 'DescansoLlegada').shelf(2280, 2400, 1000, 'BocaChimenea')
R.ent('cuerda', 'Cuerda1', 1880, 1040, Length=220.0)
R.block(1640, 1300, 1760, 1340, 'DescansoB')
R.block(2100, 1300, 2280, 1340, 'Canal')
R.ent('cascada', 'CascadaEnsenanza', 2190, 1340, Height=280.0, Width=70.0, OffTime=2.0, PourTime=2.0)
R.block(2040, 1620, 2280, 1660, 'DescansoC')
R.block(1760, 1600, 1880, 1640, 'VigaCuerda')
R.ent('cuerda', 'Cuerda2', 1820, 1640, Length=260.0)
R.ent('cascada', 'CascadaCuerda', 1820, 1640, Height=660.0, Width=60.0, OffTime=2.4, WarningTime=0.8, PourTime=1.8)
R.block(1640, 1960, 1740, 2000, 'DescansoD')
R.floor(1600, 2400, 2300, 'Fondo')
R.pinchos(1780, 2060, 2300)
# chimenea del atajo (escalera encerrada contra la pared este)
R.block(2280, 1040, 2320, 2100, 'ParedChimenea')
R.ladder(2360, 990, 2300, 'EscaleraChimenea')
R.door('PuertaChimenea', 2300, 2200, 200, pid='piramide_chimenea_sogas')
R.ent('lever', 'PalancaChimenea', 2230, 2300, SingleUse=True)
R.connect('PalancaChimenea', 'Activated', 'PuertaChimenea', 'Open')
R.ent('murcielago', 'Murcielago', 1900, 800)
R.note(1640, 1100, 'P04. Cascada 1 sobre el descanso C (presentación, inofensiva). Cascada 2 sobre la cuerda 2: te suelta al foso de pinchos. '
       'Abajo: palanca que abre la chimenea con escalera (vuelta). Arriba: escalera al rellano y palanca de la trampilla al Pórtico.')
link('P03', 'W', 1000, 'P04', 'E')
link('P01', 'B', 2000, 'P04', 'T', spawn_a=(2200, -40), spawn_b=(2000, 335))

# ==================================================================================================
# P05 · ANTECÁMARA — hoguera. Cerradura de Sombra a la vista (placa que solo pisa el clon) con un
# nicho y cofre: se resuelve recién después de Arkal. El piso tiene la caída a la Sala Hipóstila.
# ==================================================================================================
R = Room('P05', 'Antecamara', 400, 1800, 1600, 2400, 'Hoguera antes del hub')
R.floor(400, 940, 2300).floor(1060, 1600, 2300)
R.ceil(400, 600, 2100, 'TechoNicho')
R.save(760, 2300)
R.door('PuertaSombra', 540, 2200, 200, pid='piramide_sombra_antecamara')
R.ent('placa', 'PlacaSombra', 820, 2300, Targets=NP('PuertaSombra'), TargetMethod='Open', PressedByPlayer=False, PressedBySombra=True)
R.cofre(470, 2300, 500, 'CofreSombra')
R.note(420, 1850, 'P05. Hoguera. Placa de Sombra (solo la pisa el clon) → nicho con cofre (vuelta tras Arkal). El hueco baja al hub.')
link('P04', 'W', 2300, 'P05', 'E')

# ==================================================================================================
# P06 · SALA HIPÓSTILA (HUB) — tres alturas. Caés desde la Antecámara a un rellano central y ves
# todo: abajo el POZO DEL NÚCLEO con dos sellos (Sol y Arena) — la cerradura crítica, visible desde
# el primer segundo —, a los costados las dos ramas (oeste y este, pisos de abajo), arriba las dos
# galerías altas (segundos caminos a cada rama) y la jaula del montacargas cerrada (se abre desde
# el Relicario, al final). Escaleras en los extremos para volver a subir.
# ==================================================================================================
R = Room('P06', 'SalaHipostila', 0, 2400, 3600, 4000, 'Hub: dos ramas, sello doble abajo')
R.block(800, 2640, 1300, 2680, 'RellanoCentral')
R.block(140, 2800, 700, 2840, 'GaleriaOeste').shelf(0, 140, 2800, 'BocaEscaleraO')
R.block(2820, 2800, 3460, 2840, 'GaleriaEste').shelf(3460, 3600, 2800, 'BocaEscaleraE')
for x0 in (1460, 1800, 2140, 2480):
    R.shelf(x0, x0 + 140, 2800, 'Capitel')
R.ladder(70, 2790, 3900, 'EscaleraOeste')
R.ladder(3530, 2790, 3900, 'EscaleraEste')
R.floor(0, 1740, 3900).floor(1860, 3600, 3900)
R.ent('barrier', 'SelloDelSol', 1800, 3916, Width=120.0, Horizontal=True, NeonColor=4, RequiredKeyItemId='item_sello_sol')
R.ent('barrier', 'SelloDeArena', 1800, 3966, Width=120.0, Horizontal=True, NeonColor=5, RequiredKeyItemId='item_sello_arena')
R.ent('rayo', 'LuzDelPozo', 1800, 2815, Direction=(0, 1), Hurts=False, Mode=0, BeamWidth=14.0)
# jaula del montacargas (llega desde el Relicario)
R.block(2600, 3660, 2840, 3700, 'TechoJaula').block(2800, 3700, 2840, 3900, 'FondoJaula')
R.door('PuertaMontacargas', 2600, 3800, 200, pid='piramide_montacargas')
R.cofre(300, 2800, 120, 'CofreGaleria')
R.ent('escorpion', 'Escorpion', 900, 3860)
R.ent('escorpion', 'Escorpion', 2700, 3860)
R.note(1300, 2450, 'P06 HUB. Pozo del núcleo con 2 sellos (Sol, Arena) a la vista. Oeste abajo: Rama del Sol. Este abajo: Rama de la Arena. '
       'Galerías altas: segundo camino a cada rama. Jaula del montacargas: se abre desde el Relicario (P33).')
link('P05', 'B', 1000, 'P06', 'T', spawn_b=(1000, 2560))

# ==================================================================================================
# P39 · GALERÍA ALTA — segundo camino a la Rama del Sol, de una sola vía (punto de no retorno: te
# tirás al Pozo de Columnas). Ráfagas de viento del desierto por las troneras empujan hacia los
# fosos de arena; buitres que entran por las ventanas.
# 1) ráfaga sobre piso firme (aprendés el empuje) · 2) ráfaga junto a un foso · 3) foso + buitre ·
# 4) caída al pozo.
# ==================================================================================================
R = Room('P39', 'GaleriaAlta', -5800, 2000, 0, 2900, 'Viento y fosos de arena')
R.ceil(-5800, 0, 2560, 'TechoGaleria')
R.floor(-1200, 0, 2800).floor(-2600, -1440, 2800).floor(-3800, -2840, 2800).floor(-5360, -4040, 2800).floor(-5800, -5480, 2800)
for x in (-1440, -2840, -4040):
    R.ent('pozaarena', 'FosoArena', x, 2840, Width=240.0, Depth=60.0)
R.ent('rafaga', 'Rafaga1', -900, 2580, Size=(700.0, 220.0), Direction=-1, Strength=200.0)
R.ent('rafaga', 'Rafaga2', -2000, 2580, Size=(700.0, 220.0), Direction=-1, Strength=240.0, StartOffset=0.8)
R.ent('rafaga', 'Rafaga3', -3500, 2580, Size=(800.0, 220.0), Direction=1, Strength=240.0, StartOffset=1.4)
R.ent('buitre', 'Buitre', -3300, 2650)
R.ent('buitre', 'Buitre', -4600, 2650)
R.cofre(-5000, 2800, 180, 'CofreGaleriaAlta')
R.note(-1000, 2100, 'P39. Ráfagas por las troneras: 1) sobre piso firme, 2) junto a un foso, 3) foso + buitre. Al final te tirás al Pozo de Columnas (una vía).')
link('P06', 'W', 2800, 'P39', 'E')

# ==================================================================================================
# P07 · PATIO DEL SOL — idea: espejos. Presentación segura: un rayo baja del techo a un espejo en
# pedestal; pegarle con la espada lo gira y el rayo va al receptor que abre la puerta oeste. El
# rayo horizontal pasa por encima de la cabeza del jugador parado: quema solo si saltás (aprendés
# que quema sin que te castigue en el camino).
# ==================================================================================================
R = Room('P07', 'PatioDelSol', -2600, 2900, 0, 4000, 'Enseña el espejo')
R.floor(-2600, 0, 3900).ceil(-2600, 0, 3300, 'TechoPatio')
R.ent('rayo', 'RayoPatio', -1300, 3310, Direction=(0, 1), Mode=0)
R.ent('espejo', 'EspejoPatio', -1300, 3780, AngleDegrees=0.0, Angulos=[0, 135], PedestalHeight=120.0)
R.ent('receptor', 'ReceptorPatio', -2450, 3780, Targets=NP('PuertaPatio'), PersistenceId='piramide_receptor_patio')
R.door('PuertaPatio', -2500, 3800, 200, pid='piramide_puerta_patio')
R.ent('escorpion', 'Escorpion', -600, 3860)
R.note(-2500, 3350, 'P07. Pegale al espejo: el rayo va al receptor y abre la puerta. El rayo pasa sobre tu cabeza: quema si saltás.')
link('P06', 'W', 3900, 'P07', 'E')

# ==================================================================================================
# P08 · GALERÍA DE ESPEJOS — desarrollo: dos espejos. El rayo horizontal corre por encima de la
# rampa; para girar el segundo espejo hay que subir a una repisa donde el rayo te roza la cabeza.
# Cerradura: la trampilla del piso oeste (atajo desde el Santuario del Sol, se abre desde abajo).
# ==================================================================================================
R = Room('P08', 'GaleriaEspejos', -5000, 2900, -2600, 4400, 'Dos espejos, el rayo sobre la rampa')
R.ceil(-5000, -2600, 3260, 'TechoGaleria')
R.floor(-3400, -2600, 3900).ramp(-3400, 3900, -4200, 4300, 'RampaGaleria')
R.floor(-4540, -4200, 4300).floor(-5000, -4660, 4300)
R.door('TrampillaAtajo', -4600, 4316, 120, horizontal=True, pid='piramide_atajo_sol')
R.ent('rayo', 'RayoGaleria', -3000, 3270, Direction=(0, 1), Mode=0)
R.ent('espejo', 'Espejo1', -3000, 3780, AngleDegrees=0.0, Angulos=[0, 135], PedestalHeight=120.0)
R.shelf(-4820, -4700, 4160, 'Escalon').shelf(-4640, -4520, 4020, 'Escalon').shelf(-4700, -4500, 3870, 'RepisaEspejo')
R.ent('espejo', 'Espejo2', -4600, 3780, AngleDegrees=90.0, Angulos=[90, 45])
R.ent('receptor', 'ReceptorGaleria', -4600, 3300, Targets=NP('PuertaGaleria'), PersistenceId='piramide_receptor_galeria')
R.door('PuertaGaleria', -4900, 4200, 200, pid='piramide_puerta_galeria')
R.ent('escorpion', 'Escorpion', -4400, 4260)
R.ent('murcielago', 'Murcielago', -3800, 3500)
R.note(-4900, 3300, 'P08. Espejo 1 manda el rayo al oeste por encima de la rampa; el espejo 2 se gira desde la repisa (el rayo te roza). '
       'Receptor arriba → puerta oeste. Trampilla del piso: atajo desde el Santuario del Sol (se abre desde abajo).')
link('P07', 'W', 3900, 'P08', 'E')

# ==================================================================================================
# P09 · POZO DE COLUMNAS — bajada en zigzag entre columnas. Abajo, presentación de la GUILLOTINA:
# cae cuando pasás por debajo; caminando sin parar te cae detrás (susto sin daño). Castiga frenar.
# Arriba llega la Galería Alta (una vía).
# ==================================================================================================
R = Room('P09', 'PozoColumnas', -5800, 2900, -5000, 5400, 'Zigzag y la primera guillotina')
R.block(-5600, 3300, -5300, 3340, 'Columna1').block(-5300, 3600, -5000, 3640, 'Columna2')
R.block(-5800, 3950, -5500, 3990, 'Columna3').block(-5300, 4300, -5000, 4340, 'DescansoGaleria')
# parte baja: repisas cada 140 px alternadas (se baja cayendo y se vuelve a subir con doble salto)
for i, y in enumerate((4440, 4580, 4720, 4860, 5000, 5150)):
    x0 = -5620 if i % 2 == 0 else -5360
    R.shelf(x0, x0 + 180, y, 'RepisaColumna')
R.floor(-5800, -5000, 5300, 'Fondo')
R.block(-5800, 5060, -5640, 5100, 'TechoPasillo')
R.ent('guillotina', 'GuillotinaEnsenanza', -5720, 5100, Trigger=0, DropDistance=200.0, DetectionWidth=110.0)
R.cofre(-5180, 4300, 150, 'CofreColumna')
R.ent('murcielago', 'Murcielago', -5400, 3800)
R.note(-5780, 3020, 'P09. Zigzag entre columnas. Abajo, guillotina al pasar: caminando no te alcanza (cae detrás). Castiga frenar.')
link('P39', 'B', -5420, 'P09', 'T', spawn_b=(-5420, 3040))
link('P08', 'W', 4300, 'P09', 'E')

# ==================================================================================================
# P10 · CÁMARA DE LAS GUILLOTINAS — techo bajo. 1) una guillotina con ritmo, sola · 2) fila de tres
# desfasadas · 3) giro: un escorpión en medio de la fila (pelear bajo las hojas) · 4) cierre:
# guillotina al pasar seguida de una losa basculante sobre una zanja con pinchos: el que corre a
# ciegas para escapar de la hoja cae en la losa.
# ==================================================================================================
R = Room('P10', 'CamaraGuillotinas', -8400, 4600, -5800, 5400, 'Ritmo de guillotinas')
R.ceil(-8400, -5800, 5060, 'TechoBajo')
R.floor(-7840, -5800, 5300).floor(-8400, -7960, 5300).block(-7960, 5380, -7840, 5400, 'FondoZanja')
R.pinchos(-7960, -7840, 5380)
R.ent('guillotina', 'GuillotinaSola', -6200, 5060, Trigger=1, Interval=2.0, DropDistance=240.0)
for i, x in enumerate((-6750, -6950, -7150)):
    R.ent('guillotina', 'GuillotinaFila', x, 5060, Trigger=1, Interval=2.2, StartOffset=0.55 * i, DropDistance=240.0)
R.ent('escorpion', 'EscorpionFila', -6950, 5260)
R.ent('guillotina', 'GuillotinaCierre', -7700, 5060, Trigger=0, DropDistance=240.0)
R.ent('losa', 'LosaZanja', -7900, 5300, Width=120.0, Thickness=24.0, Disimulada=False)
R.note(-8300, 4650, 'P10. Hoja sola con ritmo → fila de 3 desfasadas → escorpión en la fila → hoja al pasar + losa sobre zanja.')
link('P09', 'W', 5300, 'P10', 'E')

# ==================================================================================================
# P11 · ESCALERA DE LOS ESCRIBAS — bajada en zigzag de cuatro rampas 2:1. En cada descanso una
# trampa de dardos dispara POR la pendiente: no se esquiva de costado, se salta o se hace parry
# (receta de PruebaDiagonal1). Cerradura de cargado: un muro tapa un nicho con cofre (vuelta tras Ovre).
# ==================================================================================================
R = Room('P11', 'EscaleraEscribas', -9600, 4600, -8400, 6600, 'Diagonales con dardos por la pendiente')
R.block(-8800, 5300, -8400, 5360, 'DescansoAlto')
R.band(-8800, 5300, -9400, 5600, 60, 'Rampa1')
R.block(-9600, 5600, -9400, 5660, 'Descanso1')
R.band(-9400, 5600, -8800, 5900, 60, 'Rampa2')
R.block(-8800, 5900, -8400, 5960, 'Descanso2')
R.band(-8800, 5900, -9400, 6200, 60, 'Rampa3')
R.block(-9600, 6200, -9400, 6260, 'Descanso3')
R.band(-9400, 6200, -8800, 6500, 60, 'Rampa4')
R.floor(-9600, -8400, 6500, 'Fondo')
R.ent('flecha', 'DardoPendiente1', -9560, 5560, Direction=(0.894, -0.447), FireInterval=2.4, Damage=14, MaxDistance=800.0)
R.ent('flecha', 'DardoPendiente2', -8760, 5860, Direction=(-0.894, -0.447), FireInterval=2.4, StartOffset=1.2, Damage=14, MaxDistance=800.0)
R.ent('flecha', 'DardoPendiente3', -9560, 6160, Direction=(0.894, -0.447), FireInterval=2.0, Damage=14, MaxDistance=800.0)
R.ent('muro', 'MuroNichoEscribas', -8600, 5750, Width=40.0, Height=300.0, CustomPersistenceId='piramide_muro_escribas')
R.cofre(-8480, 5900, 450, 'CofreNicho')
R.ent('saqueador', 'Saqueador', -9100, 6460)
R.ent('murcielago', 'Murcielago', -9000, 6050)
R.note(-9580, 4650, 'P11. Dardos disparados por la pendiente (saltar o parry). Muro cargado en el descanso 2: nicho con cofre.')
link('P10', 'W', 5300, 'P11', 'E')

# ==================================================================================================
# P12 · OBSERVATORIO — giro de los espejos: tres alturas y un rayo CON CICLO. El espejo del piso
# del medio manda el rayo por encima de ese piso: mientras está encendido, el piso del medio quema.
# Hay que girarlo y cruzar en el apagado, subir por las repisas y el espejo fijo lo lleva al receptor
# bajo el piso alto, que abre la puerta de salida.
# ==================================================================================================
R = Room('P12', 'Observatorio', -8400, 5400, -5000, 6600, 'Espejos en tres alturas con rayo intermitente')
R.floor(-8400, -5000, 6500, 'PisoBajo')
R.shelf(-7660, -7520, 6360, 'Escalon')
R.block(-7500, 6220, -6500, 6260, 'PisoMedio')
R.shelf(-6460, -6320, 6080, 'Escalon').shelf(-6100, -5960, 5940, 'Escalon')
R.block(-5800, 5800, -5000, 5840, 'PisoAlto')
R.ent('rayo', 'RayoObservatorio', -7200, 5410, Direction=(0, 1), Mode=1, OffTime=2.2, WarningTime=0.7, OnTime=2.4)
R.ent('espejo', 'EspejoMedio', -7200, 6150, AngleDegrees=0.0, Angulos=[0, 45])
R.ent('espejo', 'EspejoFijo', -5300, 6150, AngleDegrees=135.0, Rotatable=False)
R.ent('receptor', 'ReceptorObservatorio', -5300, 5880, Targets=NP('PuertaObservatorio'), PersistenceId='piramide_receptor_observatorio')
R.door('PuertaObservatorio', -5100, 5700, 200, pid='piramide_puerta_observatorio')
R.cofre(-6600, 6500, 200, 'CofreBajo')
R.ent('escorpion', 'Escorpion', -6400, 6460)
R.ent('escorpion', 'Escorpion', -7900, 6460)
R.ent('murcielago', 'Murcielago', -6600, 5900)
R.note(-8300, 5450, 'P12. Rayo intermitente. El espejo del medio manda el rayo sobre el piso del medio (quema encendido): cruzar en el apagado. '
       'El espejo fijo lo sube al receptor bajo el piso alto → puerta de salida.')
link('P11', 'E', 6500, 'P12', 'W')

# ==================================================================================================
# P13 · SANTUARIO DEL SOL — hoguera antes del jefe. Escalera al techo con palanca: abre la
# trampilla que da a la Galería de Espejos (atajo al hub). El hueco del piso baja a la arena.
# ==================================================================================================
R = Room('P13', 'SantuarioSol', -5000, 4400, -2600, 5900, 'Hoguera, atajo y bajada al jefe')
R.floor(-5000, -3660, 5800).floor(-3540, -2600, 5800)
R.save(-4300, 5800)
R.ladder(-4720, 4540, 5800, 'EscaleraAtajo')
R.block(-4680, 4560, -4400, 4600, 'RellanoAtajo')
R.ent('lever', 'PalancaAtajo', -4480, 4560, SingleUse=True)
R.door('TrampillaAtajo', -4600, 4460, 120, horizontal=True, pid='piramide_atajo_sol')
R.connect('PalancaAtajo', 'Activated', 'TrampillaAtajo', 'Open')
R.note(-4900, 4900, 'P13. Hoguera. Escalera + palanca: trampilla a la Galería de Espejos (atajo). El hueco del piso baja al Guardián del Sol.')
link('P12', 'E', 5800, 'P13', 'W')
link('P08', 'B', -4600, 'P13', 'T', spawn_a=(-4750, 4260), spawn_b=(-4550, 4520))

# ==================================================================================================
# P14 · ARENA DEL GUARDIÁN DEL SOL (jefe 1, pendiente) — caés desde el santuario. Repisas en escalera
# que sirven de plataformas en la pelea y de vuelta al santuario después.
# ==================================================================================================
R = Room('P14', 'ArenaGuardianSol', -5000, 5900, -2600, 7000, 'Arena del jefe 1')
R.floor(-5000, -2600, 6900)
R.shelf(-4900, -4700, 6750).shelf(-4560, -4400, 6600).shelf(-4200, -3900, 6450, 'RepisaAlta')
R.shelf(-3800, -3680, 6300).shelf(-3540, -3420, 6150)
R.door('PuertaTrasJefe', -2700, 6800, 200, pid='piramide_jefe_sol', starts_open=True)
R.note(-4800, 6000, 'P14. JEFE 1 (pendiente): Guardián del Sol. Repisa alta central = punto de Alathea. '
       'PuertaTrasJefe empieza abierta: cuando exista el jefe, StartsOpen=false y conectar su muerte a Open().')
link('P13', 'B', -3600, 'P14', 'T', spawn_b=(-3600, 6040))

# ==================================================================================================
# P15 · CÁMARA DEL SELLO SOLAR — premio: el Sello del Sol en un altar iluminado. Cerradura de Sombra:
# la placa del clon abre la trampilla del techo (repisas a la vista) hacia la Cámara de Sombra.
# ==================================================================================================
R = Room('P15', 'CamaraSelloSol', -2600, 5900, 0, 7000, 'Sello del Sol')
R.floor(-2600, 0, 6900).block(-1500, 6800, -1100, 6900, 'Altar')
R.ent('rayo', 'LuzDelSello', -1300, 5910, Direction=(0, 1), Hurts=False, Mode=0)
R.ent('pickup', 'SelloDelSol', -1300, 6770, Item=Res('sello_sol'))
R.shelf(-500, -360, 6750).shelf(-260, -120, 6600).shelf(-500, -360, 6450).shelf(-260, -120, 6300).shelf(-500, -380, 6150)
R.door('TrampillaSombra', -300, 5980, 120, horizontal=True, pid='piramide_sombra_sello_sol')
R.ent('placa', 'PlacaSombra', -800, 6900, Targets=NP('TrampillaSombra'), TargetMethod='Open', PressedByPlayer=False, PressedBySombra=True)
R.note(-2500, 5950, 'P15. Sello del Sol en el altar. Placa de Sombra → trampilla del techo → Cámara de Sombra del Sol (vuelta tras Arkal).')
link('P14', 'E', 6900, 'P15', 'W')

# ==================================================================================================
# P16 · CÁMARA DE SOMBRA DEL SOL (secreto, Sombra) — bóveda alta, cofre grande arriba.
# ==================================================================================================
R = Room('P16', 'CamaraSombraSol', -2600, 4000, 0, 5900, 'Secreto de Sombra')
R.floor(-2600, -360, 5800).floor(-240, 0, 5800)
for i, y in enumerate((5660, 5520, 5380, 5240, 5100, 4960, 4820, 4680, 4540)):
    x = -900 if i % 2 == 0 else -1150
    R.shelf(x, x + 140, y, 'Repisa')
R.block(-1700, 4400, -1100, 4440, 'BalconBoveda')
R.cofre(-1500, 4400, 800, 'CofreBoveda')
R.cofre(-2000, 5800, 200, 'CofreChico')
R.ent('murcielago', 'Murcielago', -1300, 5000)
R.note(-2500, 4050, 'P16. Secreto de Sombra: subir las repisas a la bóveda (cofre 800).')
link('P15', 'T', -300, 'P16', 'B', spawn_b=(-120, 5760), spawn_a=(-200, 6040))
