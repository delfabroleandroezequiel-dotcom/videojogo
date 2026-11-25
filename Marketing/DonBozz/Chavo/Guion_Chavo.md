# Don Bozz #4 — El Chavo del 8: la carta a Santa Claus y el camión (guion v2, 2026-11-20, verificado)

Sigue la biblia de largos (`Diseno/BibliaLargos.md`, hoy solo en el worktree `dazzling-raman-7ba839`; 9 pasos) y lo que aprendimos con el Homero. El detalle de cómo se aplica cada regla está en el encabezado de `Narracion.md` (v3).

**Por qué este tema:**
- **El hueco más grande que medimos** (biblia, 6 nov): análisis de personaje del Chavo casi no hay (Psicólogo Ness 8–19K), y la demanda es enorme (Series Infinito 4.6M, Dieck Docs 7M, Maquicienta 1.6M).
- **Es de Navidad y sale en diciembre.** Las notas sobre los capítulos navideños del Chavo se republican cada año (Milenio, SensaCine, Líbero): es búsqueda que vuelve sola en esta época.
- **Todos lo conocen.** El Plankton no era querido; el Chavo es de la familia de cualquier latinoamericano. Es el mismo control que el Homero, en otra serie.
- **El Iroh se descartó** porque el autor no conocía al personaje: señal de que el público tampoco.

## Lo que cambió al verificar (20 nov)

Se bajaron 3 capítulos del canal oficial "El Chavo del 8" con el **audio original en español** (la pista por defecto del canal es un doblaje automático al inglés: hay que pedir la de `es-US`), más las 3 partes de la versión de 1973 de un canal de fans, y se transcribieron con faster-whisper medium.

- **La escena de "preferiría comprar comida" y Don Ramón llorando, que contaban las notas de prensa, no está** en ninguno de los dos capítulos de Navidad. La versión oficial de "Navidad" es la misma de 1973 (Capítulo 41). Las notas parecen mezclar escenas.
- **Lo que sí está es más fuerte y pasa todo en un capítulo:**
  1. El Chavo le pide a Santa Claus **una torta de jamón**.
  2. Cuando lo invitan a cenar, tiene que escribirle otra vez a Santa **para que ya no se la mande**.
  3. Pide **"que todos los días del año sea Navidad"** (esa noche "no se pega").
  4. Le regala su camión, su único regalo, al bebé de la portera, tirándolo por la ventana para que crean que fue Santa: **"¿Qué no ves que es un niño pobre?"**
- **Don Ramón** queda en la comparación con dos escenas verificadas: no quiere llegar a la cena "con las manos limpias" (por no llevar nada), y en otro capítulo tira un peso al piso para que el Chavo crea que se lo encontró; el Chavo no lo agarra ("usted lo vio primero").
- **"El ratero de la vecindad"** confirma la segunda cita, textual: **"Pero yo no recé para que encontraran al ratero. Recé para que el ratero se arrepienta y se vuelva bueno."**
- **Afuera por no estar confirmados:** la Ciudad de México, la Bruja del 71 dándole una torta, el origen del Chavo según el libro de Chespirito y el nombre del ladrón (Whisper oye "señor Bolado"; las fichas dicen Hurtado).

## Empaque

- **Título A (recomendado):** La NAVIDAD que explica al Chavo del 8
  - La fórmula de la biblia, igual que el del Homero ("La PLACA que explica a Homero Simpson"): una sola palabra en mayúsculas, un objeto concreto y el personaje. Corto, para que no se corte en sugeridos.
- **Título B:** La CARTA del Chavo a Santa Claus que solo entiendes de grande
  - Para "Probar y comparar" si el A arranca mal.
- **Miniatura:** a la izquierda, en blanco y negro, el Chavo babeando frente a la torta de Kiko; flecha verde; a la derecha, a color, el Chavo en Nochebuena con el camión en las manos frente al nacimiento. Sin texto agregado. Receta de `../Shorts/build/thumb.py`.
- **Plano firma:** el barril del patio, vacío. Vuelve unas 5 veces; al final se entiende que es su casa.
- **Separadores:** el ámbar del canal.
- **Duración:** ~11 min (~1.650 palabras de narración + ~40 s de clips).
- **Fecha sugerida:** domingo 6 o 13 dic, 18:00.

### Música por bloque (regla del Calamardo)

| Bloque | Párrafos | Pista |
|---|---|---|
| Gancho y acto 1 | [01]–[12] | Snowdin Town |
| Nochebuena: la carta, la invitación, "que todos los días sea Navidad" | [13]–[18] | Home |
| **El camión** | [19]–[23] | **Sin música** (desde que reparte los regalos el señor Barriga) |
| Tesis y comparación | [24]–[33] | Fallen Down |
| El ratero y el cierre | [34]–[42] | Memory |

Todas a −27 dB, con ducking debajo del audio original.

### Fuentes (en `Downloads/Chavo/`, no van al repo)

| Id | Capítulo | YouTube | Uso |
|---|---|---|---|
| NAV | "Doña Florinda invites everyone to spend Christmas at her house" (= Navidad, Cap. 41, 1973) | `tuieZWj9lq4` (oficial, 15:02) | Escena clave y casi todo el video |
| NAV2 | "El Chavo del 8: Christmas" (después de Navidad) | `vYYgZgOW7NA` (oficial, 15:19) | El peso de Don Ramón (5:30–8:05) |
| RAT | "The Thief in the Neighborhood" (El ratero de la vecindad) | `DM-AWV-VG2k` (oficial, 14:13) | Segunda cita |
| — | Navidad 1973, partes 1–3 (canales de fans) | `t-M7gcmOuTw`, `zY60pEBlsQE`, `LWctOrhze5I` | Solo para confirmar que es el mismo capítulo; no se usan |

Transcripciones en el scratchpad de la sesión (`chavo/*_full.txt`, temporales).

### Tiempos verificados (subidas oficiales)

| Momento | Fuente | Tiempo | Texto (Whisper, corregido) |
|---|---|---|---|
| La carta | NAV | 1:52–2:10 | "¿Qué estás haciendo aquí?" / "Le estaba escribiendo mi carta a Santa Claus." / "¿Y qué le ibas a pedir?" / "Una torta de jamón." |
| La invitación | NAV | 2:10–2:31 | "Chavito, mira, ¿no te gustaría cenar esta noche en mi casa?" / "¿En su casa?" / "Sí, Chavo." / "¿Yo?" |
| Cancela la torta | NAV | 5:03 | "Pero le tengo que escribir a Santa Claus para que ya no me mande la torta de jamón." |
| "En Navidad no se pega" | NAV | 5:36–5:52 | "…hay que perdonar las ofensas, los agravios, las rentas atrasadas…" |
| El deseo | NAV | 6:14–6:22 | "¿Le estabas pidiendo algo a Santa Claus?" / "Sí. Que todos los días del año sea Navidad." |
| Manos limpias | NAV | 6:58–7:28 | Don Ramón: "Yo no quisiera llegar con las manos limpias." / "La pobreza no tiene que ver nada con la limpieza." |
| Barriga sin familia | NAV | 9:02–9:29 | No pudo pasar la Navidad con su familia "por los negocios"; Don Ramón: "nos considera a nosotros como su segunda familia" |
| Los regalos | NAV | 11:38–13:14 | "Caray, señor Barriga, pero cómo se fue usted a molestar." / "Pero si es cualquier cosa." (×5) |
| **El camión** | NAV | 13:14–14:35 | "¿Y a ti qué te regalaron?" / "Pues un camión." / "¿Y qué, no te da gusto?" … "la portera de la vecindad tuvo un niño, y está así, chiquito" … "se lo eché por la ventila de la ventana" / "¿Le regalaste tu camioncito al hijo de la portera?" / **"Pues claro. ¿Qué no ves que es un niño pobre?"** |
| El peso | NAV2 | 5:30–8:05 | "Hace frío, ¿eh?" / "Un poco." … Don Ramón tira el peso … "Usted lo vio primero." / "Te cedo los derechos, Chavito." … Kiko: "¡Mira lo que me encontré! ¡Un peso!" |
| La chusma | RAT | 7:11–7:30 | Doña Florinda: "¿Cuándo has visto a una persona decente en compañía de un ratero?" … "¿Cuántas veces te he dicho que no te juntes con la chusma?" |
| Por mayoría de votos | RAT | 10:25–10:50 | "Entré a buscar al padrecito para confesarme y decirle que yo era un ratero." … "No sería ratero de nacimiento, pero sí por mayoría de votos." |
| **El rezo** | RAT | 12:35–12:52 | "Pero ahí sí no funcionó, porque todavía no han encontrado al ratero." / "Pero yo no recé para que encontraran al ratero." / "Recé para que el ratero se arrepienta y se vuelva bueno." |
| La torta del ladrón | RAT | 13:19–13:25 | "Chavito, te traje una torta de jamón." |

**Antes de montar:** escuchar cada clip una vez. Whisper confunde nombres ("Girafales", "Flotilde") y los tiempos tienen ±1 s.

**Content ID:** los derechos están en Grupo Chespirito/Televisa. Mismo criterio que el Homero: ~40 s de audio original, ningún tramo seguido de más de ~20 s, recortar marcas de agua.

### Memes (1–2 s sobre negro, ~6)

Imágenes de corte, nada persistente en pantalla.

| # | Dónde | Meme |
|---|---|---|
| M1 | [09] | El Chavo babeando frente a la torta de Kiko |
| M2 | [08] | Don Ramón escondiéndose del señor Barriga |
| M3 | [16] | El señor Barriga recibiendo el golpe, con "Navidad no se pega" en serif |
| M4 | [19] | "Caray, señor Barriga, pero cómo se fue usted a molestar", en montaje rápido de las cinco veces |
| M5 | [30] | Kiko con el peso en la mano |
| M6 | [38] | La torta de jamón sola, en cámara lenta |

## Estructura

| Paso de la biblia | Párrafos | Tiempo aprox. | Qué se ve |
|---|---|---|---|
| 1. Gancho | [01]–[05] | 0:00–0:35 | La carta **con audio original** → el Chavo riéndose → el barril |
| 2. Spoilers | [06] | 0:35–0:42 | Separador ámbar |
| 3. Mapa | [07] | 0:42–0:50 | Placa "Don Bozz presenta" |
| 4. Acto 1 | [08]–[12] | 0:50–2:00 | El barril, Barriga y la renta, la torta de Kiko |
| 5. Acto 2: Nochebuena y el camión | [13]–[23] | 2:00–5:00 | La carta, la invitación, el deseo **(clip)**, los regalos, **el camión (clip), planos largos** |
| 6. Tesis | [24]–[27] | 5:00–6:15 | Relectura del acto 1 con los mismos planos |
| 7. Comparación + Homero + lección | [28]–[33] | 6:15–8:20 | Kiko, Doña Florinda, la Bruja, Barriga, Don Ramón y el peso |
| 8. Segunda cita | [34]–[38] | 8:20–10:00 | La chusma **(clip)**, la iglesia, el rezo **(clip)**, la torta del ladrón |
| 9. Cierre (sin anzuelo) | [39]–[42] | 10:00–10:50 | El barril de noche, la línea del canal, la pregunta para comentarios |

**La escena del camión va con planos largos** (9–13 s), a contramano del resto (un corte cada 2–4 s).
