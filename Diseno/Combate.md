# Combate — guía del diseñador de combate y jefes

Documento vivo. **Leerlo antes de diseñar o tocar un enemigo, un jefe, un ataque del jugador o una habilidad de combate**, y sumarle lo que se decida. Última actualización: 2026-10-28.

Lo técnico de la IA (piezas compartidas, lecturas del jugador, patrón de código de un ataque, cómo medir y probar) vive en **`Diseno/IAEnemigos.md`**. Este documento es el criterio de diseño.

---

## 1. El perfil: diseñador de combate de action RPG 2D

Cuando toque combate, trabajo como **diseñador de combate y jefes de juegos de acción 2D con raíz souls**: alguien que piensa en lo que el jugador lee, en la ventana justa para responder y en que cada pelea se sienta distinta.

**Sus convicciones:**
- **Identidad antes que cantidad.** El kit del jugador está cerrado en cuatro pilares; lo nuevo alimenta uno de ellos o no entra.
- **Cada enemigo y cada jefe es único.** Su propio `.cs`, sus propios ataques y su propia lógica. Nada de un template con números distintos.
- **Profundidad antes que volumen.** Pocos enemigos con kits ricos valen más que muchos básicos.
- **Todo ataque avisa y es justo.** Duro sí, injusto no: siempre hay una lectura y una respuesta posible.
- **Ágil pero comprometido.** El personaje no se vuelve lento; lo que pesa es que cada acción comprometa (ventana de parry, castigo por fallar, ejecuciones).
- **Se diseña con el autor, paso a paso.** Propongo opciones con una recomendación y construyo solo lo decidido.

---

## 2. El kit del jugador: cuatro pilares (cerrado el 2026-10-13)

1. **Combate normal:** combo de espada, ataques arriba y abajo, rodar (barato, en el piso, te pasa a la espalda del enemigo) y dash (más caro, más largo, sirve en el aire), los dos con invulnerabilidad.
2. **Parry:** la única defensa además de esquivar. **No hay guardia sostenida.** Ventana de 0.2 s; si no atrapa nada, el botón se traba 0.35 s. Refleja proyectiles, 2 parries seguidos aturden y un enemigo aturdido se puede ejecutar. Los que se pueden apuñalar, también por la espalda.
3. **Sombra:** el kit de clones neón (básico, señuelo, eco, manipulable). Los hechizos futuros salen de los clones.
4. **Invocaciones:** el Bastón revive enemigos muertos como sombras. **Apagado el 2026-11-04** por pedido del autor: los enemigos ya no sueltan esencia ni dan "usos", y no hay teclas ni HUD de invocación (`Player.BastonSummonsEnabled` y `Enemy.DropsShadowEssence` en `false`; el código queda guardado).

- **Estacionados** (código guardado, apagados): tornado, escupitajo venenoso, bola de fuego, escudo de viento. Vuelven como **consumibles**, no como habilidades.
- **Espada voladora:** se queda, pero le falta una vuelta que la ate a los pilares.
- **Arco: eliminado** (2026-10-28). No va con este protagonista; la G y R3 son solo la espada voladora (`throw_sword`).
- **Sin definir:** ataque cargado y qué habilidades de exploración pasan a ser desbloqueos.
- **Si una propuesta no alimenta un pilar, se discute antes**, sobre todo si es otra herramienta de daño a distancia.

---

## 3. Reglas de enemigos

- **Un `.cs` por enemigo**, subclase de `Enemy` / `MeleeEnemy` / `FlyingEnemy`. Si hace falta una costura nueva en la base, se agrega un `protected virtual` con el comportamiento original por defecto. Nunca se dobla el comportamiento compartido en el lugar ni se suma otro `[Export]` booleano por enemigo.
- **Cada cambio en un script compartido** se pregunta: "¿esto cambia a los otros enemigos o jefes?". Ya pasó dos veces con `Boss.cs` (el caballero perdió su idle y su esquive).
- **Nivel de referencia:** Bandido7, los elfos (ElfArcher, ElfAssassin, ElfSwordsman) y RangerArcher. El lote del 2026-10-10 quedó "bien básico": hay que subir cada uno a ese nivel.
- **IA inteligente = leer al jugador, no hacer trampa.** Castigar el reflejo (rodar o hacer parry a lo loco), premiar leer el aviso.
- **Postura:** los golpes rompen la postura y eso abre la ejecución. Solo `MeleeEnemy` se estremece; el resto tiene hiperarmadura hasta que cada uno tenga su reacción propia. Los jefes no se ejecutan.
- **Enemigos que reaccionaban a la guardia sostenida** (Bandido1/6/7, elfos, GhostWarrior2): con el parry de 0.2 s esa lógica casi no se activa. Conviene que reaccionen a un **parry fallado**.

### Guerreros de Enterbill y Radem (2026-11-05)

Catorce soldados de los dos reinos del libro, hechos con PixelLab (estilo del prota, lienzo de 96x80, escala 1.12-1.2). Cada uno tiene su `.cs` y su kit; comparten solo la plumbing de `GuerreroReino` (ver `IAEnemigos.md`). Escenas en `scenes/world/Enemigos/Guerrero*.tscn` y corredor de prueba en `scenes/world/Mapas/PruebaGuerreros.tscn`.

- **Enterbill (azul): disciplina de castillo.** Escudos, distancia, ritmo medido. Lee el parry: no golpea contra un parry abierto (sostiene el amague) y castiga el parry fallado.
  - **Escudero:** escudo torre. De frente no se mueve ni se estremece y resta daño. Contra un parry abierto empuja con el escudo; contra un parry fallado tira el tajo al instante.
  - **Lancero:** pelea a la distancia de la lanza. Estocada larga con paso adelante; si te metés adentro, barrido bajo que hay que saltar.
  - **Ballestero:** un virote pesado con puntería anticipada y una recarga larga, que es la ventana para castigarlo. Si lo encimás, salta hacia atrás.
  - **Caballero:** combo de tres tiempos (hachazo, pausa, corte y estocada). Si rodaste o dasheaste, en la pausa espera a que termine tu esquive y recién ahí golpea.
  - **Alabardero:** barrido amplio de cerca. De lejos levanta la alabarda y la clava en el piso, y una onda baja corre hacia adelante.
  - **Capitán:** **Orden** al verte ("Hold the line!"): alerta a los enemigos cercanos y les baja el cooldown ×0.65 durante 8 s, con un anillo azul sobre cada uno. Duelista: estocada y corte en diagonal, con finta contra el parry.
  - **Maza:** hiperarmadura. Mazazo al piso con una onda a los dos lados, y puñetazo de broquel rápido si fallás un golpe pegado a él.
- **Radem (rojo): presión del desierto.** Armas curvas, carreras, saltos y castigo a la salida de tu esquive.
  - **Cimitarras:** entra en dash con una ráfaga de cuatro cortes; si le parreás el segundo, corta la ráfaga. Arranca justo cuando termina tu dash y después salta hacia atrás.
  - **Jabalinero:** jabalina en arco hacia donde vas a estar (`ProyectilArqueado`, se clava donde falla). De cerca, dos pinchazos rápidos.
  - **Hachero:** hiperarmadura. Carga en línea recta de lejos, giro con el hacha que pega adelante y atrás, y hachazo al piso.
  - **Khopesh:** escudo redondo. El gancho, si te agarra, te tira hacia él y encadena un corte. Si golpeás su escudo de frente, contraataca enseguida (Radem no espera).
  - **Arquero:** flecha rápida y plana. Si te quedás quieto o te cubrís, tira una flecha incendiaria en arco que deja fuego en el piso.
  - **Mangual:** molinete sobre la cabeza mientras avanza (castiga saltarle encima) y después revienta la bola contra el piso.
  - **Campeón:** dos estocadas de tridente (si le parreás la primera, no sigue). De lejos salta adonde vas a estar y clava el tridente, con una onda baja.
- **Voces:** `Voice/EnterbillA` (sargento curtido, "Admiral Action"), `EnterbillB` (caballero joven, "Kaisserkrieger"), `RademA` ("Villain", áspero) y `RademB` ("Leónidas", con acento). Cada una tiene Golpeado 1-3, Muerte 1-2, **Alerta 1-2** (al verte, una sola voz por vez en pantalla) y **Ataque 1** (35 % de los ataques); EnterbillA suma **Orden 1-2**, que usa el Capitán. Todas en inglés, como las demás voces del juego.

---

## 4. Reglas de jefes

- **Un jefe, una clase.** No generalizar las mecánicas de un jefe dentro de `Boss.cs`.
- **Ataques adaptados de Elden Ring** (o souls en general): movimiento reconocible → versión 2D con aviso, estela neón y una ventana de esquive justa. Es una receta validada (el jefe hongo inspirado en Malenia).
  - **Verificar los ataques contra eldenring.wiki.gg antes de proponerlos.** Ya mezclé la lluvia de flechas del Radahn base con el Radahn consorte.
- **Afinar contra el ciclo de dash del jugador (0.78 s).** Una ráfaga cada 1.0 s es exigente pero justa; más corta se vuelve imposible de esquivar.
- **Fases con identidad:** cada fase cambia el repertorio, no solo los números (el hongo: fase 2 solo danza y flor, con tinte de furia).
- **Barra de vida del jefe:** cada jefe elige su estilo (`BossBarStyle`) y su nombre (`BossNameKey`). La barra baja sin estela ni destello.
- **Se diseña de a un paso:** una o dos preguntas por vez, en texto (nada de formularios con muchas preguntas).

---

## 5. Lenguaje visual del combate

- **Estilo neón:** estelas aditivas (brillo + núcleo + chispas) con `EnemySlashTrail`. Rojo para bandidos, verde para elfos, rampa de fuego para la antorcha. Al autor le encantan: "les da hasta calidad".
- **Aviso antes de pegar:** destello blanco o verde (tinte del sprite + brillo aditivo), nunca tocando `Visual.Modulate`.
- **Idea a decidir:** aviso rojo = ataque que no se puede parar, solo esquivar (regla de Sekiro).
- **La estela va por delante del arma:** su borde lejano coincide con el borde lejano del hitbox, no con la punta de la hoja. Así se ve el alcance real.
- **Efectos de ataques enemigos procedurales**, hechos en código: `Line2D`, esquirlas `Polygon2D`, partículas con `SoftDotGradient`, destellos `PointLight2D`, siluetas neón y temblor de cámara. **Nada de hojas de VFX de packs** para ataques nuevos.
- **En mapas oscuros**, el neón necesita su `PointLight2D`, porque el `CanvasModulate` apaga las líneas aditivas.
- **Momentos grandes** (ejecución, golpe final): componer con HitStop, FlashScreen, ShakeCamera, HitImpactBurst, HitSparks y cámara lenta, por encima del nivel de un golpe normal. Receta validada de la ejecución: acercamiento de cámara, impacto, sangre, chispas pesadas, destello, temblor (8, 0.35), HitStop 0.26 s y cámara lenta 0.25× por 0.45 s si mata.
- **Animaciones que faltan:** reusar una animación existente y rotarla (validado jugando con el golpe en picada y el ataque hacia arriba) antes de buscar arte nuevo.
- **Sonido:** siempre sonidos reales de los packs, nunca generados por script.
- **Voces de enemigos (2026-11-05):** quejidos sin palabras, no frases en cada golpe. `Enemy.VoiceCategory` (carpeta en `Sonidos/`) reproduce un "Golpeado N" al azar al recibir un golpe (60 % de las veces, con al menos 0.7 s entre uno y otro, y nunca en el golpe que lo mata) y un "Muerte N" al morir. Vacío = mudo. **Siempre breves y secos** ("¡Guh!", "¡Ack!"): el autor rechazó los quejidos largos con respiración porque "parece medio porno". Por eso el código corta el golpe a 0.6 s y la muerte a 0.9 s, con un fundido rápido (`Sfx.PlayIfExistsAt(..., maxSeconds)`). Los bandidos usan `Voice/BandidoA` (Bandido del desierto: 1, 3, 5, 7, Corpulento, Hacha) y `Voice/BandidoB` (Bandido nervioso: 2, 4, 6, Arquero, Dagas), generadas con ElevenLabs. Frases sueltas ("¡Ahí está!") quedan para momentos puntuales, todavía no hechas.
- **Sigilo (2026-11-05):** agacharse es sigilo (animación `sigilo` de PixelLab: camina agachado y, quieto, se congela en la pose baja). Un enemigo que **te da la espalda y todavía no te vio** no te detecta ni se da vuelta mientras estés agachado detrás de él. Lo cancelan pegarle, que lo alerten (`Alert`) o que ya te haya visto: una vez que te ve, te sigue aunque te agaches, hasta que te alejes a 1.5× su rango de detección. Los jefes siempre te ven. Combina con la ejecución por la espalda. **Los voladores (`FlyingEnemy`, Ghost, Vulture, ArchDemon) no respetan el sigilo, y es a propósito** (decisión del autor, 2026-11-05): desde el aire te ven igual.
- **Enterbill contra Radem (2026-11-06):** los soldados de los dos reinos (`GuerreroReino`) se pelean entre sí. Cada uno va por el rival vivo más cercano si está más cerca que el jugador y dentro de su rango (`Enemy.CurrentTarget`, virtual). Su espada daña al jugador y a los del otro reino, nunca a los propios ni a otros enemigos (`Hitbox.TargetFilter`). Las lecturas del jugador (parry, bloqueo, rodar) y los proyectiles de ballesteros y arqueros siguen siendo solo contra el jugador. Probado: un lancero contra un cimitarras se matan solos. **`AliadoDelJugador`** (casilla por instancia): ese soldado no te persigue ni te daña, solo pelea con el reino rival y se queda quieto si no hay ninguno. Tus golpes todavía lo pueden lastimar (sin fuego amigo resuelto).
- **Orientación inicial:** todos los enemigos tienen `OrientacionInicial` (PorDefecto / Izquierda / Derecha) para decidir hacia dónde miran al cargar el mapa, y así armar situaciones de sigilo.
- **Voz del protagonista:** desde el 2026-11-05 el prota usa siempre los sonidos del Delfrad joven (`Voice/DelfradJoven`).

---

## 6. Progresión de combate

- **Ecos** (de enemigos; jefes y secretos dan más) → se gastan en **vida, stamina o daño** a elección, en los puntos de descanso. Se pierden al morir y se recuperan llegando adonde moriste, al estilo souls.
- **Árbol de Raíces** (no construido): 4 raíces = los 4 pilares. Moneda: **Fragmentos de Raíz**, escasos y nunca se pierden.
- **Curación:** frascos tipo Estus. El autor rechazó una economía de curación.
- **Resinas:** consumibles de 60 s para imbuir el arma, desde la barra rápida de 4 casillas.
- **Pendiente que le gustó:** el parry como motor del resto. Una barra de Sombra que se llena con parries y ejecuciones y paga los clones, más mejoras de parry en el árbol (curar al hacer parry, Eco, Quebrar, Cosecha, Espejo).

---

## 7. Backlog de ideas (sin elegir)

- ⭐ **Sombra residual:** esquivar a través de un ataque deja una sombra que pega una vez.
- ⭐ **Ataques que no se pueden parar**, con aviso rojo.
- ⭐ **Jefe "La Sombra de Delfrad":** usa los clones del jugador en su contra (revisar que encaje con el libro).
- Marca de sombra (el clon marca y el golpe del jugador detona), enemigos que ignoran el señuelo, portadores de luz que disuelven clones, puertas custodiadas con varias soluciones.
- **Construidos, esperando prueba:** Clon embestida + Meteoros de Pegaso, Clon centinela, Sō Ryū Sen en el ataque cargado.

---

## 8. Lo que el autor rechazó (no repetir)

- Mecánicas de un jefe convertidas en el estándar de todos.
- Lotes de enemigos básicos para sumar cantidad.
- Explosiones o golpes de packs de VFX en enemigos ("hacé tus propios efectos").
- La estela calcada encima del arma.
- Estela y destello en la barra del jefe ("muy grotesca").
- Sonidos sintéticos generados por script ("hiper cutre").
- La guardia sostenida y la economía de curación.
- Teletransporte o intercambio con la Sombra para moverse por el mapa.

---

## 9. Registro de decisiones

| Fecha | Decisión | Origen |
|---|---|---|
| 2026-11-04 | Fuera los "usos" por matar enemigos y las invocaciones de enemigos del Bastón (apagado, no borrado). Nuevo `Enemy.ExtraLoot` para botín propio de una instancia (el bandido del hacha suelta el libro de Morlok) | Pedido del autor |
| 2026-07-07 | Cada jefe único; no generalizar en `Boss.cs` | Jefe lobo |
| 2026-07-10 | El kit de combate se siente sólido; reusar y rotar animaciones | Prueba del autor |
| 2026-08-02 | Cada enemigo con su propio `.cs`, también los comunes | OrcEnemy |
| 2026-09-23 | Estilo neón para estelas y avisos | Arañas y bandidos |
| 2026-09-28 | Ataques adaptados de Elden Ring, diseñados paso a paso | Jefe hongo (Malenia) |
| 2026-10-09 | Efectos de enemigos procedurales; estela por delante del arma | Bandido3, Bandido5 |
| 2026-10-10 | Profundidad antes que volumen | Lote de 21 enemigos |
| 2026-10-13 | Cuatro pilares; parry sin guardia sostenida; hechizos elementales como consumibles | Cierre del kit |
| 2026-10-13 | Ecos para stats, Árbol de Raíces para habilidades | Progresión |
| 2026-10-18 | Postura, estremecimiento, hitstop y retroceso | Investigación souls |
| 2026-10-28 | Se elimina el arco del jugador | Pedido del autor |
| 2026-10-28 | Joystick: A saltar, X atacar, B rodar, Y curar, LB parry, RB dash, LT clon, RT cargado, L3 golpe en carrera, R3 espada voladora, cruceta ↑ interactuar / ↓ barra rápida / ← invocar / → cambiar clon, Back inventario, Start pausa | Reasignación del joystick |
| 2026-10-30 | La cura se aplica al terminar la animación; un golpe real la cancela y la carga se pierde | Pedido del autor |
| 2026-11-02 | Hitboxes de los melee de Cueva 1–11 medidos contra el dibujo: pegan en el cuadro del golpe y su borde lejano queda a 0–14 px de la punta del arma; las estelas se ajustaron a ese alcance salvo las de Bandido1, calcadas del arte | Pedido del autor |
| 2026-11-05 | 14 guerreros de Enterbill (azul, disciplina, leen el parry) y Radem (rojo, presión, castigan el fin del esquive), con PixelLab y voces ElevenLabs con alerta y gritos de ataque. Corredor `PruebaGuerreros` | Pedido del autor |
| 2026-11-02 | Arañas comunes con el Monster (2) de Fantasy Monsters Pack: violeta la normal, carmesí la oscura; mueren con animación, sin explosión. Jefes araña con el mismo sprite a escala 6.5: `SpiderBoss` violeta, `SpiderBossArena` carmesí; explotan al morir | Pedido del autor |
| 2026-11-05 | **BandidoCuerda** (La Mina): cuchillero colgado de una cuerda que se balancea (`RopePath`); tira un cuchillo apuntado cada 2.2 s, se suelta sobre el jugador cuando pasa abajo o con 2 golpes, y en el piso es un Bandido6Cuchillos. Arte PixelLab (pose colgada + balanceo + lanzamiento). Trampas de mina nuevas: vagoneta suelta, grisú, derrumbe de techo, viga baja; la vagoneta montable con emboscadas | Pedido del autor (Mina) |
