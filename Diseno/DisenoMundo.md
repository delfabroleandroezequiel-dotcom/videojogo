# Diseño del mundo — Las Reliquias del Reino

Última actualización: 2026-10-25. Documento vivo: lo decidido está marcado **DECIDIDO**, lo abierto **PENDIENTE**.

---

## 1. Estructura general: dos actos

| | **Acto 1 — El viaje del aventurero** | **Acto 2 — El mundo se desmadra** |
|---|---|---|
| Tono | Realista: cuevas, minas, caminos, desierto | Fantasía desatada |
| Ruta | Nix → Ullatorp → desvío por el desierto (choza de Ovre) → Enterbill → ruinas de Arkal (Bastón) → puerto → islas de Veril | Vuelta a Nix por abajo: Nix queda inaccesible por la superficie y se entra por las criptas |
| Enemigos | Bandidos, lobos, ratas, murciélagos, arañas | Jekyrianos, Oscuros, dragones, esqueletos y zombis en las criptas |
| Aliados | Gente común | Los árboles y la raza de los elfos |

- La fantasía casi no aparece en el Acto 1. Primera señal: una mazmorra subterránea en Arkal, al encontrar el Bastón.
- El Acto 2 empieza cuando reviven Mefasto y los Jekyrianos.
- La vuelta es un camino subterráneo **nuevo**, no repetir el de ida.
- Las zonas del Acto 1 pueden reaparecer "contaminadas" (otros enemigos, luz y laqueado) usando `SaveManager.StoryStage`.
- El final: entrar a Nix por las criptas (ya existe `Mapas/Cripta/`, debajo de `Iglesia1`) para la conclusión del libro (la última defensa de Delfrad).

**Cambios respecto del libro (para anotar en el texto):** Ovre es de la Parte 2 en el libro (Bosque de los Perdidos); acá su choza está en el desierto del Acto 1.

---

## 1b. Apertura y prólogo en Nix · PROPUESTA

- **Apertura:** entrenamiento con Arthur joven → derrota → narración en negro → huida del Nix destruido (presente) → la casa y el recuerdo.
- **Prólogo (recuerdo):** dos días en Nix antes de partir, con vecinos con nombre, rumores de malvivientes y la quest **"El carro de Morlok"** en el borde de BosqueLobos1. Termina con el camino del norte cerrado, que es el bloqueo del Tramo 1.
- Detalle completo en **`Diseno/PrologoNix.md`**.

---

## 2. Tramo 1 — Nix → Ullatorp · DECIDIDO

```
Nix ──► BosqueLobos1 ──► BosqueLobos2 (CORTADO: bandidos)
            │
            └──► Cueva ──► MINAS ──► BosqueLobos6 (entrada de mina) ──► Ullatorp
```

- La entrada de mina está en **BosqueLobos6** (sprite `Entradamina`, hoy sin transición).
- Cueva y Mina son, básicamente, **pasar por debajo del Bosque de Lobos**: entrás en BosqueLobos1 y salís en BosqueLobos6, salteando el tramo cortado.

### Zona 1: La Cueva · DECIDIDO
- Zona **compleja y larga**, con un **jefe**.
- Al matar al jefe se **sube nuevamente** y se conecta con la Mina (el jefe es la llave del paso).
- Ambientación: cueva (la que tienen hoy las cuevas 1 a 11: fondo `bosquecave`, laqueado del bosque).
- Mapas actuales que la forman: `InteriorCueva1` a `11`. Ya hay un jefe araña (`SpiderBoss`) en la cueva 6 y santuarios en las cuevas 8, 10 y 11.
- **PENDIENTE:** ¿el jefe de la Cueva es la araña de la cueva 6 u otro? ¿Dónde está la subida a la Mina después del jefe?

### Zona 2: La Mina · DECIDIDO
- Otra zona **grande**, con **otra ambientación**, a la que se llega después de matar al jefe de la Cueva.
- Sale a la superficie por la entrada de mina de **BosqueLobos6**.
- Todavía no existe ningún mapa.
- **PENDIENTE:** ¿tiene su propio jefe? ¿Qué ambientación (rieles, vagonetas, andamios, minerales, mineros)? ¿Qué enemigos?

- **Personaje base:** sin ataque cargado y sin doble salto en todo el tramo.
- **Bloqueo:** el camino de superficie a Ullatorp está cortado por bandidos muy fuertes que te matan de un golpe.
- **Desvío obligado:** desde BosqueLobos1 se entra a la cueva, se conecta con las minas y se sale por la entrada de mina de BosqueLobos6, ya del otro lado del bloqueo.
- **Bandidos:** en BosqueLobos2 (el camino queda cortado ahí), así el desvío saltea del 2 al 5.
- **Idea para después:** volver desde el 6 hacia atrás y cruzarse con los bandidos desde el otro lado.

---

## 2b. Tramo 2 — Ullatorp → Enterbill · DECIDIDO

```
                      (bandidos)                                         (GRAN ROCA)
Nix ─ BL1 ─ BL2 ✖ BL3 ─ BL4 ─ BL5 ─ BL6 ─ Ullatorp ─ BL7 ─ BL8 ─ BL9 ✖ ─► Enterbill
       │            │                 ▲                                  ▲
       └ Cueva ─ Mina ┘ (por debajo)  │                                  │ se rompe con
                    │                 │                                  │ el ataque cargado
                    └ bifurcación ─ choza de Ovre ─ Desierto/Pirámide ───┘ (volvés)
```

1. Seguir a la derecha hacia Enterbill (BosqueLobos 7, 8, 9, mapas nuevos) está cortado por una **gran roca** que se ve y no se puede romper.
2. Hay que **volver por BosqueLobos3**, que tiene la bifurcación, pasar por la **choza de Ovre** y meterse en el **desierto y la Pirámide** (encargo de Ovre).
3. Con el **ataque cargado** que te da Ovre, **volvés al camino cortado y rompés la gran roca** → Enterbill.
- Para llegar a BosqueLobos3 se vuelve por el bosque desde el lado este, el que no tiene bandidos: recorrés al revés el bosque que no pudiste cruzar al principio.
- **Supuesto:** Ullatorp queda justo después de BosqueLobos6 y BosqueLobos 7-9 salen de Ullatorp. *(Confirmar.)*
- La roca es la cerradura vista antes de la llave por excelencia: ubicarla bien a la vista, con algún indicio de que se puede romper (grietas que brillan al tener el cargado, como el `MuroCargado`).
- **PENDIENTE técnico:** cerrar en el Acto 1 la salida de BosqueLobos1 a `CaminoDesierto`. La bifurcación correcta es la de BosqueLobos3 (que ya va a `CaminoDesierto1`).

### Zona: La Pirámide (desierto) · DECIDIDO

- **Encargo de Ovre:** te manda a buscar un núcleo o piedra al desierto, que está al fondo de una **pirámide grande** (zona propia, armada con secciones chicas conectadas).
- **Recompensa (por qué te da la habilidad):** con esa piedra, Ovre **encanta tu espada**, que "aprende a guardar el golpe" → **ataque cargado**. No es una técnica tuya, es la espada. Es un primer toque mágico chiquito antes de Arkal, que viene de un hechicero; Ovre puede darte también la pista de Arkal.
- **Uso inmediato:** volver al camino cortado de BosqueLobos 7-9 y **romper la gran roca** que bloquea el paso a Enterbill.
- **Vuelta atrás sembrada:** mecanismos de sombra en la pirámide (puertas que reaccionan a la sombra, placas dobles) que no entendés todavía; se resuelven cuando la Sombra despierta en Arkal y abren salas secretas. Puede cambiar también en el Acto 2.
- **Material existente:** sprite `Mapas/piramide.png`, pack de desierto GandalfHardcore (32x32), mapas `CaminoDesierto` y `CaminoDesierto1`.
- **PENDIENTE:** ¿jefe guardando el núcleo? ¿El núcleo se ve antes de poder agarrarlo?

---

## 3. Llaves y cerraduras por tramo

| Tramo | Te da (llave) | Abre | Cerradura que se ve antes | Estado |
|---|---|---|---|---|
| Nix + cueva | Kit base | Atajo por la cueva | Camino a Ullatorp cortado por bandidos | DECIDIDO |
| La Cueva (cuevas 1-11) | Derrotar al jefe | Subida a la Mina | ? | DECIDIDO (falta el jefe) |
| La Mina | ? | Salida a BosqueLobos6 | ? | PENDIENTE |
| Ullatorp | Doble salto (entrenamiento) | — | Camino a Enterbill (BosqueLobos 7-9) cortado por una gran roca | DECIDIDO |
| Vuelta por el bosque → BosqueLobos3 | — | Bifurcación al desierto | La bifurcación ya se vio al principio, desde el otro lado | DECIDIDO |
| Choza de Ovre + Pirámide | Ataque cargado (espada encantada) | Volvés a BosqueLobos 7-9 y rompés la gran roca → Enterbill | La gran roca, más paredes agrietadas en la Cueva, la Mina y el bosque | DECIDIDO |
| Pirámide (vuelta) | — | Salas secretas | Mecanismos de sombra (se abren con la Sombra de Arkal) | DECIDIDO |
| Enterbill | ? | Ruta a Arkal | ? | PENDIENTE |
| Arkal + mazmorra | Bastón (invocaciones; ¿despierta la Sombra?) | Puerto | ? | PENDIENTE |
| Puerto → Veril | Barco | Islas | — | PENDIENTE |
| Veril (mazmorras conectadas) | ? | Revive Mefasto → Acto 2 | — | PENDIENTE |
| Acto 2: vuelta subterránea | Lo que abren las llaves del Acto 1 en zonas ya conocidas | Criptas → Nix | Nix sellada por la superficie | PENDIENTE |

### Puntos de "obtengo la habilidad"

| # | Dónde | Habilidad | Cómo se obtiene | Uso inmediato | Cerraduras sembradas antes | Estado |
|---|---|---|---|---|---|---|
| 1 | **Ullatorp** | **Doble salto** | Te lo enseñan, con lore: Gransin (o su padre Granthin) como guía de montaña, o un patio de entrenamiento de guías o escoltas. Una sala corta tipo pista de obstáculos | La vuelta por el bosque hacia la bifurcación de BosqueLobos3 | Cornisas altas a la vista en la Cueva y la Mina | **DECIDIDO** (falta quién enseña) |
| 2 | Choza de Ovre (tras la Pirámide) | Ataque cargado | Encargo: buscar el núcleo en la Pirámide; Ovre encanta la espada con él | Romper la gran roca de BosqueLobos 7-9 → Enterbill | La gran roca; paredes agrietadas en la Cueva, la Mina y el bosque | **DECIDIDO** |
| 3 | Enterbill (u otro lugar) | Dash en el aire | ¿Objeto, soldado, Arkal? (evitar repetir "escuela") | Fosos anchos | Huecos imposibles en el desierto y el bosque | Propuesta |
| 4 | Arkal (mazmorra) | Bastón + despierta la Sombra | Reliquia | Puzles de sombra | Mecanismos vistos sin entender | Propuesta |
| 5 | Puerto / Veril | Nado o buceo | ? | Mazmorras inundadas | Agua en las cuevas y en Nix | Propuesta |
| 6 | Acto 2 (árboles y elfos) | Trepar enredaderas o raíces | Ayuda de los elfos | Descenso a las criptas | Paredes con raíces por todo el mundo | Propuesta |

- El tramo Nix → Ullatorp (Cueva + Mina) se juega entero con el personaje base; la Mina **no** puede exigir doble salto para salir.
- **PENDIENTE:** qué del kit actual es base desde el inicio y qué sale de la lista (sprint, colgarse de cornisas, cuerda, golpe en picada).

**Habilidades de movimiento a repartir** (no van en el árbol de habilidades, son llaves de mapa): doble salto, dash, nado, trepar, colgarse de cornisas, cuerda, ataque cargado (rompe muros).

---

## 4. Pendientes y decisiones abiertas

**Decisiones de diseño:**
- [ ] Qué habilidad entrega cada tramo.
- [ ] Por qué está cerrado el camino directo Ullatorp → Enterbill (¿puente caído, malvivientes, tormenta de arena?). Que el obstáculo se vea.
- [ ] Qué te da Ovre (que el desvío no sea relleno: herramienta, llave o pista sobre Arkal).
- [ ] Cuándo aparece la Sombra. La propuesta es que despierte en Arkal junto con el Bastón, porque el kit de sombra es fantasía desde el minuto uno.
- [ ] Cómo se ve que Nix está inaccesible en el Acto 2 (Jekyrianos ocupando el camino, una barrera).

**Técnico (estado de los mapas al 2026-10-25):**
- [ ] Las minas no existen: en BosqueLobos6 la entrada de mina es un sprite decorativo sin transición.
- [ ] Hoy la cueva 11 sale a `Quebrada10_Colector` (greybox de prueba) y la cueva 9 a `InteriorArbolBosqueLobos2_7`; hay que conectar la 11 con las minas.
- [ ] BosqueLobos1 y BosqueLobos3 tienen salida a `CaminoDesierto`; en la ruta el desierto viene después de Ullatorp, así que hay que cerrarlas o cambiarlas en este tramo.
- [ ] `Player.tscn` arranca con todas las habilidades activas; para el Acto 1 hay que apagar al menos doble salto y cargado.
- [ ] Verificar que las cuevas 1 a 11 se puedan pasar con el personaje base (se hicieron con todo el kit).
- [ ] Falta el mapa de Ullatorp.

---

## 4b. Que se sienta metroidvania y no plataformero

Objetivo principal del diseño. Revisar **cada tramo** contra esta lista:

- [ ] **Cerraduras a la vista antes que la llave:** al menos una cornisa, pared o pozo que todavía no se puede pasar, para recordar.
- [ ] **Volver con la llave cambia el lugar viejo:** dónde se abre cada cerradura sembrada.
- [ ] **Atajos que cierran bucles:** al final del tramo, una puerta que vuelve al santuario o al hub.
- [ ] **Mundo continuo:** la zona se toca con otras por más de un lado.
- [ ] **Secretos opcionales detrás de habilidades.**
- [ ] **Orientación:** el jugador sabe hacia dónde seguir sin carteles.

Ideas ya aplicables:
- La Cueva y la Mina, jugadas con el personaje base, siembran cerraduras para doble salto y cargado que se abren al volver o en el Acto 2.
- **Primera llave temprano:** sugerencia de que el jefe de la Cueva (o la Mina) entregue la primera habilidad, y que la Mina tenga obstáculos que la usen. *(PENDIENTE decidir.)*
- **Bucles:** al salir de la Mina en BosqueLobos6, atajo de vuelta a la Cueva o a Nix; al volver hacia BosqueLobos3, forma de esquivar a los bandidos desde el otro lado.
- **Ullatorp como hub** al que se vuelve varias veces.
- **El Acto 2 abre las cerraduras** sembradas en el Acto 1.
- **Mapa:** hoy no hay pantalla de mapa; hace falta una, aunque sea simple, que marque sola las puertas bloqueadas. *(Respetar la política de autoloads de CLAUDE.md al diseñarla.)*

---

## 5. Lecciones de Afterimage

**Tomar:**
- Zonas tempranas con salidas visibles pero cerradas que lleven a zonas del final (el mundo "se dobla").
- Llaves de tipos distintos: movimiento, objetos o visión, y transporte.
- Un hub claro.
- Cadenas verticales que terminan en una habilidad.

**Evitar:**
- Zonas gigantes: mantener mapas en secciones chicas conectadas.
- Mundo abierto sin dirección; si una zona es muy difícil, que se note desde la entrada.
- Mapa que solo se actualiza al guardar; mejor que se dibuje solo y marque las puertas bloqueadas.
- Viaje rápido tacaño, que vuelve pesada la vuelta atrás.
- Fondos que parecen plataformas y decoración frontal que tapa el combate.
