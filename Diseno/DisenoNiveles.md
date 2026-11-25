# Diseño de niveles — guía del diseñador

Documento vivo. **Leerlo antes de armar, generar o tocar cualquier mapa** (greybox, trampas, zonas nuevas, atajos, secretos) y sumarle lo que se aprenda en cada uno. Última actualización: 2026-11-12.

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

**v2 (2026-11-12): rehecha entera.** La v1 eran 12 tiras laterales de una sola altura, una entrada y una salida cada una ("lo más antimetroidvania que jamás vi"). La v2 es un circuito con hub, bucle a la hoguera, dos o tres pisos por sala y cerraduras sembradas. Generador: `gen_raices_v2.pl` + `rooms_raices.pl` (scratch de la sesión del 2026-11-12).

```
            R09 Árbol hueco ──► R10 Rastro (hoguera) ─ R11 ─ R12 Arena ─ R13 Comunidad ──atajo──► Raices1
              ▲                   └ estanque (NADO) → cofre + túnel pendiente
            R08 Chimenea ── raíz trepable (Acto 2) → superficie (pendiente)
              ▲
            R07 Espinas ◄── R06 Columpios ◄── R05 Pantano (abajo) ◄── R04 CUNA (hub, hoguera) ◄── R03 Pozo ◄── R02 ◄── Raices1
                              │ (doble salto + cuerda alta)          ▲ reja que se abre desde la copa
                              └──► R05 copa (arriba, baja hacia el este) ──┘
```

| # | Sala | Forma | Qué tiene y por qué |
|---|---|---|---|
| 02 | Enredo | horizontal de 2 pisos | Abajo: cuerdas en contrafase, islote, soga y raíz baja. Arriba (liana desde la entrada): copa, diagonal y mirador desde el que se ve un **cofre a 420 px: dash en el aire**. Del mirador caés al camino de abajo |
| 03 | Pozo | vertical hacia abajo | Zigzag con repisas de una vía, dos lianas, rampa y labio. A mitad, una **cueva tapada por un muro cargado** con cofre, que se ve al bajar. Se sube de vuelta con doble salto |
| 04 | Cuna | hub | Hoguera y 3 salidas: el pozo, el pantano y una repisa alta. La liana sube a una **reja cerrada desde el otro lado**: se abre con la palanca llegando por la copa del pantano |
| 05 | Pantano | 2 pisos de una sola vía | Abajo (la ida): raíces que se hunden, soga y raíces inclinadas. Arriba (la vuelta, desde Columpios): copa que **baja hacia el este** con dos arañas, hasta la reja de la Cuna. De arriba podés caer abajo; de abajo no se sube |
| 06 | Columpios | caverna alta | Abajo sigue la zona (cuerdas hacia el oeste). Desde el islote, tres repisas con doble salto y una cuerda alta hacia la repisa del este: **vuelta por la copa** |
| 07 | Espinas vivas | 2 caminos al islote | Arriba: cuerdas con espinas que bajan del techo y pájaros. Abajo: raíces que se hunden con espinas que salen del barro. Final común con cuerdas |
| 08 | Chimenea | vertical hacia arriba | Repisas alternadas, descanso con rampa y liana; espinas retráctiles en las dos paredes. Arriba, hueco en el techo con una **raíz trepable (Acto 2)** hacia la superficie (`ConexionPendiente_Superficie`) |
| 09 | Árbol hueco | sala de 3 alturas | Entrás alto por el este, bajás entre arañas (ves el nicho de la **Espada de Dios**) y salís alto por el oeste trepando una liana |
| 10 | Rastro | bajada en diagonal | 4 rellanos con rampas 2:1, hoguera y lobos neutrales. Abajo, un **estanque que solo se cruza nadando** (cofre + `ConexionPendiente_TunelNado`). Sin nado te hundís y salís por el escalón del oeste |
| 11 | Antesala | sala con bóveda | Silencio y huellas quemadas. **Placa que solo pisa la Sombra**: abre la trampilla de una bóveda con cofre |
| 12 | Arena del Lobo de Fuego | arena | Raíces bajas para saltar, repisas laterales y una alta ancha al centro. Jefe pendiente |
| 13 | Comunidad | santuario | Loma con hoguera, dos madrigueras en altura, peña de la loba vieja (liana), lobos neutrales y la **liana del atajo a Raices1** |

**Verificado sin pantalla (2026-11-12):** las 28 transiciones dejan al jugador parado sobre piso y fuera de la salida; un piloto con el jugador real probó 38 saltos (subidas con doble salto en 03, 06, 08, 09, 10, 12 y 13; huecos de la copa en 02 y 05) y que **el cofre del dash y la cueva del estanque no se alcanzan** con doble salto. El mapa del mundo cierra el bucle Cuna → Pantano → Columpios → copa → Cuna sin conflictos. **Sin probar jugando:** cuerdas, lianas, soga y la palanca de la reja.

**Aprendido rehaciéndola:**
- **El doble salto real sube 183 px**, el simple 92. Una repisa a 140-150 px entra con margen.
- **No poner una repisa justo debajo de otra plataforma**: saltando desde ahí te golpeás la cabeza (pasó en el Pozo y en el Rastro). Hay que dejar el lugar del salto fuera de la plataforma de arriba.
- **Dos salas unidas por dos pisos** tienen que tener el mismo desnivel en las dos: si en una el piso alto está 760 px arriba, en la otra también. Si no, el mapa del mundo marca CONFLICTO.
- `tools/generar_mapa_mundo.gd` se cae en `_initialize` (`gchandle.is_released`); llamando al generador en el primer `_process` funciona.

**Programado (2026-11-12, `scripts/world/Raices/`):**
- `RaizHundible` (Raices05): plataforma que se hunde hacia el barro mientras estás parado (0,25 s de margen) y vuelve a subir cuando te bajás. Solo traslada en Y.
- `EspinasRetractiles` (Raices07 desde el techo, Raices08 desde la pared): `ThornVine` que sale con ritmo fijo (adentro 1,6 s, aviso con temblor 0,5 s, afuera 1,2 s), con `Phase` para desfasar vecinas.
- `AranaColgante` (Raices09): baja rápido de su hilo cuando pasás debajo, espera y sube lento; lastima al tocarla (no mata). Dibujada en código; todavía no se puede matar.
- `PajaroRaices` (Raices07): murciélago teñido que solo se lanza mientras estás colgado (cuerda, soga, liana) y te suelta con `Player.SoltarAgarre`. Sin arte propio.
- Lobos neutrales (Raices10 y 13): `LoboBase.Neutral`. No atacan: si te acercás a menos de 230 px gruñen y retroceden. Pegarle a uno lo vuelve hostil, junto con los neutrales a menos de 520 px.

Probado sin pantalla (cada pieza por separado). **Sigue pendiente:** el Lobo de Fuego (se diseña con el autor) y la loba vieja.

---

## 6d. Zona: Las Lavas (diseño, 2026-11-24 · PROPUESTA)

**Entrada:** un `MuroCargado` a la izquierda de la arena de la araña (lo pone el autor) → `Lavas00_Caida`, un pozo vertical largo → la zona. Se llega con el kit de después de Ovre: guantes, doble salto y **ataque cargado**.

**Qué cambia al final:** el jugador baja a una forja vieja enterrada bajo el bosque y la recorre **sin tocar nunca el suelo**: todo piso de sala es lava. Es la zona más de plataformeo del juego, pero armada como circuito: hub con hoguera, dos ramas que se tocan, una esclusa que baja la lava y abre el tramo de abajo, atajos que vuelven a la hoguera y cuatro cerraduras de habilidades que todavía no se tienen.

**Lo que tomo de otros juegos (investigado):**
- **Celeste:** cada mecánica nueva se presenta en una sala **sola y segura**, después se **combina** con otra y al final se **retuerce**. Cada sala es una historia chica con su propio punto de reaparición. Rutas opcionales más difíciles con premio para el que domina.
- **Super Metroid:** cerraduras a la vista antes de la llave, metas visibles pero inalcanzables, un punto de no retorno que compromete al jugador, y siempre una puerta trasera.
- **Metroid Dread (Cataris):** la lava como **válvula**: una palanca la baja y cambia salas ya visitadas. Mejor que usarla de reloj en todos lados.
- **Ori:** salas donde no se toca el piso, y fugas de lava. **La crítica a Ori:** las fugas con muerte instantánea en todo y sin punto de control frustran. Lección: la lava común **no mata**, te devuelve al último piso firme con daño (como el ácido de la Mina); la muerte instantánea queda solo para la fuga, con una hoguera antes.
- **Prince of Persia: The Lost Crown / Rayman:** el recorrido como partitura; ritmo y lectura clara del camino; variedad en lo que bloquea.
- **Mario y Ristar:** la lava que sube se presenta tarde, después de enseñar la marea quieta, y se afina con mucho margen.

### Vocabulario de la zona

| Pieza | Qué es | Origen |
|---|---|---|
| Poza de lava | El piso de toda la zona: daño y te devuelve al último piso firme | `AcidPool` en naranja (existe) |
| Escoria | Losa que se hunde en la lava mientras estás parado y vuelve a subir | `RaizHundible` recoloreada (existe) |
| Chispa | Bola de fuego que salta de la lava con aviso | `ChispaSaltarina` (existe) |
| Costra | Basalto que se quiebra al pisarlo | `CrumbleFloor` (existe) |
| Fuelle | Plataforma que aparece y desaparece con ritmo | `PlataformaIntermitente` (existe) |
| Lanzallamas | Chorro desde la pared o el piso | `Flamethrower` (existe) |
| Brasa | Roca incandescente que cae del techo | `Estalactita` (existe) |
| Magma rodante | Roca rodante por una pendiente | `RocaRodante` (existe) |
| Balsa | Roca que se mueve sobre un río de lava | `MovingPlatform` (existe) |
| Placa + relevo | Pisás algo y se dispara algo en otro lado | `PlacaDePresion` + `RelevoTrampa` (existen) |
| **Géiser de vapor** *(nuevo)* | Respiradero que burbujea, avisa y te **lanza hacia arriba**: es transporte, no trampa. Ritmo fijo | nuevo, de tiempo |
| **Marea de lava** *(nuevo)* | La lava sube y baja con ciclo y aviso. Modo **fuga**: sube sin parar cuando se dispara. Modo **esclusa**: una palanca la baja para siempre | nuevo, de tiempo y disparo |
| **Cortina falsa** *(nuevo)* | Cascada de lava igual a las otras que se atraviesa. Esconde pasajes | `LavaFall` sin daño |
| **Paso de brasas** *(nuevo)* | Plataformas **invisibles** sobre la lava; las brasas que caen se apoyan en ellas y las delatan un instante | nuevo |
| **Piso agrietado** *(nuevo)* | Solo se rompe con el **golpe en picada** (habilidad todavía sin repartir) | nuevo, cerradura |

### Mapa

```
                       (muro cargado en la arena de la araña)
                                     │
                              L00 CAÍDA (pozo largo)
                                     │
   L19 Bóveda ◄─ piso agrietado ─ L01 FONDO ─► L02 ESCORIAS ─► L03 CHISPAS ─► L04 GÉISER
   (picada)                       ▲   ▲                                          │
                       ascensor ──┘   └── atajo (cortina falsa) ─────┐           ▼
   L09 CORTINA ◄─ L08 ESCLUSA ◄─ L07 CÁMARA ◄─ L06 MAREA ◄──────── L05 FORJA (hub, hoguera)
                    │ palanca baja                                     │   └─► L10 RÍO ─► L11 COSTRAS ─► L12 PENDIENTE ─► L13 FUELLES
                    ▼ la lava de L05                                   │       (dash)                                       │   │ (Sombra → L20)
             L05 abajo ─► L14 POZO DEL FUNDIDOR ─► L15 ASCENSO (fuga) ─► L16 GÉISERES ◄── bajada de una vía ───────────────────┘
                                                                              │
                                                    L05 ◄─ atajo ─ L17 ANTESALA (hoguera) ─► L18 PUENTE QUEBRADO ─► L21 ARENA
```

### Salas

| # | Sala | Forma | Qué enseña o pide, y por qué |
|---|---|---|---|
| 00 | Caída | pozo vertical largo | La puerta del cargado da al vacío. Caída larga partida por repisas cada ~320 px (la cámara no muestra más). **Punto de no retorno** hasta encender el ascensor de L01 |
| 01 | Fondo | sala ancha de 2 pisos | Primer contacto con la **poza de lava**, sin enemigos. Se ve el **piso agrietado** y, arriba, el **ascensor** apagado al pie de la Caída |
| 02 | Escorias | horizontal | **Enseña la escoria** sola |
| 03 | Chispas | horizontal de 2 alturas | **Enseña la chispa** y la **combina** con escorias: esperar en la losa mientras se hunde a que pase la chispa |
| 04 | Géiser | pozo hacia abajo y subida | **Enseña el géiser**: para bajar al hub primero hay que subir. Uno solo, después dos en cadena |
| 05 | Forja | hub de 3 pisos | **Hoguera.** Palanca que **enciende el ascensor de L01** (atajo 1). Salidas oeste y este. **Abajo, un lago de lava** que tapa una puerta: se ve desde el principio y se abre con la esclusa de L08 |
| 06 | Marea | sala de 2 alturas | **Enseña la marea** en ciclo: en bajamar se abre un camino bajo con un cofre chico |
| 07 | Cámara de la marea | caverna alta | **Combina** marea y géiseres: con la lava alta los géiseres no funcionan. **Chimenea lisa** que solo se trepa con **trepar paredes** |
| 08 | Esclusa | sala-máquina | Una **placa** dispara un **relevo** de lanzallamas mientras cruzás. Al fondo, la **palanca de la esclusa**: baja la lava de L05 para siempre |
| 09 | Cortina | secreto | **Cortina falsa** → **paso de brasas** (plataformas invisibles) hacia un cofre grande y la **cortina de vuelta a L01** (atajo 2) |
| 10 | Río | horizontal larga | **Balsas** sobre el río, lanzallamas en el techo bajo. Cofre al otro lado de un hueco de 400 px: **dash en el aire** |
| 11 | Costras | vertical hacia arriba | **Costras** que se quiebran + **brasas** que caen: subir sin quedarse quieto |
| 12 | Pendiente | diagonal larga | **Magma rodante** por una rampa 2:1 con refugios a 150 px; chispas en los huecos |
| 13 | Fuelles | sala de ritmo | **Fuelles** en secuencia con **géiseres**. **Placa de Sombra** → L20. Bajada de una vía a L16 |
| 14 | Pozo del fundidor | vertical hacia abajo | Se abre con la esclusa. **Lanzallamas** alternados en las paredes. **Hoguera** abajo |
| 15 | Ascenso | fuga vertical | **Contratiempo:** una placa dispara la **marea en modo fuga**. Piezas conocidas, ahora con apuro. **La única lava que mata de una** |
| 16 | Géiseres | caverna | **Retuerce** el géiser: cadenas desfasadas, chispas cruzando el chorro, relevo de brasas. Llega la bajada de L13: **las ramas se tocan** |
| 17 | Antesala | santuario | **Hoguera.** Puerta de una vía: **atajo a la Forja** (atajo 3) |
| 18 | Puente quebrado | horizontal final | Examen: todo junto, nada nuevo |
| 19 | Bóveda | secreto | Bajo el piso agrietado de L01 (**golpe en picada**). Premio grande |
| 20 | Cámara de sombra | secreto | Tras la placa de L13 (**Sombra**). Puzzle de cuerpo y clon con la marea |
| 21 | Arena | arena | Lago de lava con islas de escoria y géiseres. **Jefe pendiente** (se diseña con el autor) |

### Cerraduras y vuelta atrás

| Cerradura | Dónde se ve | Llave | Premio |
|---|---|---|---|
| Piso agrietado | L01, lo primero al llegar | Golpe en picada | L19 Bóveda |
| Chimenea lisa | L07 | Trepar paredes | Repisa con cofre y vista a la Forja |
| Hueco de 400 px | L10 | Dash en el aire | Cofre |
| Placa de sombra | L13 | Sombra | L20 |
| Lago de la Forja | L05, desde el principio | Palanca de L08 (misma zona) | Tramo de abajo L14-18 |

**Atajos a la hoguera:** L05 → ascensor de L01; L09 → cortina a L01; L17 → puerta a L05.
**Mundo continuo:** las ramas oeste y este se tocan por abajo (L13 → L16); se vuelve a la Cueva por el ascensor de L01.

### Las Lavas v2 (2026-11-25, en construcción)

**La v1 se descartó** (las 21 salas están en `Mapas/Lavas_v1_descartada/`): el autor la encontró **repetitiva, aburrida, demasiado fácil y lineal**. Eran casi todas "orilla → pilares sobre lava → orilla" con una pieza distinta en el medio, cerraduras explicadas con carteles y ninguna sala jugada antes de entregar.

**Cambio de método:** tandas de 3-4 salas, cada una con **un momento propio** (no "la mecánica del día"), varios pisos y salidas, sin carteles, y un **piloto** que maneja al jugador real y prueba cada salto obligatorio antes de entregar. Enemigos: melee y arqueros **de ejemplo** (se cambian después).

**Estructura nueva:** la **Forja** es un hub con **tres ramas en cualquier orden**; cada una guarda un **crisol** (rojo, ámbar, negro) y la arena se abre con los tres sellos (`EnergyBarrier` con llave). Cada rama vuelve a la Forja por un atajo propio.

**Tanda 1 (hecha):** `Lavas_Forja`, `Lavas_Crisol`, `Lavas_Torre`, `Lavas_BovedaCrisol` (rama del crisol rojo). La Caída (`Lavas00_Caida`) llega a la Forja.

| Sala | Momento | Rutas |
|---|---|---|
| Forja | Hub de 3 pisos. Abajo escorias y costras sobre la lava; el **géiser de la isla** te sube al piso de la forja (hoguera). Pasillo este con los **3 sellos** a la arena, a la vista desde el principio. Balcón oeste = llegada del atajo; balcón este (géiser) = rama de los Fuelles (pendiente) | 3 salidas + 2 pendientes |
| El Crisol que vuelca | Un **caldero gigante se inclina** y una **ola de lava** barre la calzada de punta a punta (`OlaDeLava`). Refugios a 150 px. Bandido en la calzada y arquero arriba | Abajo (calzada, con olas) o arriba (géiser del hueco + costras, cofre): las dos llegan a pisos distintos de la Torre |
| La Torre que se derrumba | La placa del pie **derrumba las costras de abajo hacia arriba** detrás tuyo (`DerrumbeEnCadena`). Rellano con bandido, escoria, géiser a la cima con arquero | Entrada por abajo (desde la calzada) o por el rellano (desde la ruta alta) |
| Bóveda del Crisol | **Marea** bajo el camino (pilares altos, escoria que se hunde), altar con 2 bandidos y arquero en la atalaya; cofre con el **crisol rojo**. Palanca → **puerta al balcón oeste de la Forja** (atajo) | Vuelta por el atajo |

**Verificado sin pantalla:** 17 llegadas paradas; el piloto pasó **31 de 32 saltos** del camino (el que falló pasa aislado: está justo); la ola lastima en la calzada (100 → 70) y no en el refugio; el derrumbe cae en orden cada ~0,9 s y las costras vuelven a los 4 s. **Falta jugarla.**

**Retoque con las lecciones de §6e (2026-11-25):**
- **Forja:** el **crisol colgado se ve desde la orilla este** (adelanto de la rama; no vuelca). Un **cofre chico de miga** en esa orilla tira hacia la rama. **Secreto:** una de las cascadas de la pared oeste es falsa (`CortinaFalsa`) y tapa un nicho con cofre de 450; se llega dejándose caer desde el piso de la forja hacia la izquierda (la repisa que asoma es la pista).
- **Crisol:** **presentación segura**: la primera ola sale cuando estás detrás del caldero y hay un refugio pegado a la entrada (`Refugio0`). **Secreto:** desde la repisa alta del arquero (U4), un **paso de brasas invisible** sube a un nicho en el techo con cofre de 450.
- **Torre:** **miga** en T1 (cofre chico) que tira hacia arriba; T3 bajó a -1620 para darle margen al salto desde la escoria.
- **Bóveda:** **cerradura de habilidad futura**: un **piso agrietado** en el altar (golpe en picada) tapa un pozo con cofre de 700; un **géiser** con ritmo te saca (un viento no servía: no te dejaba bajar al cofre).
- **Verificado sin pantalla:** el piloto pasó **36 de 36** saltos (los 3 secretos y el refugio nuevo incluidos), 17 llegadas paradas, y el pozo de la bóveda: caés al cofre y el géiser te deja en la atalaya.
- **Ojo:** Godot volvió a guardar 11 salas de la v1 en `Mapas/Lavas/` porque seguían abiertas en pestañas; se movieron a `Lavas_v1_descartada/reaparecidas/`. Cerrar esas pestañas.

## 6e. Aprendido de videos de diseño (2026-11-25, pedido del autor)

Vistos completos (transcripción): **"Super Mario 3D World's 4 Step Level Design"** (GMTK), **"Why You (Probably) Didn't Get Lost in Metroid Dread"** (Boss Keys) y **"The World Design of Hollow Knight"** (Boss Keys). Pendientes por bloqueo de YouTube: "Level Design Workshop: Designing Celeste" (GDC) y "Ten Principles for Good Level Design" (GDC).

**1. Cada sala, una historia en 4 tiempos (Mario 3D World, kishōtenketsu).**
- **Presentación segura:** la idea nueva aparece donde fallar no castiga (en Mario, caés a un piso de abajo y no perdés la vida).
- **Desarrollo:** la misma idea sin red.
- **Giro:** se combina con algo ya visto en la sala o antes (el enemigo del principio + la mecánica nueva), o se da vuelta.
- **Cierre:** un último momento de lucimiento.
- **Una idea por sala**, vista de principio a fin. Lo que falló en Mario Galaxy: salas que mezclan varios trucos sin darle tiempo a ninguno.
- **Lo opcional es un giro más difícil** (monedas verdes): el premio extra va donde la idea se lleva al límite.
- Las mecánicas ya aprendidas se pueden reusar más tarde sin volver a enseñarlas.

**2. Que no te pierdas sin carteles (Metroid Dread).**
- **Puntos de no retorno:** una caída, una puerta que se cierra detrás, un tobogán. Achican la zona explorable y bajan la carga de memoria.
- **La cerradura cerca de la llave**, en la misma zona; si está lejos, hay un atajo o un viaje rápido que la acerca.
- **Pocas cerraduras críticas por llave:** cuando conseguís algo, hay una sola puerta importante cerca. Las demás son opcionales.
- **Migas de pan:** premios chicos (cofres, mejoras, enemigos detrás de una pared, luces o bichos alrededor de la puerta correcta) que te llevan por el camino crítico.
- **Lenguaje visual:** las mismas luces o marcas indican siempre lo mismo. Los **hitos memorables** se recuerdan y llaman a volver.
- **El riesgo:** usarlo demasiado hace que el mundo se sienta un tour guiado (la crítica de Mark Brown a Dread).

**3. Un mundo que se abre (Hollow Knight).**
- **Lineal al principio, después se abre en abanico:** varias cosas para hacer a la vez, en cualquier orden. Así el jugador siempre encuentra algo importante vaya por donde vaya.
- **Dos o más caminos a cada lugar importante**, y dos o más **cerraduras críticas** por llave: duplica las chances de avanzar.
- **Cada zona se reconoce:** color propio, juego propio (vertical o en largo) y enemigos propios.
- **Una sala de adelanto:** una sala con la identidad de la próxima zona (hojas, cristales) la deja grabada como "lugar a donde volver".
- **Un personaje que tira de vos** (Hornet), y carteles dentro del mundo en vez de marcadores.
- **Atajos que se abren desde el otro lado:** la primera vez das toda la vuelta con el desafío completo; después, una puerta o un ascensor te ahorran repetirlo.
- **Contenido escondido de verdad:** jefes y premios opcionales que muchos no ven. Hace el mundo vivo.
- **Cerraduras blandas:** un camino que existe pero se evita por dificultad o costo.
- **Saltarse cosas a propósito:** dejar trucos que permitan pasar sin la habilidad. Es parte del gusto de explorar.

**Cómo lo aplico a Las Lavas desde la tanda 2:**
- Cada sala se escribe primero con sus **4 tiempos** (presentación segura, desarrollo, giro, cierre) y una sola idea central.
- Cada rama se **adelanta desde la Forja**: se ve el resplandor del crisol, se oye el fuelle.
- Cada crisol tiene **dos caminos** de llegada.
- **Migas de pan** hacia la ruta crítica y **un secreto de verdad** por rama.
- Un **punto de no retorno** al entrar a cada rama, y un **atajo** que la cierra al salir.
## 7. Lo que el autor rechazó (no repetir)

- **Plantilla repetida:** La Sima v1, 40 pisos en zigzag con un obstáculo cada uno ("bastante repetitivo"). Mezclar tipos: pozos verticales, cavernas anchas, diagonales, chimeneas de viento, salas de rebote, santuarios.
- **Puertas porque sí:** muros cargados puestos en todos lados sin crear nada.
- **Muro invisible al azar** en el camino principal, sin nada detrás.
- **Herramientas de grafos** para diseñar mapas: el autor piensa en el espacio físico, en papel.
- **Decorar yo:** no entrego salas "terminadas" con decoración. Entrego herramientas de dispersión (Count + jitter) y piezas, y el autor elige.
- **Galerías de cobertura:** en vista lateral, la primera columna tapa todas las flechas de atrás.
- **Vagonetas sueltas de frente en el viaje en vagoneta** ("malísimas", 2026-11-09). Arriba del carro van pozos, vigas, enemigos que te persiguen o caen, palancas a espadazo y el cambio de vagoneta.
- **Salas laterales planas en fila** (Raíces v1, 2026-11-12: "lo más antimetroidvania que jamás vi. Escenas laterales sin mucho esfuerzo"). Cada sala necesita varios pisos, más de un camino o salida, y su lugar en un bucle o una cerradura; la zona necesita un hub y por lo menos un atajo a la hoguera.

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
| 2026-11-12 | Raíces v2: las 12 salas rehechas como circuito (hub en la Cuna, bucle por la copa del Pantano con reja de una vía, 2-3 pisos por sala, cerraduras de dash, cargado, nado, Sombra, raíz trepable y Espada de Dios) (§6c) | Rechazo del autor a la v1 |
| 2026-11-11 | Ullatorp es un pueblo "normal": un corredor plano del largo de Nix (`Mapas/Ullatorp/Ullatorp.tscn`), sin barrios en altura ni pista de obstáculos en el mapa. Greybox sin arte, conectado al borde este de BosqueLobos6 | Pedido del autor |

## 10. Mapa del mundo (generado)

- **El mapa no se dibuja a mano.** `tools/generar_mapa_mundo.gd` (`GeneradorMapaMundo.cs`) lee cada escena de `Mapas/` y escribe `resources/mapa/MapaMundo.tres`. El tamaño de cada sala sale de los `CameraLimit*` del LevelBootstrap; si faltan, de la geometría. Cada sala se pega al borde de la sala desde la que se llega, alineando la salida con el `TargetSpawnPosition`.
- **Regiones:** cada carpeta es una zona (Cueva1, Quebrada, Cripta...). Las escenas sueltas en `Mapas/` forman la región de su raíz (Nix). Una salida con `RequireInteract` (puerta, boca de cueva) o que cambia de carpeta abre otra región y se marca como puerta.
- **Correrlo cada vez que se conectan o se mueven mapas:** `godot --headless --path . --script res://tools/generar_mapa_mundo.gd`. El reporte es una herramienta de diseño: **CONFLICTO** = un circuito que no cierra geométricamente, **SOLAPE** = dos salas que se pisan, **ROTA** = destino inexistente, **SUELTA** = zona sin conexión desde las raíces.
- **Ajustes a mano** en `resources/mapa/MapaMundoConfig.tres`: `Raices`, `Excluir` y `AjustesPosicion` (escena → desplazamiento). Se respetan al regenerar.
- **Un solo mundo:** cada región se ubica respecto de su entrada. Por una puerta, su spawn cae en la puerta (la cueva queda bajo su boca); cruzando de carpeta, se pega al borde. Si pisa otra zona se corre al lugar libre más cercano, primero hacia abajo, y el reporte lo marca como **MOVIDA**. Los interiores de una sola sala detrás de una puerta (casas, iglesia) no se dibujan: adentro, tu marcador va en su puerta.
- **En el juego:** cada sala aparece entera la primera vez que se entra (`SaveData.VisitedScenes`). Se ve todo lo visitado de todas las regiones, con un tono y el nombre de cada una. Se abre con M o desde el menú de pausa, enfocado en tu región; con zoom out ves el resto. El nombre de cada región va en `MAPA_REGION_<CARPETA>` en `strings.csv`.
- **Estado al 2026-11-07:** 63 salas en 16 regiones. La Quebrada (circuito 01→05 que vuelve a la 01), las salas del Árbol, la Cripta 2↔3 y la Cueva 7↔8 tienen circuitos que no cierran. La Mina y la Sima todavía no están conectadas.
