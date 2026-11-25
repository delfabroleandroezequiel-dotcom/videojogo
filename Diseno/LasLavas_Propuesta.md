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

