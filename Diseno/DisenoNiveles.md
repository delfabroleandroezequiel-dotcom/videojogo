# Diseño de niveles — guía del diseñador

Documento vivo. **Leerlo antes de armar, generar o tocar cualquier mapa** (greybox, trampas, zonas nuevas, atajos, secretos) y sumarle lo que se aprenda en cada uno. Última actualización: 2026-11-05.

El mapa grande del mundo (actos, tramos, llaves y cerraduras) vive en **`Diseno/DisenoMundo.md`**. Este documento es el oficio de cada sala y cada zona.

---

## 1. El perfil: diseñador de niveles de metroidvania

Cuando toque un mapa, trabajo como **diseñador de niveles de metroidvanias 2D**: alguien que piensa en recorridos, en lo que el jugador ve antes de poder hacerlo y en la vuelta atrás, no en "pantallas lindas".

**Sus convicciones:**
- **Metroidvania, no plataformero.** Es el objetivo principal del juego. Una cadena de niveles A→B no alcanza: el mundo tiene que doblarse sobre sí mismo.
- **Cada elemento tiene un porqué.** Antes de poner una trampa, un enemigo, un muro secreto o una pared agrietada, puedo decir en una frase qué situación crea. Si no, no va.
- **La cerradura se ve antes que la llave.** El jugador tiene que recordar "ahí no pude pasar" para que volver con la habilidad valga la pena.
- **Variedad de formas.** Una plantilla repetida se siente relleno aunque cambien las trampas.
- **Secciones chicas.** Mapas cortos conectados con `LevelTransition`, nunca una escena gigante.
- **Yo pongo la base mecánica, el autor pone el ojo.** Geometría, distancias y trampas las puedo calcular y verificar; la decoración y lo estético los decide el autor.

---

## 2. Checklist de cada zona (metroidvania, no plataformero)

Revisar **cada tramo** antes de darlo por cerrado (es la misma lista de `DisenoMundo.md` §4b):

- [ ] **Cerraduras a la vista antes que la llave:** al menos una cornisa, pared o pozo que todavía no se puede pasar.
- [ ] **Volver con la llave cambia el lugar viejo:** dónde se abre cada cerradura sembrada.
- [ ] **Atajos que cierran bucles:** al final del tramo, una puerta que vuelve al santuario o al hub.
- [ ] **Mundo continuo:** la zona se toca con otras por más de un lado.
- [ ] **Secretos opcionales detrás de habilidades.**
- [ ] **Orientación:** el jugador sabe hacia dónde seguir sin carteles.
- [ ] **Se puede pasar con el kit que el jugador tiene en ese punto** (no con el kit completo de desarrollo).

---

## 3. El orden de trabajo: gris → jugable → lindo → poblado

1. **Gris:** bloques de colisión reales, sin arte.
2. **Jugable:** recorrer cada camino y validar distancias, saltos, cerraduras y vuelta atrás.
3. **Lindo:** cambiar a tiles finales sin tocar la colisión validada.
4. **Poblado:** decoración, enemigos, triggers y luces, al final.

Decorar o poner enemigos antes de cerrar la geometría obliga a rehacerlo cada vez que cambia el mapa.

---

## 4. Reglas de construcción (greybox)

**Colisión:**
- Un `StaticBody2D` llamado `Colisiones` con hijos `CollisionShape2D` / `CollisionPolygon2D`, más un cuerpo one-way para repisas. Es lo que el autor edita cómodo (como en Raices1 o MontañaExtraña).
- Cada pieza con su `ColorRect` / `Polygon2D` visible encima (los rellenos de greybox se ocultan en el juego).
- Paredes laterales partidas en una colisión por piso, para que el autor pueda abrir accesos.
- **Diagonales:** meter rampas (2:1, como mucho 3:2) entre descansos planos. Que no sea todo recto.

**Números del jugador (para calcular, no adivinar):**
- Salto simple: unos **89 px de alto** y **178 px de largo**.
- Ciclo de dash: **0.78 s**.
- Gravedad 900 sin velocidad terminal, y la cámara muestra solo unos 200 px hacia abajo: las caídas largas no se leen. Diseñarlas como tramos cortos de "leer y comprometerse" (~320 px) con repisas seguras.
- Al caer de una repisa quedás pegado a su costado hasta que la cabeza la pasa (~60 px): un pincho de pared va al menos ~200 px debajo.

**Conexiones:**
- La zona del `LevelTransition` está ~47 px por encima del nodo.
- El punto de llegada, al menos a **140 px** de la salida (si no, el input sostenido te devuelve).
- Sin `SavePoint` en el mapa, morir te manda al último guardado o a Casa1.

**Escenas:**
- Raíz con `LevelBootstrap`. Nombres de escenas y nodos en castellano.
- Para cadenas de mapas de un mismo bioma, preferir una **escena base heredada** (como `MontaniaExtraniaBase.tscn`) o una escena de atmósfera compartida (como `AtmosferaBosqueLobos.tscn`), no copiar a mano la atmósfera en cada mapa.
- Carpetas: `scenes/world/Mapas/` para mapas, `Rehusables/` para piezas.

---

## 5. Trampas, puertas y secretos

**Trampas:**
- Antes de programar una trampa nueva, **usar las que ya existen** (pinchos, flechas, roca rodante, péndulo, sierra, aplastador, estalactita, rayo, cepo, barril, placa de presión, andamio, etc.). Hay muchas que casi no están puestas en ningún mapa.
- Si hace falta una nueva, priorizar las de **tiempo o disparo** (leer un patrón y reaccionar) sobre otra más de "tocar y dañar".
- El movimiento de una trampa sale de **reglas físicas** (gravedad, colisión, giro al chocar), no de caminos dibujados a ciegas con puntos.

**Puertas (muros cargados, sellos, barreras):**
- Un tipo de puerta se usa **una vez para enseñarlo** y después solo donde crea una situación (romper el muro mientras una trampa se cierra) o abre un atajo. Repetida por todos lados deja de significar algo.

**Secretos:**
- Un muro invisible o un piso que se rompe solo existe si protege algo: un premio, un atajo de vuelta, un engaño que el mapa preparó. Si el pedido no dice para qué, lo pregunto o propongo el propósito antes de ponerlo.

---

## 6. Recetas que funcionaron

- **PruebaTrampas1** ("muy bueno"): ola de pinchos → 3 aplastadores en tiempo → pozo con piedra y péndulo → sierra con refugio → flechas altas y bajas alternadas.
- **PruebaDiagonal1** ("me encantó"): rampa vacía para enseñar, pelea en un descanso angosto, flecha disparada por la pendiente (no se esquiva de costado: saltar o parry), aplastador que niega el descanso, murciélagos bajando por la pendiente, guardado al fondo. **Lo que la hizo funcionar:** decir el porqué de cada elemento antes de construir y verificar cada uno en modo headless.
- **PruebaTorre1:** torre de 3 pisos con ascensor, un segundo ascensor que se llama con una palanca custodiada, y trampilla de una sola vía como atajo al guardado.
- **PruebaCruce1:** hub con 3 sellos de color y tres caminos (trampas, enemigos, plataformas) que devuelven al hub con la llave. Es el prototipo de llave y cerradura.

---

## 6b. Zona: La Mina (greybox, 2026-11-05)

`scenes/world/Mapas/Mina/` — 12 escenas. **Recorrido desde el 2026-11-09:** se entra por la vagoneta y después se **sube**. `InteriorCueva7` (hueco del piso) → cae en `SpiderBossArena` → `HabitacionRecompensaSpider` → pared abierta detrás de Gransin → **Mina07a → 07b → 07c** (el viaje en vagoneta, bajando **hacia la derecha**) → Mina06 → 05 → 04 → 03 → 02 → Mina01, todo de subida hacia el este, hasta la boca de la mina: **Mina01 (borde este) sale a la `Entradamina` de BosqueLobos6** (conectado el 2026-11-09; desde el bosque se entra con interactuar frente a la boca, pasar caminando no te mete). BosqueLobos6 tiene el acceso a Ullatorp. **Ramal:** de la estación de entrada de 07b (repisa baja, puerta oeste) → Mina08 → Mina09 → montacargas a la superficie (`ConexionPendiente_BosqueLobos6`, pendiente). Las salas 01-06, 08 y 09 siguen espejadas (bajan hacia la izquierda, 2026-11-07); las capturas de `Mina/capturas/` son de antes del espejado y la Mina07 vieja ya no existe. Se juega con el kit base (sin doble salto ni cargado).

**Subirla en vez de bajarla (revisado 2026-11-09):** las rampas de 01 y 02 se caminan en los dos sentidos; en 03 se llama el montacargas desde abajo; 04 se sube por la **escalera de vuelta** (los andamios de una vía y el derrumbe-escalón quedan como camino opcional de bajada); 05 y 06 son simétricas, pero sin guantes no se pasan, por eso **los guantes se movieron al final de 07c**. Sin probar a mano: el primer salto suelo → `Cuerda4` en 06 (240 px desde la orilla, antes era cuerda → orilla). Las vagonetas sueltas de Mina01 ahora bajan **de frente** por las rampas: los refugios siguen sirviendo, pero es la misma idea que el autor rechazó en la vagoneta (ver §7) — a revisar.

| Cuarto | Forma | Mecánica estrella y por qué |
|---|---|---|
| Mina01_Bocamina | dos rampas largas con descansos | **Vagoneta suelta** enseñada: una frenada a la vista en la entrada, que se suelta cuando llegás al descanso (viene de atrás); otra con ritmo fijo en la segunda rampa. Respuesta: saltarla o meterse en el **refugio** (zanja bajo el riel). Cerradura de **doble salto**: cornisa a 150 px con cofre |
| Mina02_Grisu | galería escalonada con domo | **Grisú**: domo con ciclo fijo (cruzar el montículo con el gas limpio) y gas en el techo donde cuelgan murciélagos (explotan con él). **Secreto con propósito**: una palanca-válvula llena de gas el tapón de escombro del techo; la explosión lo abre → enseña que el gas rompe escombro |
| Mina02b_Polvorin | cuarto chico arriba | premio del secreto (mejora de frasco 1), barriles, rata |
| Mina03_Pozo | pozo vertical | **Montacargas** con murciélagos dormidos en las paredes; hoguera arriba. Galería lateral a mitad del pozo con **cuerdas sobre ácido**: cerradura de los guantes (mejora de frasco 2 al fondo) |
| Mina04_Andamios | caverna alta | andamios de una vía bajando, bandido de bombas arriba. **Derrumbe de techo**: el escombro que cae es el escalón para la repisa de salida (170 px, imposible sin él). Cerradura de **ataque cargado**: muro cargado con cofre en un nicho. Escalera de vuelta |
| Mina05_Capataz | campamento plano | 3 bandidos + barriles; oficina en andamio (vacía desde que los **Guantes de minero** pasaron a 07c). Cuerda sobre la fosa de ácido |
| Mina06_LagoAcido | lago largo | cadena de 4 **cuerdas en contrafase** (con islote), **bandido de cuerda** en la del medio, luego **cuerda horizontal** sobre un segundo lago con murciélagos |
| Mina07a_Bajada | viaje de 7000 px (~28 s), bajando 960 px hacia la derecha | **v3 (2026-11-09): caerse del carro es morir** (todo sobre un mar de ácido `InstantKill`, sin piso bajo la vía). **Saltar hace saltar al carro** (estilo DKC): 7 pozos de 120-140 px, sueltos y en ritmo doble, 6 **vigas bajas** (agacharse), una viga justo después de un aterrizaje. Hoguera en la estación de entrada |
| Mina07b_Persecucion | ~7300 px (~27 s) | **Carro de bandidos** por la vía de abajo que te alcanza (arquero arriba: parry) y termina cayendo al ácido; bandidos que caen al carro; **palanca a espadazo** (`Lever.GolpeConArma`): la vía principal termina en un **puente roto** de 600 px que se ve desde la palanca; pegándole, el desvío baja por debajo y vuelve. Estación de entrada con **repisa baja y puerta oeste al ramal de Mina08** (escalera de soga para volver) |
| Mina07c_Cambio | ~7500 px (~28 s), sobre **lava** | Derrumbe que te persigue (estalactitas que caen al pasar), pozos y vigas; el puente sobre la lava **se termina** (`CaeAlFinal`): una vagoneta baja por la vía de arriba hasta quedar a la par, 70 px más alta (`Acompanar`) → **saltar a ella**; la tuya cae a la lava. Sigue con 2 pozos más, estación final con **Guantes de minero** y hoguera, salida a Mina06 |
| Mina08_Fundicion | lava + galería | cuerda horizontal sobre **lava**; después, **el gas como dinamita** (obligatorio): galería llena de grisú sin chispa y tapada por escombro; la palanca suelta una vagoneta que se estrella adentro y lo prende |
| Mina09_Montacargas | pozo de 1100 px | hoguera; **montacargas mayor** hacia la superficie con 2 bandidos de cuerda que caen sobre la jaula y murciélagos |

**Llave y cerraduras (propuesta):** la llave de la Mina son los **Guantes de minero** (balancearse en cuerdas), ahora al final del viaje en vagoneta (07c), justo antes de los lagos de cuerdas de 06. Con el sentido nuevo, las cuerdas de Mina03 ya no son una cerradura vista antes (llegás con los guantes): queda como desvío opcional. Siguen: cornisa de doble salto en Mina01 (vuelta desde Ullatorp), muro cargado en Mina04 (vuelta después de Ovre). El viaje en vagoneta es de **una sola vía**: de la estación final de 07a no se puede volver arriba.

**Aprendido construyendo la Mina:**
- **El agarre de cornisa solo existe en paredes de la capa `ClimbableWalls`.** En greybox con colisión normal el límite real es el salto: 80 px sube, 100 ya no. Una cerradura de doble salto va entre 100 y 170 px (el doble llega a ~174).
- **Cuerdas que se balancean (`Rope`) son de ritmo fijo y lento** (periodo ~6 s): dos seguidas van **en contrafase** (`PhaseOffset` = π) y a ~250 px, así en los extremos las puntas casi se tocan. Con desfases arbitrarios casi nunca se cruzan.
- **Peligros que caen al vacío:** `PozaAcido` te devuelve al último piso firme (nunca a un carro o plataforma móvil) en lugar de matarte.
- **Plano al inicio de un riel:** la vagoneta suelta necesita velocidad de arranque mayor que lo que la fricción le saca en ese tramo, o se frena y "explota" quieta.
- **`Elevator.PointStart` no deja el carro arriba al cargar** (arranca abajo y sube solo); está en el script compartido, no lo toqué. Por eso los montacargas tienen palancas de llamada propias.
- **Espejar una zona entera** (2026-11-07): cada escena gira alrededor de `CameraLimitLeft + CameraLimitRight` (así los límites de cámara no cambian). Se invierten posiciones, polígonos (y su orden), offsets de `ColorRect`, rieles (sin invertir el orden: es el sentido de marcha), `ZoneOffset`/`SparkOffset`, el `TargetSpawnPosition` de cada transición con el eje de la escena **destino**, `PozaAcido`/`HangRope` (su nodo es la punta izquierda: se resta el ancho), `PhaseOffset` + π en las cuerdas, `MiraDerecha` de escaleras de costado y el arte de las palancas (`OffTexture` = palanca derecha). Todo lo centrado (andamios, derrumbes, vigas, bolsas de grisú, muros) es solo posición. Los nombres Oeste/Este e Izq/Der se intercambian.
- **Lo que se rompió al ir hacia la izquierda** (ya arreglado en los scripts): las vagonetas se dibujaban **dadas vuelta** (rotaban con el ángulo de la tangente, ~180°; ahora se espejan y rotan con la pendiente) y `EmboscadaVagoneta.Direction` era la dirección del riel pero se comparaba con la X del mundo (ahora es la dirección en pantalla: −1 = hacia la izquierda).
- **El carro no arrastraba al jugador en el aire** (también en la versión original): con `SyncToPhysics`, Godot devuelve `Position` al estado de física apenas se lo asigna, así que "cuánto se movió" daba siempre 0. Se mide sobre la vía. Sin esto, saltar una vagoneta de frente te dejaba atrás.
- **Viajes largos:** `ReturnWhenEmpty` (volver a casa si se cae el jinete) castiga demasiado en 9000 px; `EsperarAlJinete` frena donde te caíste. Los saltos son tramos sin riel (`TramosSalto`) dibujados como arco: el carro no vuela, sigue la curva y la física de pendiente lo frena al subir y lo acelera al bajar.
- **Salto con el carro (v3, `SaltaConJinete`):** cuando el jinete salta desde la cubierta, el carro salta también, más bajo que él (360 px/s, gravedad 1100: ~59 px de alto y 0,65 s en el aire), así siempre vuelve a caer adentro. Con eso los `TramosSalto` son **pozos de verdad**: un carro que entra a uno sin estar en el aire se cae (`StartFalling`, explota 1,1 s después). A 300 px/s un pozo de 130 px da ~0,22 s de ventana; hay **0,12 s de coyote** (`SaltoTolerancia`) pasado el borde. Los pozos van **después de una bajada** (a velocidad), nunca tras un tramo lento.
- **Vigas en bajadas:** llegás desde más arriba que la viga, y agachado igual la tocás. La altura de la viga sale del punto más alto de la vía en los 110 px anteriores. Una viga no puede ir pegada al despegue de un pozo (saltás adentro de ella).
- **Vagoneta acompañante (`Acompanar`):** arranca cuando la líder la alcanza y copia su X (con hasta ±150 px/s para alcanzarla). Debe esperar a que la líder **ya se mueva**: con velocidad 0 arrancaba en el cuadro 0 a velocidad 0 y volvía a estacionarse. 70 px más alta que la líder: la cubierta queda justo sobre la cabeza del jugador y el salto (89 px) la pasa por 19 px.
- **Puentes largos planos** frenan el carro a la velocidad mínima (fricción): darles una bajada suave, y no subir después de un momento fuerte (10 s a 70 px/s se sienten eternos).
- **Generador:** `generar_mina07v3.pl` (scratch de la sesión del 2026-11-09) arma las tres escenas desde arrays de vía; un piloto headless (`viaje_mina07v3.gd`) recorre cada tramo saltando, agachándose y pegándole a la palanca.

## 6c. Zonas: Mina Honda y Raíces (greybox, 2026-11-11)

Generadas con `gen_mina_raices.pl` (scratch de la sesión del 2026-11-11). Todas las transiciones se probaron sin pantalla: cada punto de aparición deja al jugador parado y no rebota. **Todavía no se recorrieron jugando**: los saltos y las cuerdas están medidos con las reglas de abajo, pero hay que probarlos.

**Mina Honda** (`Mapas/Mina/`, de izquierda a derecha). Se entra **interactuando en la cornisa alta de Mina01**.

| Sala | Pide | Forma |
|---|---|---|
| Mina10_Galeria | Doble salto | Pozo de 1300 px que se sube con repisas cada 150 px; murciélagos |
| Mina11_Derrumbe | Ataque cargado | Galería baja con 3 `MuroCargado`; una vagoneta se suelta cuando llegás al último |
| Mina12_Inundada | Nado | Pileta con una roca que baja hasta el agua: hay que bucear por debajo |
| Mina13_Fosos | Dash en el aire | Dos fosos de ácido de 360 px con techo bajo encima (ajustar el ancho al probar) |
| Mina14_Corazon | Las cuatro | Escalón de 150, muro cargado bajo techo, pileta con roca, foso; cofre de 500 y **atajo a Mina03** (interactuar) |
| Mina15_Contrapeso | **Sombra** (clon manipulable) | Puzzle de contrapeso: la **placa del cuerpo** abre la reja solo mientras estás encima. Detrás de la reja, la **placa de sombra** (solo la pisa el clon) abre la trampilla de una bóveda con un cofre de 800. Solución: pararse en la placa, lanzar el clon (el cuerpo queda arrodillado y sigue pesando) y llevarlo hasta la otra placa. Sin trampa de encierro: el cuerpo nunca puede pasar la reja |

**Sombra en placas (código, 2026-11-11):** `PressurePlate` tiene `PressedByPlayer` (por defecto sí) y `PressedBySombra`. El clon manipulable (`ShadowPuppet`) se suma al grupo `sombra`. Como el clon no tiene capa de colisión, la placa lo busca por posición (de pie, dentro del ancho de la placa). Probado sin pantalla con un cuerpo de prueba en el grupo; el clon real no se probó jugando.

**Raíces** (`Mapas/Raices/`, hacia la izquierda desde Raices1). El barro es `PozaAcido` en marrón (te devuelve al piso firme) y las espinas son `ThornVine` (matan).

| # | Sala | Forma | Estrella |
|---|---|---|---|
| 02 | Enredo | horizontal | 6 cuerdas en contrafase con un islote, espinas en el techo; cachorro (marcador) |
| 03 | Pozo | vertical hacia abajo | Repisas de una vía y lianas que se mecen; espinas en las paredes |
| 04 | Cuna | sala | Hoguera y rampa |
| 05 | Pantano | horizontal | Raíces chicas sobre el barro, soga horizontal de 650 px, raíces inclinadas |
| 06 | Columpios | caverna alta | Dos cuerdas bajas y después cuerdas altas (doble salto); islote alto |
| 07 | Espinas vivas | horizontal | 6 cuerdas con espinas colgando entre ellas; murciélagos en lugar de pájaros |
| 08 | Chimenea | vertical hacia arriba | Repisas cada 160 px (doble salto) y una liana; espinas en la pared |
| 09 | Árbol hueco | sala | Raíz de la **Espada de Dios** que tapa un nicho con cofre; arañas (marcadores) |
| 10 | Rastro | horizontal con bajada | Pozo de barro, hoguera, lobos neutrales (marcadores) |
| 11 | Antesala | sala | Vacía a propósito: huellas quemadas |
| 12 | Arena del Lobo de Fuego | arena | Dos repisas; jefe pendiente de diseño (marcador) |
| 13 | Comunidad | santuario | Hoguera sobre una loma, lobos y loba vieja (marcadores); **atajo a Raices1** (liana, interactuar) |

**Pendiente de código** (marcado con carteles en cada sala): raíces que se hunden, espinas que se retraen, pájaros que te tiran de la liana, arañas colgadas, lobos neutrales, el Lobo de Fuego.

---

## 7. Lo que el autor rechazó (no repetir)

- **Plantilla repetida:** La Sima v1, 40 pisos en zigzag con un obstáculo cada uno ("bastante repetitivo"). Mezclar tipos: pozos verticales, cavernas anchas, diagonales, chimeneas de viento, salas de rebote, santuarios.
- **Puertas porque sí:** muros cargados puestos en todos lados sin crear nada.
- **Muro invisible al azar** en el camino principal, sin nada detrás.
- **Herramientas de grafos** para diseñar mapas: el autor piensa en el espacio físico, en papel.
- **Decorar yo:** no entrego salas "terminadas" con decoración. Entrego herramientas de dispersión (Count + jitter) y piezas, y el autor elige.
- **Galerías de cobertura:** en vista lateral, la primera columna tapa todas las flechas de atrás.
- **Vagonetas sueltas de frente en el viaje en vagoneta** ("malísimas", 2026-11-09). Arriba del carro van pozos, vigas, enemigos que te persiguen o caen, palancas a espadazo y el cambio de vagoneta.

---

## 8. Cómo trabajamos

- **El cuaderno es del autor:** dibuja los mapas y atajos en papel. No tengo acceso; si hace referencia a un dibujo, le pido que lo describa.
- **Le doy una base:** mapas greybox completos y fáciles de reconectar (transiciones en los bordes), que el autor después engancha al mundo y pule a mano. Sin sobrepulir.
- **Verificar en headless** saltos, colisiones y movimiento de trampas antes de entregar.
- **Avisar qué archivos toco** antes de editar escenas existentes.

---

## 9. Registro de decisiones

| Fecha | Decisión | Origen |
|---|---|---|
| 2026-07-14 | Orden gris → jugable → lindo → poblado | Pedido del autor |
| 2026-08-21 | Mapas en secciones chicas conectadas, no escenas gigantes | Lección del autor |
| 2026-10-14 | Mapas greybox de trampas como base que el autor pule; cada elemento con su porqué | PruebaTrampas1, PruebaDiagonal1 |
| 2026-10-14 | Secretos y muros invisibles solo con propósito | PruebaTrampas3 |
| 2026-10-20 | Colisión con bloques (`Colisiones`) y diagonales, no corredores de piezas | Revisión de La Quebrada |
| 2026-10-25 | Variedad de tipos en cada lote; puertas para enseñar o crear situaciones | La Sima v1 |
| 2026-10-25 | Objetivo principal: que se sienta metroidvania y no plataformero | Ruta de dos actos |
| 2026-10-28 | Atmósfera compartida por bioma (`AtmosferaBosqueLobos.tscn`) | BosqueLobos 1 a 6 |
| 2026-11-05 | La Mina en greybox (10 escenas): vagonetas, grisú como trampa y como dinamita, cuerdas en contrafase, guantes de minero como llave | Pedido del autor |
| 2026-11-07 | La Mina baja hacia la izquierda (espejada entera); Mina07 pasa a ser un viaje largo obligado arriba de la vagoneta | Pedido del autor |
| 2026-11-09 | La vagoneta pasa a ser la entrada de la Mina (desde la habitación de Gransin) y después se sube hasta la boca; viaje partido en 3 tramos con hoguera, muerte al caerse, salto con el carro, cambio de vagoneta; fuera las vagonetas de frente | Pedido del autor ("las vagonetas de frente son malísimas") |
| 2026-11-07 | Mapa del mundo estilo Afterimage, generado desde las escenas (sala entera al visitarla, una región por carpeta o por puerta, M para abrirlo); ver §10 | Pedido del autor |
| 2026-11-11 | Mina Honda: 5 salas de vuelta atrás desde la cornisa de Mina01, cada una pide una habilidad posterior. Raíces: 12 mapas hacia la izquierda de Raices1, con lianas y poco piso, terminan en la comunidad de los lobos (neutrales) tras el Lobo de Fuego (§6c) | Pedido del autor |
| 2026-11-11 | Ullatorp es un pueblo "normal": un corredor plano del largo de Nix (`Mapas/Ullatorp/Ullatorp.tscn`), sin barrios en altura ni pista de obstáculos en el mapa. Greybox sin arte, conectado al borde este de BosqueLobos6 | Pedido del autor |

## 10. Mapa del mundo (generado)

- **El mapa no se dibuja a mano.** `tools/generar_mapa_mundo.gd` (`GeneradorMapaMundo.cs`) lee cada escena de `Mapas/` y escribe `resources/mapa/MapaMundo.tres`. El tamaño de cada sala sale de los `CameraLimit*` del LevelBootstrap; si faltan, de la geometría. Cada sala se pega al borde de la sala desde la que se llega, alineando la salida con el `TargetSpawnPosition`.
- **Regiones:** cada carpeta es una zona (Cueva1, Quebrada, Cripta...). Las escenas sueltas en `Mapas/` forman la región de su raíz (Nix). Una salida con `RequireInteract` (puerta, boca de cueva) o que cambia de carpeta abre otra región y se marca como puerta.
- **Correrlo cada vez que se conectan o se mueven mapas:** `godot --headless --path . --script res://tools/generar_mapa_mundo.gd`. El reporte es una herramienta de diseño: **CONFLICTO** = un circuito que no cierra geométricamente, **SOLAPE** = dos salas que se pisan, **ROTA** = destino inexistente, **SUELTA** = zona sin conexión desde las raíces.
- **Ajustes a mano** en `resources/mapa/MapaMundoConfig.tres`: `Raices`, `Excluir` y `AjustesPosicion` (escena → desplazamiento). Se respetan al regenerar.
- **Un solo mundo:** cada región se ubica respecto de su entrada. Por una puerta, su spawn cae en la puerta (la cueva queda bajo su boca); cruzando de carpeta, se pega al borde. Si pisa otra zona se corre al lugar libre más cercano, primero hacia abajo, y el reporte lo marca como **MOVIDA**. Los interiores de una sola sala detrás de una puerta (casas, iglesia) no se dibujan: adentro, tu marcador va en su puerta.
- **En el juego:** cada sala aparece entera la primera vez que se entra (`SaveData.VisitedScenes`). Se ve todo lo visitado de todas las regiones, con un tono y el nombre de cada una. Se abre con M o desde el menú de pausa, enfocado en tu región; con zoom out ves el resto. El nombre de cada región va en `MAPA_REGION_<CARPETA>` en `strings.csv`.
- **Estado al 2026-11-07:** 63 salas en 16 regiones. La Quebrada (circuito 01→05 que vuelve a la 01), las salas del Árbol, la Cripta 2↔3 y la Cueva 7↔8 tienen circuitos que no cierran. La Mina y la Sima todavía no están conectadas.
