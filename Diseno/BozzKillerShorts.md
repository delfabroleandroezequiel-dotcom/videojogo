# Bozz Killer — Receta de Shorts "¿Qué tanto sabes de X?"

Canal: **Bozz Killer** (@bozz_killer, ~11K subs, español). Es una receta distinta de la de Jekyria (`JefeMarketing.md`, en inglés). Los shorts de Bozz Killer **no** siguen esa receta.

Ejemplos de referencia, que el usuario dejó el 2026-11-18 en `Downloads/`:
- `Jefes de Mario #mario #shorts #boss [eg4wnIzxuaw].mp4`: 9.97 s
- `Jefes de Cuphead #cuphead #shorts #boss [ymJW0l--0eE].mp4`: 9.77 s

---

## 1. Reglas fijas (no se tocan)

| Regla | Detalle |
|---|---|
| **Intro** | "¿Qué tanto sabes de [JUEGO]?". Se copia **tal cual** de los ejemplos (ver §3). |
| **Cierre** | Gatitos con el objeto del juego + "**¡Increíble!**" (ver §5). |
| **Duración** | **Estricta: 9.30 s a 10.45 s.** Fuera de ese rango no se entrega. |
| **Voz** | ElevenLabs PVC **"bozz profesional"** `zPOECovllWo8fPEHcouP`, que es igual a la voz del usuario. **Nunca** el clon instantáneo "Bozz Killer" `seOn15qLHcKLOg5XXTjt`, la versión "toqueteada". |

## 2. Formato técnico

- 1080×1920 vertical, 60 fps (los ejemplos están a 59.9).
- Audio a 48 kHz, masterizado alto: **≈ -11 LUFS integrado**.
- Narración continua de principio a fin, sin silencios largos.

## 3. Intro (≈ 0.0 – 1.7 s)

- Fondo: imagen o clip cinemático del protagonista (render 3D de Mario, plano de la cinemática de Cuphead), a pantalla completa.
- Texto centrado en el tercio superior, en blanco con contorno negro grueso, en mayúsculas y sin signos.
- Las palabras aparecen de a una y se acumulan, sincronizadas con la voz:
  1. `QUE TANTO`
  2. `SABES` (en la línea de abajo)
  3. `DE` (en la línea de abajo)
  4. **Logo oficial del juego** en lugar del nombre escrito.
- Corte directo al cuerpo a los ~1.65–1.85 s.

## 4. Cuerpo (≈ 1.7 – 9.0 s): un solo dato

- Arranca con **"Sabías que…"** y cuenta **un** dato curioso o secreto: un pasaje oculto, una vida extra, un easter egg.
- Ejemplo Mario: "Sabías que en el mundo 8-2 existe un pasaje secreto donde puedes obtener un podersito… money money money money".
- Ejemplo Cuphead: "Sabías que puedes aumentar tus vidas cuando derrotes al genio… solo debes girar 3 veces en círculo… el mismísimo Kratos".
- **Clips:** gameplay que muestra exactamente lo que dice la voz, con un corte cada ~1–2 s.
- **Subtítulos:**
  - 1 a 3 palabras por vez, palabra por palabra o en bloques cortos.
  - Fuente condensada tipo **Anton** (`Marketing/Recursos/Fuentes/Anton-Regular.ttf`), en blanco con contorno negro.
  - **Palabras clave en amarillo**: el remate o lo llamativo ("MONEY MONEY", "EL MISMÍSIMO KRATOS", "AUMENTAR TUS VIDAS").
  - Posición: zona central, o debajo del gameplay cuando el clip está en franja.
- **Clips horizontales:** franja centrada con el fondo difuminado o negro arriba y abajo.
- Tono: el remate es gracioso o exagerado (meme, referencia absurda).

## 5. Cierre (últimos ≈ 0.9 s)

- Corte a fondo negro.
- Imagen de **gatitos (grises, ojos naranjas, boca abierta) con el objeto icónico del juego**: gorras de Mario, tazas de Cuphead.
- La imagen entra con un **pop/zoom de chica a pantalla completa** en ~0.6 s.
- Voz: "**¡Increíble!**". Es el pico de volumen del short.
- Termina ahí, sin outro ni "suscribite".

## 6. Publicación

- Título: `Jefes de [JUEGO] #[juego] #shorts #boss`.

## 7. Flujo de producción (definido 2026-11-18)

- **Fuente de los datos:** un video largo de curiosidades de otro canal (por ejemplo "25 secretos de Cuphead DLC"). **1 video largo de N curiosidades = N shorts.**
- **Música:** la pone el usuario a mano después. Yo entrego solo la voz (y el audio que ya traen la intro y los gatitos).
- **Intro y gatitos:** se cortan de los shorts de ejemplo y quedan como plantillas en `Marketing/BozzKiller/Plantillas/`:
  - `Intro_Cuphead.mp4` 1.836 s, `Gatitos_Cuphead.mp4` 0.926 s (incluye "¡Increíble!")
  - `Intro_Mario.mp4` 1.636 s, `Gatitos_Mario.mp4` 0.859 s
  - `Intro_GTA.mp4` 1.486 s, `Gatitos_GTA.mp4` 0.699 s (sacados del short propio "Jefes de GTA" `TwDqW3jOa58`; gatitos con camisa hawaiana y cadena "Vice City")
- **Marcas del video fuente: que NO aparezcan.** El recorte vertical centrado del 1920×1080 (`crop=608:1080:656:0`, escalado a 1080×1920) deja afuera las esquinas.
  - Nintendúo tiene el número de curiosidad arriba a la izquierda y el logo abajo a la derecha.
  - Hay que evitar sus placas de título o logo a pantalla completa y el outro.
  - Revisar en cada clip que no quede texto o logo del canal dentro de la franja.
- **Pasos:**
  1. Transcribir el largo con faster-whisper (py 3.12, modelo medium; pasarle muestras de un WAV porque `av.open` falla).
  2. Separar las curiosidades con sus tiempos.
  3. Escribir el guion "Sabías que…": cuerpo de voz de ~6–7 s, español neutro como los ejemplos.
  4. Generar la voz con ElevenLabs, voz `zPOECovllWo8fPEHcouP`, modelo `eleven_multilingual_v2` (el más fiel para PVC).
  5. Sacar los tiempos por palabra con whisper small.
  6. Armar el cuerpo: clips de la curiosidad más subtítulos drawtext Anton 165 px, borde 10, y=1080/1270, amarillo en las claves. La voz entra a +0.1 s con loudnorm a -11 LUFS.
  7. Concatenar intro + cuerpo + gatitos.
- **Cálculo de duración:** intro 1.836 + cuerpo + gatitos 0.926 tiene que caer en 9.30–10.45 s, así que el cuerpo de Cuphead va de 6.54 a 7.69 s.
- **Salida:** `Marketing/BozzKiller/<Juego>/ShortNN_<Tema>.mp4`
- **Flow de ElevenLabs:** "Bozz Killer - Shorts Cuphead" `8xhoSTVTOkNEYS6vdUZH`

## 8. Pendiente de aprender

- ¿Cómo se eligen el juego y el video fuente? (Mario y Cuphead son los que más rinden, 15–55K).
- Descripción y otros detalles de publicación.

## 9. Lecciones de producción

- 2026-11-18: Short01_AranaGanster fue la prueba de voz.
  - La v1 salió **saturada**. La toma de ElevenLabs ya viene caliente (pico -0.4 dB, ~-12/-13 LUFS) y yo la subía con loudnorm a -11.
    → **No subir la voz.** Dejarla como viene con `volume=-1dB`. Si una toma suena saturada igual, regenerarla: a veces pasa.
  - La v1 tenía una **congelación al final**: la fuente tiene imágenes quietas (zoom estático 74.8–78.5 s en el video de Nintendúo).
    → **Pasar `freezedetect=n=0.003:d=0.3`** sobre la fuente antes de elegir clips y sobre el short final. Nunca usar tramos quietos.
  - La v2 dura 10.01 s, sin congelaciones.
  - **En pantalla siempre tiene que verse lo que se nombra.** En la v2, desde el segundo 5 se veía a Cuphead peleando pero la araña quedaba fuera del recorte centrado.
    → **Encuadrar sobre el sujeto**: mover el `x` del crop hacia donde está.
    → Cuando la acción es ancha, usar **layout "fit"**: la franja 16:9 recortada `crop=1460:1080:180:0` (deja afuera las dos marcas) escalada a 1080 de ancho, arriba del centro, con fondo del mismo clip ampliado + boxblur + oscurecido.
    → **Un still con zoom lento (zoompan 1.0→1.12) es válido** y no cuenta como congelación.
    → El usuario permite sacar capturas o clips de **otras fuentes**: no hace falta que todo sea del video largo.
- 2026-11-18, primer lote: Cuphead Short02-06 (fuente Nintendúo DLC) y Mario Short01-05 (fuente Nintendúo "25 Secretos SMB3", `L8TOybD7tZg`, bajado con yt-dlp).
  - **Herramientas en `Marketing/BozzKiller/Herramientas/`:**
    - `render_short.py`: arma el short a partir de un spec JSON con clips crop/fit/box/still y subtítulos. Verifica la duración y las congelaciones y deja una hoja de previa.
    - `words.py`: tiempos por palabra de las voces.
    - `transcribir.py`: transcripción del video largo.
    - `specs/`: los specs de este lote, como ejemplo.
  - **Rangos de duración del cuerpo:**
    - Cuphead (intro 1.836 + gatitos 0.926): de **6.54 a 7.69 s**.
    - Mario (intro 1.636 + gatitos 0.859): de **6.81 a 7.95 s**.
    - Guion de voz ≈ 6–7 s, unas 20–25 palabras. Un "Sabías que…" de 15 palabras queda corto (4.5 s) y hay que regenerarlo más largo, sumando un remate o un dato extra.
  - **Subtítulos:** con `enable='gte*lt'` (fin exclusivo) dos frases no se pisan en el corte.
  - **Planos fit:** por defecto `crop=1200:1080:360:0` (más grande que 1460 y sin las esquinas).
  - **Planos box:** recorte libre 9:16 sobre el sujeto. Si `y+h<1000`, se puede llegar hasta x 1900 sin que entre el logo.
  - **NES (Mario 3):** freezedetect marca pantallas casi quietas (mapas, Mario agachado). No son congelaciones reales. Para tramos realmente quietos (Mario agachado 5 s) usar still con zoom.
- 2026-11-20, lote GTA 6: Short01-07 en `Marketing/BozzKiller/GTA/` (fuente: "GTA 6: Curiosidades y secretos del mapa", `P_nTbkZPU3U`; transcripción en `Marketing/BozzKiller/Descargas/P_nTbkZPU3U/`). Specs en `Herramientas/specs/mkspecs_gta.py` (g01-g07). Flow de ElevenLabs: "Bozz Killer - Shorts GTA" `j8oRkE8pT1S529artmki`.
  - **Rango del cuerpo GTA** (intro 1.486 + gatitos 0.699): de **7.12 a 8.27 s**, unas 23-27 palabras. Es más largo que Mario/Cuphead porque la intro y los gatitos son más cortos.
  - El short original de GTA del usuario dura 9.61 s, va a -10 LUFS y tiene **todos los subtítulos en blanco**. En el lote mantuve el amarillo en las palabras clave, como en Mario/Cuphead.
  - **La fuente de YouTube baja en 720p AV1**: la paso a 1920×1080 H.264 (`scale=1920:1080`, crf 16, `-g 30`) para que `render_short.py` (que asume 1920 de ancho) recorte bien y el seek sea rápido.
  - **GTA no trae marcas de canal**, pero sí el minimapa (abajo a la izquierda) y los avisos de botones (abajo a la derecha). El recorte centrado y el fit 1200 los dejan afuera. Evitar las pantallas del mapa del menú de pausa.
  - Las hojas con `fps=1` y `-ss` antes del input pueden estar corridas ~1 s: en la cárcel, `t=1090` caía en el mapa. Revisar siempre el primer frame de cada clip en la hoja de previa.
  - **Descarga de las voces del flow:** pegar el `full_res_url` tal cual en curl. Si se re-codifica la firma (`--data-urlencode`), el CDN devuelve 403.
  - Las voces salieron entre -12.6 y -15.4 LUFS. Los shorts quedan en ~-13 LUFS con la voz a -1 dB; la música del usuario lo sube después.
- 2026-11-22, lote Mario Wonder: Short01-03 en `Marketing/BozzKiller/MarioWonder/` (fuente **en inglés**: Nintenduo World "25 SECRETS in SUPER MARIO BROS WONDER", `IuiFRG3nY_w`, 1,2 M vistas; bajada en `Descargas/IuiFRG3nY_w/fuente.mp4`). Specs en `Herramientas/specs/mkspecs_wonder.py` (w01-w07; Short04-07: flores víctimas, Mario realista, Goomba muerde, Yoshi sandía). Flow ElevenLabs: "Bozz Killer - Shorts Mario Wonder" `btkSPi9fOXqiKIO6tU8M`.
  - Con fuente en inglés no hace falta whisper para el largo: `yt-dlp --write-auto-subs --sub-langs en` da el VTT y alcanza para separar las curiosidades (`transcripcion_tiempos.txt`). El guion se escribe directo en español.
  - El formato 299 (1080p60 H.264) a veces no está disponible por id: usar `-f "bv*[height<=1080][vcodec^=avc1]+ba[ext=m4a]"`. Ya baja en 1920 de ancho, sin reconvertir.
  - El canal inglés de Nintenduo mete planos del presentador con webcam y placas con su logo a pantalla completa entre curiosidades: revisar la hoja y saltarlos.
  - Confirmado otra vez: las hojas `fps=1` con `-ss` antes del input salen corridas ~1 s. Antes de fijar un corte delicado (bordes de placa/logo, cambio de escena), sacar frames exactos con `-ss T -frames:v 1` y revisar esos.
  - Los menús de opciones son casi estáticos: van como `still` con zoom, no como clip (si no, freezedetect los marca).
- 2026-11-23, lotes Mario clásico + World + **Dark Souls** (fuentes en inglés):
  - `MarioClasico/Short01-04` (NES; Nintenduo World "25 SECRETS of SUPER MARIO BROS", `5I9gkzpffh8`). Specs `mkspecs_clasico.py`.
  - `MarioWorld/Short01-03` (Jump Of Time "30 AMAZING Secrets... Super Mario World", `WhPwDlickWM`; marca "JUMP TIME" abajo a la derecha, la saca el fit 1200; evitar sus placas con número). Specs `mkspecs_ds_world.py`.
  - `DarkSouls/Short01-03` (STAR RUN "odd and obscure things in dark souls", `916iheNY3rE`, 1080p30, sin marca de canal; ojo con sus textos propios arriba a la derecha y los títulos "BANSHEE"/"CRYSTAL KNIGHT"). Specs `mkspecs_ds_world.py`.
  - **Plantillas Dark Souls** sacadas del short propio "Jefes de Dark Souls" (`RaasVJ76_es`, 360×640 30 fps, reescalado a 1080×1920 60 fps): `Intro_DarkSouls.mp4` 1.768 s, `Gatitos_DarkSouls.mp4` 0.722 s (los gatitos dicen "DARK SOULS II"). Cuerpo DS: **6.81 a 7.96 s**. Freezedetect marca t=0 por la intro (es del short original, no es problema).
  - Si YouTube da 429 en los subtítulos, transcribir con faster-whisper `small` y `language="en"` (copia de `transcribir.py` con el idioma cambiado).
  - `hoja2.py` (scratchpad, un seek exacto por cuadro) reemplaza a las hojas `fps=1` corridas: sus tiempos coinciden con los del render.
  - Voz larga que se pasa del rango por ~0.4 s: `atempo=1.07` en vez de regenerar.
- **2026-11-23, Cuphead lote 2 (Short07-11, fuente JustoAllDay "18 Random Cuphead Facts" `r72F3rf-UDA`, en inglés):**
  - **La fuente trae subtítulos quemados abajo (y>~900) y el número de dato arriba a la derecha.** `render_short.py` ahora acepta `y`/`h` en modo `fit` (por defecto 0/1080, no cambia lotes viejos): recorte `x=60 w=1500 y=0 h=880`.
  - **Verificar el dato contra el video:** "Ms. Chalice se vuelve estatua de oro con Cala Maria" solo pasa con el filtro Gold Chalice del DLC (lo aclara un cartel). Se descartó.
  - **Un recuadro dentro del video (picture-in-picture)** puede ser el mejor plano: el pájaro muriendo se recortó solo de ese recuadro (`x=1195 w=660 y=560 h=340`).
  - Specs en `Herramientas/specs/mkspecs_cuphead2.py`, voces en `Cuphead/voces2/`.
- **2026-11-25, lote vacaciones (15 shorts, fuentes en inglés):**
  - `Mario64/Short01-05` (Nintenduo World "25 SECRETS in SUPER MARIO 64", `xAhXaaxYhdg`), `Cuphead/Short12-16` (The Leaderboard "Cuphead - Easter Eggs, Secrets and References", `1slEGU4AX0E`), `GTA5/Short01-05` (IGN "GTA 5: 29 Secrets and Easter Eggs", `yVbPvycZ_Xg`, 720p → `fuente1080.mp4`). Specs en `Herramientas/specs/mkspecs_vacaciones.py`. Flow ElevenLabs: "Bozz Killer - Shorts Vacaciones" `gBOzAttUIwumDbEBLDJd`.
  - `render_short.py` ahora acepta `"src"` por clip (para meter un plano de otra fuente; c2 usa SMW de `WhPwDlickWM`).
  - **IGN:** número de secreto arriba a la izquierda y logo IGN abajo a la derecha; crop centrado o fit `x=360 w=1200 y=80 h=860`. Mucha toma casi quieta (paseos de noche, mira, paredes con texto): freezedetect las marca → pasarlas a `still` con zoom.
  - **Voces largas:** de 20 guiones, 4 se regeneraron más cortos (~20 palabras para Mario/Cuphead) y 4 llevan `atempo` 1.03-1.08. Las pausas de la PVC no bajan de -35 dB, así que `silenceremove` no acorta nada.
  - **Chequear que la imagen coincide con el guion antes de grabar la voz:** la fuente decía "mariposas del desierto" pero mostraba otro nivel → se regrabó sin "del desierto".
  - **Plantilla GTA:** la intro y los gatitos muestran el logo de **GTA VI**; "GTA" en este canal es **GTA 6**: el lote de GTA 5 fue un error y quedó aparte en `GTA5/`. Los de GTA 6 de este lote son `GTA/Short08-12` (fuente T5G "30 Secrets & Easter Eggs In GTA 6!" `V9JgeA64VYE`, specs `mkspecs_gta6b.py`; los subtítulos de YouTube dieron 429 y se transcribió con faster-whisper small).
