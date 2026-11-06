# Cinemáticas — guía de dirección

Documento vivo. **Leerlo antes de armar o tocar cualquier cinemática** y sumarle lo que se aprenda en cada una. Última actualización: 2026-10-28.

---

## 1. El perfil: director de cinemáticas

Cada vez que toque una cinemática, trabajo con el rol de **director y cineasta de juegos 2D**: alguien que viene del cine, pero sabe que en un juego la cinemática le quita el control al jugador y tiene que ganarse ese tiempo.

**Sus convicciones:**
- **Cada cinemática cuenta una sola cosa.** Si no puedo decir en una frase qué cambia al final ("Delfrad entiende que su padre lo protege"), no está lista.
- **Corta y en el motor.** Personajes y escenarios reales, nada pre-renderizado. Lo ideal es menos de 60 segundos; si es más larga, partirla o devolver el control en el medio.
- **Mostrar antes que decir.** Un gesto, una mirada o un silencio valen más que una línea de diálogo.
- **El ritmo es el oficio.** La mayor parte del trabajo está en los tiempos: pausas entre líneas, la reacción del que escucha, cuánto se sostiene un plano.
- **Cámara sobria.** Un movimiento por idea. Nada de cámara hiperactiva ni temblor sin motivo, que marea a un jugador acostumbrado a manejar la cámara.
- **Respeto por el jugador.** Siempre se puede saltear, nunca se repite sin permiso, y nunca se le quita el control en medio del peligro sin aviso.
- **Las muertes de personajes tienen peso.** Plano propio, música, silencio, tiempo (ver la memoria de "muertes con peso").

---

## 2. Gramática de planos adaptada a un side-scroller 2D

En 2D no hay lentes ni ángulos: los planos se arman con **zoom + punto de foco + encuadre** (`StepCamera`: `Target`, `Weight`, `Zoom`).

| Plano | Cómo se logra acá | Para qué |
|---|---|---|
| **General / establecimiento** | Zoom 0.9–1.0, foco en el centro del lugar | Abrir una escena: dónde estamos y quién está. Casi siempre es el primer plano. |
| **Medio (dos personajes)** | Zoom 1.15–1.3, foco en el **punto medio** entre los dos (`Weight` 0.5) | Diálogos. Que entren los dos con aire alrededor. |
| **Primer plano** | Zoom 1.5–1.8, foco en un personaje (`Weight` 1) | Momentos emocionales, una revelación, la reacción clave. Usarlo poco. |
| **Plano de reacción** | Mover el foco hacia el que escucha durante la línea del otro | Que el silencio del que escucha diga algo. |
| **Inserto** | Foco en un objeto (Marker2D) | La espada que le regala el padre, el amuleto de Matheo. |

**Reglas de encuadre:**
- **Regla de 180°, versión 2D:** si dos personajes hablan, quedan enfrentados izquierda/derecha **toda la escena**. No cruzarlos ni darlos vuelta sin motivo. Quien "entra" a la historia lo hace por el lado del que viene en el mapa.
- **Aire de mirada:** si un personaje mira a la derecha, que haya más espacio a su derecha (correr el foco un poco hacia donde mira).
- **Tercios:** con el foco en el punto medio, los personajes caen cerca de los tercios. Que nadie quede pegado al borde ni tapado por el cuadro de diálogo (abajo) o las franjas negras (arriba).
- **Profundidad 2D:** los frentes con parallax (árboles y casas) y el polvo dan profundidad gratis. En un primer plano, que un frente cruce el cuadro despacio queda muy cinematográfico.

---

## 3. Ritmo y tiempos (números de partida)

- **Lectura:** los estándares de subtítulos para adultos rondan **15–17 caracteres por segundo** (Netflix usa 20 como tope). Si una línea avanza sola: `AutoAdvanceSeconds ≈ caracteres / 15 + 0.6` (mínimo 1.8 s). Si avanza con botón, no hace falta.
  - Líneas cortas: hasta ~80 caracteres. Una idea por línea.
- **Pausas:**
  - Entre líneas de distintos personajes: 0.3–0.6 s.
  - Antes de una respuesta importante: 0.8–1.2 s (el silencio también actúa).
  - Después de la línea más fuerte de la escena: sostener el plano **1.5–2 s** antes de seguir.
- **Movimiento:** caminar a un lugar no debería durar más de ~2 s en pantalla. Si es más lejos, cortar con un fundido corto o arrancar al personaje más cerca.
- **Cámara:** dejarla viajar 0.5–0.8 s antes de la línea siguiente (`StepCamera.WaitSeconds`).
- **Fundidos:**
  - 0.3–0.6 s: elipsis dentro de la misma escena ("más tarde").
  - 1.2–2 s a negro: cierre de un capítulo o de un momento fuerte.
  - Desde negro al abrir: 1–1.5 s.
- **Temblor:** solo con una causa en pantalla (rugido, impacto, derrumbe). Proporcional y corto.

---

## 4. Sonido y música (lo que más vende y lo que más falta)

- **La música entra antes del momento**, no junto con él. Una muerte o una revelación muchas veces suena mejor con **silencio** (cortar la música es un recurso).
- **Corte J o L:** que el sonido de la escena siguiente empiece unos instantes antes del corte (J) o que el de la anterior se arrastre después (L). Suaviza los fundidos.
- **Ambiente:** nunca silencio digital. Fuego, viento, mar de fondo.
- **Voces:** si hay voz, la línea dura lo que dura el audio.
- Pendiente de herramientas: hoy no hay pasos de Música ni de Sonido (ver §7).

---

## 5. Proceso de trabajo para cada cinemática

1. **Una frase:** qué cambia al final.
2. **Beats:** lista de 4 a 8 momentos (entrada, conflicto, giro, reacción, salida).
3. **Storyboard en texto:** por beat, plano (general, medio, primer plano), quién se mueve, qué se dice, sonido.
4. **Blocking:** armar los pasos en el Inspector con texto de relleno. Mirarla entera y corregir ritmo y encuadre ahora, que es barato.
5. **Pulido:** tiempos finos, sonido, luz y efectos (desenfoque, velo, temblor).
6. **Prueba:** verla jugando desde el punto de entrada real. ¿Se entiende sin diálogo? ¿Sobra algo? ¿Se puede saltear?
7. **Registro:** anotar acá lo aprendido.

**Checklist antes de darla por buena:**
- [ ] ¿Abre con un plano que ubica?
- [ ] ¿Los enfrentados respetan su lado toda la escena?
- [ ] ¿Ninguna línea queda tapada o pegada al borde?
- [ ] ¿Las pausas dejan respirar la línea fuerte?
- [ ] ¿Cada movimiento de cámara tiene motivo?
- [ ] ¿Dura lo menos posible para contar lo que cuenta?
- [ ] ¿Se guarda como vista y no se repite sola?

---

## 6. Herramientas que tenemos (`scripts/world/cinematica/`)

- **`Cinematic`** (nodo en la escena, sin autoload):
  - Bloquea al jugador con `HoldForCutscene` (franjas y cámara guiada).
  - Los pasos se ejecutan en orden y el mundo no se pausa.
  - Se dispara con `PlayOnStart`, desde código o con un trigger.
  - `PlayOnce` + `PersistenceId` la guardan como vista por partida.
  - `RequiresQuestCompleted` (2026-11-05): solo se reproduce si esa quest está terminada. Sirve para una escena de más adelante en un mapa que se visita antes (la espada de Arthur en Casa1, después del carro de Morlok).
  - **Saltear** (2026-11-05): mantener **Saltar** (`SkipAction`, por defecto `jump`) durante 1.2 s (`SkipHoldSeconds`). Abajo a la derecha aparece un anillo que se llena y el texto "Mantén Saltar para omitir" (`CIN_SALTEAR`); cualquier botón lo muestra 2.5 s para que el jugador sepa que existe. Un botón que ya venía apretado al empezar no cuenta.
    - Al saltear: negro corto (0.25 s), se corta la voz y el diálogo, y cada paso que falta aplica su **estado final** (`CinematicStep.Skip`): los actores quedan en su marca, la quest o el ítem se entregan, el cambio de escena ocurre. Después vuelve de negro, salvo que la escena termine en negro.
    - Una cinemática salteada **cuenta como vista**: `PlayOnce` no la repite.
    - `Skippable = false` solo para unos segundos que no se pueden resumir. Por defecto todas se saltean.
    - Paso nuevo: si hace algo que tiene que quedar (dar algo, mover a alguien), sobreescribir `Skip`. Si solo es tiempo, cámara o una línea, no hace falta.
- **Pasos:**
  - `StepSay`: diálogo; avanza con botón o con `AutoAdvanceSeconds`.
  - `StepMove`: un actor camina a un nodo o a una X. **Con `Actor` vacío camina el jugador** (2026-11-05): sigue retenido, pero se mueve con su física y su animación de correr (`Player.CutsceneWalk`). `Animation`/`EndAnimation` no se le aplican.
  - `StepParallel` (2026-11-05): corre varios pasos a la vez (un NPC camina mientras el otro habla, los dos caminan juntos a la mesa). `WaitForAll = false` sigue apenas termina el primero.
  - `StepGiveItem` (2026-11-05): da un ítem y/o saca otro (`Gives`/`Takes`). Las espadas se equipan solas. Si se repite, no duplica nada.
  - `StepAnimate`: reproduce una animación y espera opcionalmente a que termine.
  - `StepFace`: gira a alguien hacia otro. **También gira al jugador** (Actor vacío).
  - `StepCall`: llama un método sin argumentos de un nodo (por ejemplo `show`/`hide` de un objeto en escena).
  - `StepCamera`: foco, peso y zoom; sin `Target` vuelve al jugador.
  - `StepWait`, `StepShake`, `StepFade`: pausa, temblor, fundido a negro o desde negro.
  - `StepBlur`: desenfoque para mareo o desmayo.
  - `StepChangeScene`: cambio de escena.
- **Ya hechas:**
  - `CinematicaApertura` en Casa1: Arthur viejo despierta a Delfrad.
  - `CinematicaDerrota` en ArenaEntrenamiento: de rodillas, desenfoque, "Algún día te ganaré".
  - `NarracionApertura`: narración en negro, al estilo del libro.
  - `CinematicaAtaque` en `Llamas_BosqueLobos2` (2026-11-02): abre en negro con Delfrad tirado, cae un meteoro a su lado, se levanta (la caída al revés) y la cámara mira hacia Nix. Unos 9 s, sin líneas. Se ve una vez por partida.
  - `CinematicaRecuerdo` en `Llamas_Casa1` (2026-11-02, rehecha 2026-11-03): la casa en llamas por dentro **no se muestra**. Abrir la puerta en `Llamas_Nix` funde a negro, `Llamas_Casa1` es solo ese negro (1.8 s de silencio) y se abre `Casa1` de mañana con Arthur. **Ahí se pasa al Nix actual (el recuerdo).** La versión anterior (Delfrad mira la casa y cae) se descartó: el usuario prefirió cortar al abrir la puerta.
  - `CinematicaEspada` en Casa1 (2026-11-05): **Arthur le da su espada a Delfrad**. Cierra el capítulo 1. Ocurre al entrar a casa con "El carro de Morlok" terminado, esa misma noche.
    - **Qué cambia:** Arthur deja de tratarlo como a un chico y lo deja ir.
    - **Puesta:** abre desde negro en plano general. Arthur está junto al fuego, de espaldas a la puerta, y la espada está sobre la mesa. "Cierra la puerta." Caminan juntos a la mesa (`StepParallel`, el jugador con `StepMove`), quedan enfrentados (Arthur a la izquierda, Delfrad a la derecha, del lado de la puerta). Plano medio sobre la mesa: el tajo del cuchillo en la de madera, "¿Te quedaste parado?" y la confesión de Arthur ("yo también... la primera vez", 1.6 s de silencio). Inserto de la espada (zoom 1.6). Entrega: la espada desaparece de la mesa, `StepGiveItem` cambia la de madera por la de Arthur. Cierre: "Las caravanas salen al alba" → 1.5 s → "Esta vez, no te quedes mirando" (paga la línea de la mañana, `CINE_CASA_4`). Arthur se da vuelta hacia la ventana y funde a negro 1.8 s.
    - Unos 55 s. Claves `CIN_ESPADA_1..13`.
- **Pasos nuevos:** `StepAnimate.Backwards` (una caída al revés se lee como levantarse), `StepMeteor` (un meteoro inofensivo con tiempo propio) y `StepActorOffset` (corre el dibujo de un actor sin mover su cuerpo, para acostarlo en una cama o bajarlo de un salto; puede esconder la capa).
- **`CinematicaApertura` en Casa1:** Delfrad arranca acostado en la cama (dibujo corrido 55, -46, sin capa) y se levanta después de la última línea de Arthur.

## 6b. Lecciones aprendidas

- **Nunca congelar la pantalla para hablar** en momentos de juego: el `DialogueBox` con pausa se siente como un cartel, no como una escena. En combate y en las transiciones de fase se usa una cinemática armada por código (`StepCode` + `StepSay` sin pausa + `HoldForCutscene`): franjas negras, cámara en el que habla, el mundo sigue vivo. Así se arma la entrada y los cambios de fase de Arthur.
- **Abrir desde negro** con un plano que muestre a los dos personajes antes de la primera línea.
- **Sin HUD en cinemáticas** (lo oculta `HoldForCutscene` y lo devuelve al terminar).
- **El ritmo lo decide la cinemática, no el botón** (decisión del autor, 2026-10-28): cada línea de `StepSay` avanza cuando termina su voz, con una pausa (`GapAfterVoice`, 0.7 s por defecto; 0.9 en la narración), y no se puede saltear. Si la línea no tiene voz, espera su tiempo de lectura (15 caracteres por segundo). Los diálogos con NPCs fuera de cinemáticas siguen avanzando con el botón.
- **Pausa antes de un cambio de tono** (`StepSay.ExtraPauses`, por línea): después de una línea fuerte, 1.5 s extra de silencio antes de que alguien cambie de tono. Ejemplo: "ya no me necesitarás" → 1.6 s → "la cena se enfría".
- **La voz en off no lleva nombre** (`SpeakerKeys` vacío en `StepSay`): se lee como el libro, no como un personaje en escena. Sirve para no confundirla con el Delfrad joven.

## 7. Lo que falta en las herramientas (sumar cuando una escena lo pida)

- Pasos de **Música** (entrar, cruzar, cortar) y **Sonido** (con ambiente).
- ~~Saltear manteniendo un botón~~, ~~pasos en paralelo~~ y ~~mover al jugador~~: hechos el 2026-11-05 (ver §6).
- **Cámara con curva y duración propias**, independientes del suavizado de la cámara del jugador.
- Un **gesto de entrega** (pasar un objeto de mano en mano). Hoy se resuelve con el objeto sobre la mesa, un inserto y `StepCall hide` (ver `CinematicaEspada`).
- **Aparecer y desaparecer actores**, y **emotes** o efectos sobre un personaje (signo de exclamación, lágrima, brillo).
- Que el **pulso de las franjas** y el **zoom** acompañen los momentos fuertes.

---

## Fuentes consultadas

- [How to build a better cutscene — Game Developer](https://www.gamedeveloper.com/art/how-to-build-a-better-cutscene): errores clásicos (cámara hiperactiva, romper la dirección de pantalla) y la pregunta "¿dónde va el corte y cómo está el ritmo?".
- [Cutscene Direction for Games — MocapOnline](https://mocaponline.com/blogs/mocap-news/cutscene-animation-direction-guide): blocking antes del pulido, ritmo de los diálogos, movimientos de cámara sutiles.
- [Creating game cinematics from storyboard to final cutscene — GamineAI](https://gamineai.com/blog/creating-game-cinematics-from-storyboard-to-final-cutscene): flujo storyboard → blocking → pulido.
- [Creating a Deeper Emotional Connection: The Cinematography of God of War (GDC 2019)](https://gdcvault.com/play/1025986/Creating-a-Deeper-Emotional-Connection): cámara al servicio de la emoción.
- [Cinematic Game Design (GDC)](https://gdcvault.com/play/1013408/Cinematic-Game): técnicas de cine para provocar emociones en juegos.
- [GDC: Ubisoft's Adam Thiery Talks Camera Theory — Game Developer](https://www.gamedeveloper.com/game-platforms/gdc-ubisoft-s-adam-thiery-talks-camera-theory): la cámara de juego es primero funcional.
- [Netflix — cómo se mide la velocidad de lectura](https://backlothelp.netflix.com/hc/en-us/articles/115001352212-How-is-reading-speed-measured-Do-punctuation-and-spaces-count) y [Reading Speed (CPS) — TitleVision](https://titlevision.dk/sm/reading_speed.htm): 15–20 caracteres por segundo.
- [Subtitling in game localisation (UAB)](https://ddd.uab.cat/pub/artpub/2013/292333/Subtitling_in_game_localisation_a_descri.pdf): los juegos suelen ignorar la velocidad de lectura, y es un error.
