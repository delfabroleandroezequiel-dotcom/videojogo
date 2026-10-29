# Diseño de niveles — guía del diseñador

Documento vivo. **Leerlo antes de armar, generar o tocar cualquier mapa** (greybox, trampas, zonas nuevas, atajos, secretos) y sumarle lo que se aprenda en cada uno. Última actualización: 2026-10-28.

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

## 7. Lo que el autor rechazó (no repetir)

- **Plantilla repetida:** La Sima v1, 40 pisos en zigzag con un obstáculo cada uno ("bastante repetitivo"). Mezclar tipos: pozos verticales, cavernas anchas, diagonales, chimeneas de viento, salas de rebote, santuarios.
- **Puertas porque sí:** muros cargados puestos en todos lados sin crear nada.
- **Muro invisible al azar** en el camino principal, sin nada detrás.
- **Herramientas de grafos** para diseñar mapas: el autor piensa en el espacio físico, en papel.
- **Decorar yo:** no entrego salas "terminadas" con decoración. Entrego herramientas de dispersión (Count + jitter) y piezas, y el autor elige.
- **Galerías de cobertura:** en vista lateral, la primera columna tapa todas las flechas de atrás.

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
