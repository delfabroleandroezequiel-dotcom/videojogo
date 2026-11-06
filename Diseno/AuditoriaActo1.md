# Auditoría del Acto 1 con el kit base

Fecha: 2026-11-05. Recorrido: `ArenaEntrenamiento` → `Casa1`/`Nix` → `BosqueLobos1` → Cueva (`InteriorCueva1`..`11`).
Pregunta: ¿se puede pasar **sin doble salto, sin dash y sin ataque cargado** (solo salto simple, rodar, ataque y parry)?

## Resumen

| Cuarto | Estado | Qué pasa |
|---|---|---|
| ArenaEntrenamiento | ✅ | Arena plana. No tiene `LevelTransition`: sale por la cinemática (`CinematicaDerrota` → narración). |
| Casa1 | ✅ | Plano; puerta a Nix con interactuar. |
| Nix | ✅ | Todas las salidas de la ruta se alcanzan con salto simple. |
| BosqueLobos1 | ✅ | Cueva, BL2 y Nix alcanzables. Sigue abierta la salida a `CaminoDesierto` (pendiente ya anotado en DisenoMundo). |
| InteriorCueva1 | ✅ | Pasillo plano. |
| InteriorCueva2 | ✅ (ida) / ⚠️ (vuelta) | Ida bien (se baja por las one-way). Volviendo desde Cueva3 no se puede subir: ver detalle. |
| InteriorCueva3 | ✅ | Pozo de plataformas one-way (abajo + salto). El `MuroCargado` tapa un túnel lateral opcional: buena cerradura vista antes. |
| InteriorCueva4 | ✅ | Escalera + saltos con margen. No tiene salida de vuelta a Cueva3. |
| InteriorCueva5 | ✅ | Puente sobre el agua; el agua no hace falta (no hay nado en el kit base). |
| **InteriorCueva6** (jefe araña) | ❌ **BLOQUEADO** | No se llega a la salida `HaciaCruce`: escalón de 145 px sin pared escalable. Hace falta doble salto o la cuerda (tampoco es kit base). |
| InteriorCueva7 (cruce) | ✅ / ⚠️ | Las 3 salidas a los sellos se alcanzan. `HaciaAbajo` (Cueva11) está tras el `GranSello`, que abre con las 3 llaves (no es un salto). No hay vuelta a Cueva6. |
| InteriorCueva8 | ✅ | Llave roja y las dos salidas, con margen. |
| InteriorCueva9 | ✅ | Llave verde y salida al Árbol, con margen. |
| InteriorCueva10 | ⚠️ **MUY JUSTO** | `Escalon2` está a 90 px y el salto llega a 92,3 px (2 px de margen, sobre un piso que se rompe). El resto pasa (la plataforma diagonal lleva a Descanso2). |
| InteriorCueva11 | ✅ | Suelos frágiles y salida a `Quebrada10_Colector` (greybox de prueba; falta la Mina). |

**Lo más grave:** la Cueva6 (sala del jefe) corta el Acto 1 entero con el kit base. Y como de Cueva4 y de Cueva7 no se puede volver atrás, el jugador **queda atrapado** entre Cueva4 y Cueva6 (sin volver a Nix).

---

## 1. Números del protagonista (medidos)

Sacados de `scripts/player/Player.cs` y medidos en headless con el `Player.tscn` real en un cuarto de prueba (doble salto, dash, trepar, sprint, cargado, cuerda y nado bloqueados con `LockForScene`):

| Dato | Valor |
|---|---|
| `Speed` / `JumpVelocity` / `Gravity` | 200 px/s / −400 / 900 (al caer ×1,7 = 1530), caída máx. 560 |
| Altura máx. del salto simple | **92,3 px** medidos (la fórmula v²/2g da 88,9; Godot aplica el impulso antes de la gravedad) |
| Escalón que se sube con margen | **≤ 75 px** (90 sube por 2 px; 145 no sube) |
| Hueco plano máximo | **~200 px** de borde a borde (190 seguro, 200 justo con coyote time de 0,1 s). Con margen: **≤ 170 px** |
| Agarre de bordes (`StartWithLedgeGrab = true`) | Sube paredes de hasta **~190 px** (145 y 190 medidos), **pero solo si la pared está en la capa `ClimbableWalls` (16)** y se mantiene arriba |
| Rodar | 300 px/s durante 0,35 s **con `velocity.Y = 0`**: rodando desde un borde se flota ~105 px sin caer. No hace falta en ningún lado, pero es un "atajo" que quizá no sea a propósito |
| Kit actual de `Player.tscn` | Doble salto, dash, sprint, trepar, cargado, cuerda y nado ya están en `false`. Rodar, parry, ataque arriba, borde y antorcha en `true`. `StartWithComboFourth` y `StartWithFlyingSword` quedan en `true` por defecto en `PlayerAbilities.cs` |

> El punto de DisenoMundo "`Player.tscn` arranca con todas las habilidades activas" ya no aplica: hoy el kit base no tiene doble salto ni cargado.

**Ojo con el agarre de bordes:** casi todas las colisiones de las cuevas están en la capa 1 sola, así que el agarre casi nunca se activa. En la ruta solo lo permiten las plataformas `CaveRockPlatform` (capa 49) y los `MiniCorridorAccordionCueva` (capa 17) de la Cueva6, que además son opcionales.

## 2. Método

- Un script `extends SceneTree` carga cada escena, junta toda la colisión que choca con el jugador (capa 1, one-way, escalable 16, bajable 32), la rasteriza a 2 px y recorre por BFS todas las posiciones de pie a las que llega el jugador desde cada punto de entrada (`TargetSpawnPosition` de los cuartos vecinos). Simula caminar, caer con control en el aire, saltos con 20 perfiles de dirección, coyote, bajar por one-way, escaleras, agarre de bordes según `TryDetectLedge`, rodar desde un borde, y como referencia el doble salto y el dash. Cada salida queda marcada con el nivel mínimo que pide: 0 salto, 1 + borde, 2 + rodar, 3 kit completo.
- Las alturas y huecos límite se confirmaron con el `Player.tscn` real.
- Para buscar pasos muy justos se repitió todo con un salto 10 % peor (180 px/s, −380). Lo único que cambió fue el `Escalon2` de la Cueva10.
- **Límites:** las plataformas móviles y los `BloqueFurioso` se tomaron quietos en su posición inicial (la plataforma diagonal de la Cueva10 se revisó a mano). Las cuerdas no se simulan (piden `RopeSwing`). No se cuentan enemigos ni trampas, solo la geometría.
- Scripts (fuera del repo): `audit.gd`, `audit_tight.gd`, `realstep2.gd` y `presser.gd` en el scratchpad de la sesión.

## 3. Detalle por cuarto

### ArenaEntrenamiento ✅
Piso plano. No tiene salidas físicas: el avance es por cinemática.

### Casa1 ✅ · Nix ✅
Todo plano o con escalones chicos. En Nix se llega a `ToBosqueLobos1Transition` (5238, 361) y a la puerta de Casa1 desde las dos entradas (1143, 463) y (4950, 440).

### BosqueLobos1 ✅
Desde (150, 258) y (1464, 263) se llega a `CaveEntranceTrigger` (1484, 190), `ToBosqueLobos2`, `ReturnToNixTransition` y `ToCaminoDesiertoTransition`. Esta última habría que cerrarla en el Acto 1 (pendiente que ya está anotado).

### InteriorCueva1 ✅
Pasillo plano de x −258 a 1821.

### InteriorCueva2 ✅ ida · ⚠️ vuelta
- **Ida:** de (121, 160) se camina hasta `MiniCorridor` (851..1089, techo y=249, one-way bajable) y se baja por `MiniCorridor2` (y=506) hasta la salida de abajo (807..1161, y 624..897).
- **Vuelta desde Cueva3:** se aparece en (983, 436) sobre `MiniCorridor2` (y=506). Para volver a `ACueva1` hay que subir a `MiniCorridor` (y=249): **257 px**, ni con borde. Sin doble salto el jugador no vuelve a la Cueva1.
- **Arreglo (si se quiere vuelta):** una `Ladder` en x≈1060, de y≈240 a 506 (Length ≈ 270), o un escalón intermedio one-way en y≈430 y otro en y≈350 (cada uno ≤ 80 px).
- `SecretWall` (792..856, y 416..512) abre un bolsillo hacia el oeste; no es necesaria.

### InteriorCueva3 ✅
Pozo vertical de 5 plataformas one-way bajables entre x 860..1090. Se baja con abajo + salto hasta `LevelTransition2` (807..1161, y 670..943). El túnel de la derecha (y 436..522) está tapado por el `MuroCargado` (1100, 473): cerradura opcional para el Acto 2, bien puesta.

### InteriorCueva4 ✅
Desde (120, −288) se baja por la `Ladder` (103, −240, 800 px) y se llega a `LevelTransition2` (1423..1777, y 64..337) con margen. **No hay salida de vuelta a Cueva3**: es de un solo sentido.

### InteriorCueva5 ✅
Desde (177, 537) por el `Puente`, entre los dos péndulos, hasta `HaciaCueva6` (1489..1549, y 421..591). El agua (`EnderLiliesWaterTest4/WaterZone`, 323..1207, y 645..1070) queda debajo y no se toca.

### InteriorCueva6 (jefe araña) ❌ BLOQUEADO
- Piso `Colision/Floor` de x −32 a 2270, **techo y=928**.
- Bloque `Colision/LeftWall/CollisionShape2D2` de x 2275 a 3015, **techo y=783** → escalón de **145 px**, en capa 1 sola (no escalable).
- La salida `HaciaCruce` (3005..3065, y 555..735) está sobre ese bloque.
- La ruta pensada era roca → fila de `MiniCorridorAccordionCueva` (y=661, x 498..1415) → `RopeAccordion` (1637, 494) → fila `MiniCorridorAccordionCueva2` (y=246, x 1424..2341) → caer al bloque. Pide **`RopeSwing`** (cuerda), que no es kit base, o **doble salto** para el escalón de 145.
- Además, llegar a las rocas (`CaveRockPlatform`, techo 768, 160 px sobre el piso) ya pide el agarre de bordes.
- **Las tres `CaveRockPlatform` están apiladas en el mismo punto (336.25, 775)**: seguramente faltó repartirlas.

**Arreglo propuesto (elegir uno):**
1. **(Recomendado) Escalón en el piso:** un `StaticBody2D` en `Colision` con rectángulo x 2150..2270, y 855..928 (techo a 73 px del piso y a 72 px del bloque). Sube con salto simple con margen y deja la cuerda como cerradura vista para cuando se tenga `RopeSwing`.
2. **Bajar el bloque:** subir el techo de `LeftWall/CollisionShape2D2` de 783 a 853 (75 px sobre el piso) y correr igual `RightWall/CollisionShape2D2`, el trigger `HaciaCruce` y el spawn de vuelta (2950, 740) que usa Cueva7. Toca más cosas.
3. **Solo si el agarre de bordes queda como base:** poner la cara izquierda del bloque en capa 1+16 (`ClimbableWalls`). 145 px se trepa (medido hasta 190).

### InteriorCueva7 (cruce) ✅ / ⚠️
- Desde la entrada (70, −370), sobre el balcón (`Greybox/Balcon`, −20..220, techo −330), se cae al piso (`SueloA`..`SueloE`, techo 0) y se llega a `HaciaRoja` (0, −247), `HaciaVerde` (1595, −205) y por `EscaleraArriba` a `HaciaVioleta` (1260, −637).
- `HaciaAbajo` (720..880, y 333..363) está dentro del pozo cerrado por `GranSello` (720..880, y −8..24), que abre `CerraduraDeSellos` con las 3 llaves. Las 3 llaves se alcanzan con el kit base (cuevas 8, 9 y 10), así que **es cerradura, no salto**. ✅
- ⚠️ **No hay vuelta a Cueva6:** el balcón está a 330 px sobre el piso y las entradas de vuelta (160, −40), (1440, −40) y (1250, −40) caen abajo. Junto con Cueva4 → Cueva3 y Cueva3 → Cueva2, la Cueva entera es de ida. La única salida a la superficie son Cueva9/Cueva10 → `InteriorArbolBosqueLobos2_7` (fuera de esta auditoría). Si se quiere un bucle de vuelta: `Ladder` en x≈100 del piso al balcón (Length ≈ 340).

### InteriorCueva8 ✅
Desde (−160, −40) se llega a `Objetos/LlaveRoja` (−2752, −692) y a las dos salidas a Cueva7. Pasa también con el salto 10 % peor.

### InteriorCueva9 ✅
Desde (160, −40) se llega a `Objetos/LlaveVerde` (2728, −692) y a `HaciaArbol` (2770, −807). Con margen.

### InteriorCueva10 ⚠️ MUY JUSTO
- `Greybox/Base` techo y=0 → `Trampas/Escalon2Cuerpo` (480..560) techo **−90** → `Escalon3Cuerpo` (600..680) −140 → `Descanso1` (720..1020) −200.
- **Escalon2 a 90 px con salto de 92,3 px: 2 px de margen**, y encima es `CrumbleFloor` que reaparece a los 3 s. Con el salto 10 % peor no pasa.
- `PlataformaDiagonal` (de 1072..1168 a techo −234 hasta +240, −200) lleva de Descanso1 a `Descanso2` (1380..1700, −400). Después `Rampa3` → `Cima` (−600) → `LlaveVioleta` (2688, −642) y `HaciaArbol` (3875, −779) pasan con salto simple. El `BloqueFurioso` (3806..3866, y −671..−611) se salta (76 px).
- **Arreglo:** bajar `Escalon2` y `Escalon2Cuerpo` 15 px (position.y −77 → −62, techo −75). Escalon3 queda a 65 px del nuevo Escalon2: bien.

### InteriorCueva11 ✅
Desde (60, −40), por los suelos frágiles `SuelosFragiles/Frag1..3`, hasta `HaciaColector` (610, 1553). Falta conectarla con la Mina (pendiente ya anotado).

## 4. Pendientes que salen de esto

- [ ] **Cueva6:** escalón (o bloque más bajo) para llegar a `HaciaCruce` sin cuerda ni doble salto. Repartir las 3 `CaveRockPlatform` apiladas.
- [ ] **Cueva10:** bajar `Escalon2` 15 px.
- [ ] Decidir si la Cueva es de ida (hoy no se vuelve de 7→6, 4→3 ni 3→2) y, si no, poner escaleras de vuelta (Cueva2 x≈1060, Cueva7 x≈100).
- [ ] Decidir si el agarre de bordes es base. Hoy está activo, pero casi ninguna pared de las cuevas está en `ClimbableWalls`, así que casi no se usa.
- [ ] Rodar desde un borde flota ~105 px (`velocity.Y = 0` durante el roll). Decidir si es a propósito.
