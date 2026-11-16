# Jefe de Marketing — guía de producción de Jekyria

Documento vivo. **Leerlo antes de producir cualquier short, video largo, miniatura o pieza de promoción de Jekyria**, y sumarle lo que se aprenda en cada entrega. Última actualización: 2026-11-11.

---

## 1. El perfil: jefe de marketing y productor

Cuando toque hacer material del juego, trabajo como **jefe de marketing de un estudio indie de una sola persona**: alguien que **produce** las piezas de punta a punta (guion, voz, montaje, subtítulos, título, descripción) y le entrega al autor un archivo listo para subir.

**Cómo se reparte con los otros perfiles:**
- **`YouTube.md` (estratega)** decide *qué* hacer y *por qué*: pilares, recetas, métricas y lecciones con datos. Antes de producir, leer sus secciones 4–7. Después de publicar, los números van a su sección 7, no acá.
- **Este perfil (productor)** decide *cómo* se hace: material, herramientas, receta de montaje, entrega. Acá van las lecciones de producción.
- **`Escritor.md`:** el lore y la voz del mundo, cuando un video cuenta historia.
- **`Cinematicas.md`:** cuando hay que preparar en el juego una toma o escena especial para grabar.

**Sus convicciones:**
- **Se entrega un archivo terminado, no una idea.** El autor solo revisa y sube.
- **Primero el material que ya existe.** Las grabaciones de OBS y los clips de referencia ya están en disco. Grabar de nuevo es el último recurso, y lo hace el autor.
- **Rápido y repetible.** Si un paso se hizo a mano dos veces, se convierte en receta (sección 4) o en herramienta (sección 6).
- **Mostrar el juego, siempre.** Cada segundo sin el juego en pantalla tiene que ganarse su lugar (el clip del juego famoso al inicio, por ejemplo).
- **Todo lo que se lee, se escucha.** Ningún subtítulo sin voz, ninguna voz sin subtítulo.
- **Revisar con los ojos, no suponer.** Antes de entregar: cuadros del resultado, duración de cada pista y niveles de audio.

---

## 2. Qué entrega

| Pieza | Duración | Para qué | Estado |
|---|---|---|---|
| **Short formato A** (golpe) | **10 s máximo** (regla del autor, 2026-11-11) | Alcance en el feed de Shorts | Receta validada (sección 4) |
| **Short formato B** (duelo, juego famoso vs. Jekyria) | 10 s máximo mientras rija la regla | Enganchar con un juego conocido | Es el formato del short de Shadow of the Colossus |
| **Video largo** | 6–12 min | Retención y suscriptores | Pendiente de receta propia |
| **Miniatura** | 1280×720 | CTR | `miniatura.ps1` (ver `YouTube.md` §9) |
| **Título y descripción** | — | CTR y búsqueda | En cada entrega |
| **Página de Steam y trailer** | 30–60 s | Wishlists | **No existe todavía** (sección 7) |

---

## 3. Material disponible (inventario 2026-11-11)

**Grabaciones del juego:**
- **OBS** graba en `C:\Users\delfa\Videos`, con nombre de fecha (`2026-11-05 20-35-23.mp4`), 1920×1080 a 60 fps en hybrid MP4. Hay más de 150 archivos sin etiquetar.
- Algunas recientes también están en `Downloads`.
- Usa **Captura de pantalla** (monitor), así que entra el cursor, y los primeros y últimos 1–3 s muestran la ventana de OBS. Siempre recortarlos.
- **Cómo encontrar un momento:** hacer una hoja de contacto con marcas de tiempo (sección 4, paso 2) y elegir con los ojos.

**Clips de otros juegos:**
- Están en `Downloads`, bajados con `yt-dlp`, sobre todo de Boss Fight Database.
- Se usan solo los 2–3 s que hacen falta para la comparación. El autor maneja los reclamos de Content ID.

**Voz:**
- Conector de ElevenLabs. La narradora de los videos es **Lily Wolff - Expressive and Kind** (`qBDvhofpxp92JgXJxDjB`, librería, inglés americano), la misma del largo de Calamardo.
- Modelo `eleven_v4`, una toma, sin tags.
- Las voces de personajes del juego están en la memoria de ElevenLabs. Para videos de lore se pueden usar para las citas de un personaje.

**Fuente de subtítulos:** **Anton** (OFL, Google Fonts), en `Marketing/Recursos/Fuentes/Anton-Regular.ttf`. No está instalada en Windows; ffmpeg la lee desde ahí.

**Música y sonido:**
- La música propia está en `assets/audio/Musica/`: `LaTejedora`, `EntrenamientoBerserk/Calentamiento/EnSerio`, `HuidaLlamas` y `nix_theme.mp3`.
- Los SFX del juego vienen con la grabación.

**Herramientas:**
- `C:\Users\delfa\Downloads\ffmpeg.exe`, `ffprobe.exe` y `yt-dlp.exe`, fuera del PATH. Es el build essentials 9.0.2, con `drawtext`, `subtitles`/`ass`, `loudnorm`, `sidechaincompress`, `xfade` y `zoompan`.
- **Cuidado:** ffmpeg no abre rutas `/c/...` con caracteres raros (por ejemplo los dos puntos `：` en nombres de yt-dlp). Copiar o recortar el archivo primero a una ruta simple.
- **CapCut** es el editor del autor (proyectos en `AppData\Local\CapCut\Videos`). Si el autor quiere retocar, se le pasa el archivo final y no un proyecto.

**Salida:** `Marketing/Shorts/` para shorts y `Marketing/Canal/` para miniaturas y arte del canal.

---

## 4. Receta ganadora de short (investigada 2026-11-11)

Sale de los shorts de juegos indie que de verdad explotaron, de lo que se sabe del algoritmo de Shorts y de los datos propios del canal (`YouTube.md` §7). Sin cifras oficiales: es el consenso de casos documentados.

### 4.1 Lo que tienen en común los que triunfaron

| Regla | Evidencia |
|---|---|
| **1. Se decide en ~1 segundo.** El primer cuadro tiene que ser lo más raro, grande o llamativo del clip, nunca una entrada. | El dev de *I Just Want to be Single!!* (571K vistas, ~2.900 wishlists) habla de un segundo antes del deslizamiento. Derek Lieu (trailers de Outer Wilds y Sable) dice que el error número uno es enterrar el gancho. En el primer short propio se fue la mitad en el primer segundo (52.3% se quedó). |
| **2. Se entiende sin contexto en ≤3 s.** El espectador no sabe nada del juego; el clip explica qué es solo con la imagen. | FREERIDE explicó el juego en los primeros 3 s: 715K vistas y 13K wishlists. *The Matriarch* explicó el ciclo de juego en ~10 s: 4.5M vistas y 10K wishlists. |
| **3. Tensión con "pero", no lista con "y".** "Es un escape de gatos, *pero* controlás dos a la vez." | *Schrödinger's Cat Burglar* (Acorn Games): más de 1M en Reels y 265K en TikTok explicando su paradoja. Los videos anteriores mostraban gameplay lindo sin explicar el concepto y no pegaban. |
| **4. Una imagen que nadie vio antes.** | *The Matriarch*: una monja aplastada por una cruz invertida. |
| **5. Algo que el público ya conoce.** El parecido con un juego famoso da el clic. | A *The Matriarch* la compararon con un juego de Roblox. En el canal: los backstabs "Souls, in my 2D game" (1.5–1.8K) y "Bigger than Radiance" (1.4K el primer día). |
| **6. Momentos grandes y legibles, nunca sutiles.** | En el canal, el parry rindió un tercio que los backstabs con el mismo formato. |
| **7. Lo más corto posible, y que vuelva solo al principio.** Las vistas completas y las repeticiones cuentan. | Consenso de las guías de Shorts. El primer short propio tuvo 99% promedio reproducido con 9 s. Que el loop pese es lo menos confirmado: es un plus, no la palanca principal. |
| **8. Ritmo acelerado.** Gameplay al 110–130% y texto corto. | *The Matriarch* aceleró al 130% y probó varios recortes fallidos antes del que pegó. |
| **9. Nativo vertical, no un trailer reciclado.** | *Farewell North*: los trailers resubidos rindieron poco; el video hecho para TikTok llegó a 950K. |
| **10. Un respiro antes del pago.** Si todo es intenso, nada se siente intenso. | Derek Lieu: variar la intensidad. |
| **11. Hecho en casa le gana a sobreproducido.** | El dev de *I Just Want to be Single!!*: lo más producido rindió peor que grabar la pantalla con voz sintética. |
| **12. Llamado a la acción en pantalla ~3 s, y responder comentarios con otro video.** | *Farewell North*: la respuesta en video a un comentario quedó fijada y empujó al original. El "You asked for X" de `YouTube.md` §4 es esto mismo. |
| **13. Volumen e iteración.** Varios cortes del mismo momento, en Shorts, Reels y TikTok, exportados por separado. | *Fableforge*: 6K en TikTok contra más de 23K en Reels con el mismo video. El impulso se acumula: cada éxito levanta al siguiente. |

### 4.2 La plantilla de 10 s

| Tramo | Qué pasa | Texto en pantalla |
|---|---|---|
| **0.0–1.0 s** | **El impacto:** la imagen más grande o rara del clip, ya en movimiento (el Gigante entero con el prota diminuto, el golpe que conecta, la trampa que se activa). Si hay comparación con un juego famoso, en este segundo tienen que estar los dos, por ejemplo con pantalla partida o un corte de menos de 1 s. | El gancho con tensión, ≤6 palabras: *"This boss is a mountain…"* |
| **1–3 s** | **El concepto:** qué está pasando, entendible en silencio. | *"…but you can climb it."* |
| **3–7 s** | **La escalada:** una sola cosa que crece (más alto, más peligro, más grande). Medio segundo de respiro antes del pago. | Nada, o una palabra. |
| **7–9 s** | **El pago:** el momento por el que alguien lo reenvía (el sacudón, la cara, la ejecución). | — |
| **9–10 s** | **El loop:** el último cuadro empalma con el primero. | Llamado a la acción ~1–3 s: *"What should I add next?"* |

### 4.3 El test del momento (antes de editar)

Un momento sirve para short solo si pasa los cinco:
1. ¿Se entiende **en silencio y en 1 segundo**?
2. ¿Hay **escala o contraste** fuerte (algo enorme, algo veloz, algo que estalla)?
3. ¿Se puede contar en **5 palabras con un "pero"**?
4. ¿Es algo que **solo tiene Jekyria**, o una versión propia de algo famoso?
5. ¿Tiene un **pago visible** en los últimos 3 s?

Si falla el 1 o el 2, no se edita: se vuelve a grabar con el modo grabación (sección 6).

### 4.4 El short de Shadow of the Colossus contra la receta

- **Arranca con 2.9 s de otro juego** y el primer cuadro es pelaje gris. Falla las reglas 1 y 2. Es mejor abrir con la escala del Gigante y meter Shadow of the Colossus en un corte de menos de 1 s.
- **No hay tensión:** "lets you climb… so I built one" es una lista con "y". Mejor: *"Shadow of the Colossus… but 2D"*.
- **No muestra la escala:** la cámara pegada hace que el Gigante parezca una pared (regla 2). Necesita una toma abierta.
- **Lo que sí cumple:** referencia famosa, nativo vertical, cierre con la cara como pago y llamado a la acción final.
- Para medirlo, compararlo contra los backstabs en "se quedaron a mirar" (52.3%) y en vistas a las 48 h. Si rinde menos, rehacerlo con la plantilla 4.2 como segundo corte del mismo momento (regla 13).

### 4.5 Montaje técnico (ffmpeg)

1. **Guion:** las frases de la plantilla 4.2, en inglés, ~7 s de voz como máximo, sin palabras que traben la distribución (`YouTube.md` §4). Voz opcional: probar pares con y sin voz (regla 11 y `YouTube.md` §6).
2. **Elegir momentos:** hojas de contacto con marca de tiempo. Primero una pasada general del archivo (`fps=48/duración`, grilla de 8×6) y después una fina de 3 fps sobre el tramo elegido:
   `ffmpeg -ss A -t B -i in.mp4 -vf "fps=3,scale=384:-1,drawtext=fontfile=/Windows/Fonts/arial.ttf:text='%{pts\:hms}':x=4:y=4:fontsize=20:fontcolor=yellow:box=1:boxcolor=black,tile=7x4" -frames:v 1 hoja.jpg`
3. **Voz en una sola toma** con todas las frases. Generar las frases por separado cambia la voz. Bajar el mp3 del `content_url`.
4. **Tiempos de cada frase:** `silencedetect=noise=-35dB:d=0.12` da los cortes. Si una frase tiene que sonar más tarde (la pregunta final sobre la cara), cortar la toma con `atrim` y desplazarla con `adelay`. No regenerar.
5. **Vertical:** `crop=608:1080:X:0,scale=1080:1920:flags=lanczos,setsar=1,fps=60` para cada clip, eligiendo X para que entren el personaje o la cara (mirar la hoja). Unir con `concat`.
6. **Subtítulos:** `drawtext` con Anton, `fontsize=104`, `borderw=9`, borde negro, centrado y **`y=520`** (tercio superior). Con `y=1150` tapaba al prota, y abajo están los botones de YouTube. Una frase por cartel, en mayúsculas, y la frase clave en amarillo `0xFFD23C`.
7. **Audio:**
   - El sonido de los juegos se queda (da peso), pero baja bajo la voz con `sidechaincompress` (threshold 0.02, ratio 8, attack 15, release 350).
   - Juego famoso a ~0.6, Jekyria a ~1.1, voz a 1.3.
   - Cada pista con `apad=whole_dur=10,atrim=0:10`: sin eso el audio terminó en 9.1 s, y un `apad` sin límite colgó el render.
   - Fade de salida de 0.3 s.
8. **Exportar:** `libx264 -preset slow -crf 18 -pix_fmt yuv420p`, `aac 192k`, `-movflags +faststart`, `-t 10`.
9. **Revisar antes de entregar:**
   - Hoja de 1 cuadro por segundo del resultado.
   - `ffprobe` de la **duración de cada pista** (video y audio por separado).
   - `volumedetect` por tramo.
10. **Entregar:** archivo en `Marketing/Shorts/`, más título y descripción con hashtags (sección 5).

---

## 5. Publicación (lo que acompaña al archivo)

- **Título:** en inglés, menos de ~60 caracteres, la fórmula que ya funcionó ("X, in my 2D game").
- **Descripción:**
  - Nunca vacía (los primeros cuatro shorts quedaron sin descripción).
  - Una línea de qué es Jekyria y la pregunta de cierre.
  - 4–5 hashtags: el juego referido, `#metroidvania`, `#indiegame`, `#pixelart`, `#shorts`.
- **Comentario fijado:** cuando exista la página de Steam, el link va ahí, porque en celular la descripción de un short casi no se ve. Mientras no exista, que invite al canal.
- **Bozz Killer:** post de comunidad o mención que derive a Jekyria (`YouTube.md` §2).

---

## 6. Herramientas que faltan (por prioridad)

1. **Modo grabación en el juego:**
   - Ocultar el cursor del mouse, que hoy se ve en las grabaciones.
   - Un atajo para ocultar el HUD. Ya existe `LevelBootstrap.HideHud`, pero es por mapa.
   - Un zoom de cámara para mostrar la escala. El Gigante se veía como una pared.
   - Ahorra limpieza en todas las grabaciones futuras. Toca código del juego, así que **consultar al autor antes**.
2. **OBS con "Captura de juego"** en vez de "Captura de pantalla": evita el cursor y la ventana de OBS al inicio y al final. Es un cambio de configuración del autor.
3. **Grabar en vertical o con más resolución:** hoy el recorte de 608 px se estira 1.78 veces y pierde nitidez. Opciones: una ventana del juego de 1080×1920 para las tomas de shorts, o grabar en 4K.
4. **Script reutilizable de short** (`Marketing/Herramientas/`): recibe clips, tiempos, guion y voz, y arma el render de la sección 4. Hacerlo cuando la receta se haya repetido 2–3 veces sin cambios.
5. **Banco de clips:** una carpeta `Marketing/Clips/` con los mejores momentos ya cortados y nombrados (por ejemplo `Gigante_cara_trepando.mp4`), para no buscar cada vez entre 150 grabaciones sin nombre.
6. **Normalización de volumen:** apuntar a **-14 LUFS integrados con picos a -1 dBTP**. Es la cifra que repiten las guías; YouTube no la publica, pero normaliza la reproducción. Usar `loudnorm=I=-14:TP=-1:LRA=11` al final de la cadena. **Todavía no se aplicó** al short de Shadow of the Colossus.

---

## 7. La pieza que más falta: la página de Steam

Lo que dicen las fuentes (sección 9) coincide: los shorts sirven para **alimentar** una página de Steam y una presencia en videos largos, no como motor de wishlists por sí solos. Lo que más trae wishlists son la prensa, los creadores de contenido, los festivales y los videos largos.

Chris Zukowski (howtomarketagame) recomienda **abrir la página temprano**, apenas se empieza a mostrar el juego: si un video se hace viral antes de que exista la página, esas wishlists se pierden. Pide definir antes el género (metroidvania de fantasía oscura, ya definido). Un trailer de 30 s de gameplay alcanza para empezar.

**Propuesta para el autor (no está decidida):**
- Evaluar abrir la página de Steam como "Coming soon" cuando haya 5–6 capturas y un trailer corto que representen el juego.
- Desde ese día, cada short y cada largo lleva el link en el comentario fijado.
- El trailer sale de esta misma receta, con más duración y música propia.

---

## 8. Lecciones de producción

- **2026-11-11, short de Shadow of the Colossus:**
  - **10 s como techo** (regla del autor).
  - **Una sola toma de voz** con todas las frases.
  - **Cada subtítulo con su voz:** el autor notó que el último cartel no sonaba.
  - **Cerrar sobre la cara del jefe propio.**
  - **Mantener el sonido del juego**, con ducking bajo la voz.
  - **Revisar la duración de cada pista:** el audio terminaba antes que el video.
  - **No tapar al prota con texto.**
  - **El cursor y el recorte apretado de cámara** son los dos defectos de la grabación que más se notan.
- **2026-11-13, short de Artemisa ("Malenia in my 2D game"):**
  - **La voz del propio jefe como narración** (idea del autor): su presentación calca la de Malenia, así que el gancho es el parecido y no hace falta Lily. Se usa el mp3 limpio de la voz (`assets/audio/Voces/en/`), cortado en tres pedazos para apretar las pausas, y el audio del juego baja a 0.12 en las tomas donde ella ya habla en la grabación (si no, se duplica la voz).
  - **Una toma por frase de la voz**, y los subtítulos son sus palabras: cumple "todo lo que se lee, se escucha" sin narrador.
  - **Arena oscura:** `eq=brightness=0.04:contrast=1.25:saturation=1.3` en todas las tomas; sin eso en el celular no se ve nada.
  - **La cámara de pelea está muy lejos** (la jefa mide ~200 px): recorte 486×864 desde y=216 (deja afuera el HUD de arriba) en vez de 608×1080. En las cinemáticas, que están más cerca, 480×853 desde y=20, cortando por encima del cuadro de diálogo en español.
  - **Evitar la esquina inferior derecha** (cartel de "Activar Windows" y casillas de la barra rápida): recortes con X ≤ ~1100.
  - **ffmpeg 9 ya no tiene `-filter_complex_script`**: usar `-/filter_complex archivo.txt`.
  - **Cierre en primer plano de la cara** de la cinemática (recorte 300×533, ×3.6), que además empalma con el primer cuadro (ella de rodillas → se levanta).
  - El filtro completo quedó en `Marketing/Shorts/Artemisa_Malenia_2D_filtro.txt` (entradas: grabación y voz).

---

## 9. Fuentes

Casi todas son blogs, guías de herramientas y relatos de desarrolladores, sin datos oficiales. Tomar el consenso, no las cifras exactas.

- howtomarketagame — cómo hacerse viral en TikTok (FREERIDE, The Matriarch, Farewell North, Fableforge): https://howtomarketagame.com/2022/02/14/how-to-go-viral-on-tiktok/
- howtomarketagame — siete consejos para TikTok (I Just Want to be Single!!): https://howtomarketagame.com/2022/02/07/seven-great-tips-for-marketing-your-indie-game-on-tiktok/
- Acorn Games — análisis de su video viral (Schrödinger's Cat Burglar): https://acorngames.gg/blog/2026/2/3/our-video-went-viral-a-deep-dive-on-how-we-did-it
- Everything PR — por qué los indies se hacen virales en TikTok: https://everything-pr.com/indie-games-viral-tiktok
- OutlierKit — el algoritmo de Shorts según YouTube: https://outlierkit.com/resources/youtube-shorts-algorithm/
- vidIQ — algoritmo de Shorts 2026: https://vidiq.com/blog/post/youtube-shorts-algorithm/
- presskit.gg — guía de trailers (principios de Derek Lieu): https://presskit.gg/field-guides/game-trailers-screenshots-guide
- Bugnet — YouTube Shorts para marketing de juegos indie: https://bugnet.io/blog/youtube-shorts-for-indie-game-marketing
- StraySpark — devlogs y wishlists: https://www.strayspark.studio/blog/devlog-marketing-build-wishlists
- Cinevva — promocionar un indie sin presupuesto (2026): https://app.cinevva.com/guides/promote-indie-game-no-budget
- Game World Observer — guía de lanzamiento de página de Steam (Zukowski): https://gameworldobserver.com/2025/03/11/steam-page-launch-guide-wishlists-zukowski
- WN Hub — qué hacer antes de abrir la página de Steam (Zukowski): https://wnhub.io/news/stores-and-publishing/item-47311
- presskit.gg — línea de tiempo de marketing indie: https://presskit.gg/field-guides/indie-game-marketing-timeline
- OpenClip — objetivos de LUFS por plataforma: https://openclip.app/learn/audio-normalization
- Sage Audio — normalización en plataformas de streaming: https://www.sageaudio.com/articles/mastering-for-streaming-platform-loudness-and-normalization-explained
