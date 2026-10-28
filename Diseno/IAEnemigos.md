# IA de enemigos — guía de referencia

Documento vivo con todo lo que hace falta para diseñar o tocar la IA de un enemigo. Última actualización: 2026-10-27.

---

## 1. Principios (decididos con el usuario)

- **Cada enemigo con su propio kit y su propia lógica.** Cada uno es una subclase de `Enemy` con su `.cs` propio. Nada de un "template" compartido con parámetros distintos, y no generalizar las mecánicas de un jefe dentro de `Boss.cs`.
- **Profundidad antes que cantidad:** pocos enemigos, pero con kits ricos (al nivel de Bandido7 o de los elfos).
- **IA "inteligente" = leer al jugador, no hacer trampa.** Se castiga reaccionar por reflejo (rodar o hacer parry a lo loco) y se premia leer el aviso del ataque.
- **Todo ataque avisa:** destello blanco o verde antes de pegar, y estelas neón (`EnemySlashTrail`). Los efectos del enemigo son procedurales (`GroundSlamFx`, chispas, luces), no sprites de packs de VFX.
- **El daño entra en el cuadro dibujado**, nunca en el cuadro 0 ni durante el amague. Se mide el dibujo (ver §5).

---

## 2. Piezas compartidas

| Pieza | Archivo | Para qué |
|---|---|---|
| `Enemy` | `scripts/world/Enemy.cs` | Base: detección, persecución, giro, contacto, poise/flinch, parry→stun, ejecuciones, muerte. |
| `Enemy.ComputeMoveX` | ídem | **Gancho de movimiento**: devolver la velocidad X deseada (espaciado, kiting, acercarse). |
| `CanTurnToFacePlayer`, `CanChase`, `ContactDamageEnabled`, `UpdateAnimation` | ídem | Overrides habituales (no girar mientras ataca, sin daño por contacto, etc.). |
| `PlayerReads` | `scripts/world/PlayerReads.cs` | Lecturas del jugador: `IsBlockingToward` (parry abierto hacia mí), `WhiffedParryToward` (falló el parry: abertura), `IsClosingIn`, `PredictAimDirection` (apuntar adonde va a estar). |
| `DashWatcher` | ídem | Cuánto pasó desde que terminó su dash (`SinceDashEnded`): soltar el ataque justo al salir. |
| `WhiffWatcher` | ídem | `PlayerStartedSwing` (arrancó un golpe: esquivar) y `JustWhiffed` (falló un golpe: castigar). Avisarle `NotifyHitTaken` en cada golpe recibido. |
| Lecturas del `Player` | `scripts/player/Player.cs` | `IsAttacking`, `IsBlocking`, `IsParryRecovering`, `IsDashing`, `IsRolling`, `IsHealing`, `IsFacingRight`, `Velocity`. |
| `EnemyCombatCoordinator` | `scripts/world/EnemyCombatCoordinator.cs` | Cupo de atacantes simultáneos: `TryAcquireAttackSlot` / `ReleaseAttackSlot` (si no hay cupo, esperar). |
| Parry → stun | `Enemy.RegisterParried` | Con `ParriesToStun` parries (un parry perfecto cuenta 2) se aturde `StunDuration`, y se lo puede ejecutar. |
| Poise | `Enemy.TakePoiseDamage` | Postura que se rompe con golpes; el flinch solo aplica a `MeleeEnemy`, el resto tiene hiperarmadura. |
| `GroundSlamFx` | `scripts/world/GroundSlamFx.cs` | `Impact`, `Chip`, `EarthSpikes`, `WarningCrack`, `BeginFissure`, con paleta (`BandidoCorpulento.Palette` = roca). |
| Estela neón tipo dash | `Metroidvania.Player.NeonSilhouette` | Silueta aditiva de un cuadro (afterimages), igual que el dash del protagonista. |
| Velo berserk | `Metroidvania.Player.BerserkerVeil` | Pantalla roja que late; se agrega a la escena y `Release()` lo quita. |

---

## 3. Recetas probadas

### ElfArcher — el banco de pruebas de la IA "inteligente" (`ElfArcher.cs`)
- **Espaciado** con banda preferida e histéresis: se aleja cuando lo encimás, se acerca si estás lejos, nunca retrocede hacia un pozo o una pared. Si lo encimás salta hacia atrás, o por encima tuyo si está acorralado, y solo si el arco simulado del salto cae en suelo.
- **Apunta adonde vas a estar** (`PredictAimDirection`, con la anticipación vertical amortiguada).
- **Sostiene el ataque:** mantiene el arco tenso mientras dasheás o hacés parry hacia él, y suelta apenas termina.
- **Elige el ataque por puntaje** según la situación (si saltás: abanico; si está cerca o acorralado: ráfaga), y sube la variedad cuando fallan sus flechas.
- **Si le pegan, se desengancha:** recibir un golpe cancela el tiro y lo hace querer alejarse.

### Arthur / Guerrero Rojo (`RedWarrior.cs` + `ArthurEntrenamiento.cs`)
- **RedWarrior (base):**
  - Tajo bajo (cuadros 5-8) y, seguido de eso, un martillazo al piso (15-18, explosión de roca).
  - Torbellino.
  - Esquive deslizándose (hoja `dash`, invulnerable, estela neón) cuando arrancás un golpe cerca.
  - Castigo si fallás un golpe.
  - Frenesí por debajo de cierta vida.
- **Ganchos para variantes:**
  - `MoveAllowed(Move)`: qué movimientos puede usar.
  - `TelegraphScale`, `CooldownScale`, `DamageScale`, `SpeedBonus`: ajustes de ritmo y daño.
  - `OverheadFinisher`: tercer golpe por arriba (cuadros 23-25).
  - `Decide(...)`: cerebro propio.
  - `HoldTelegraph()` + `MaxTelegraphHold`: sostener el amague.
- **Arthur (cerebro estilo ElfArcher):**
  - Nunca pega contra un parry abierto: sostiene el amague o espera, y castiga el parry fallado.
  - Castiga si te curás o al final de tu ruedo o dash (`PostEvadeWindow`).
  - No ataca dentro de tus i-frames.
  - Si parreás mucho (`ParryHappyBlocks`), amaga con retrasos (feints) y usa más el torbellino.
  - Dos golpes recibidos seguidos lo hacen alejarse con un esquive.
  - A distancia te salta encima (salto apuntado con anticipación) y, en berserk, usa el leap.
  - En rango elige por puntaje entre tajo y torbellino.
- **Fases de Arthur:** Intro → Calentamiento (pasivo) → En serio (rugido, IA completa) → Berserk (velo rojo, temblor enorme, salto con martillazo, leap con lanzamiento de maza y picada, tercer golpe por arriba).

### Otros con IA propia, para consultar
- **Bandido5:** salto y explosiones con `GroundSlamFx`.
- **BandidoCorpulento:** fisuras, rocas y erupciones.
- **Bandido6 y Bandido7:** afterimages.
- **DeathKnight.**
- **Hongo jefe** (estilo Malenia, robo de vida).
- **Midir:** caótico, con láser.

---

## 4. Patrón de código de un ataque

```csharp
private async Task Ataque()
{
    if (!Begin()) return;              // cupo de ataque + _busy + mirar al jugador
    try
    {
        Sprite.Play("anim"); Sprite.Pause();
        // amague: cuadros de preparación + FlashBody(t)
        // cuadros activos: chequear daño SOLO en los cuadros dibujados (StrikeFrames / Strike)
        // recuperación
    }
    finally { Finish(); }              // libera el cupo, quita el destello, _busy = false
    AttackCooldownTimer = ...;
}
```
- `Wait()` devuelve `false` si el enemigo quedó **aturdido o en ejecución**: hay que cortar el ataque. Los timers del árbol siguen corriendo durante el congelamiento; si no se corta, el golpe parreado sigue y te pega igual.
- **Root motion** (cuando la hoja dibuja al personaje avanzando dentro de la celda): `BeginRootMotion(drift, animOffset, escala)` esconde el avance dibujado con `Sprite.Offset` y mueve el cuerpo lo mismo. `EndRootMotion()` vuelve a idle **antes** de sacar el offset, porque si no el cuerpo aparece en otro lado por un cuadro.

---

## 5. Medir antes de programar (y trampas conocidas)

- **Medir el dibujo**, no adivinar: un script en PowerShell que saque el bbox de alfa por cuadro dice en qué cuadro llega la hoja y hasta dónde llega. Con eso se arma la hitbox por cuadro.
- **Márgenes ocultos en los SpriteFrames:** algunos packs traen `margin = Rect2(...)` en sus AtlasTexture, y eso corre el dibujo respecto del cuerpo. Al Guerrero Rojo y a Arthur ya se les sacó. Paladin, Necromancer y ShieldKnight todavía los tienen y no están revisados.
  - **El `get_image()` en headless ignora el margen.** La posición del sprite se verifica con un **render real** (Godot con ventana y `root.get_texture().get_image().save_png(...)`), no solo en headless.
- **Celdas de distinto tamaño** dentro del mismo pack (170x96, 220x96, 260x128): calcular un `animOffset` por animación para que la figura del cuadro 0 quede donde está la de idle.
- **Sprite propio para variantes:** copiar el `.tres` y las hojas a una carpeta propia (por ejemplo `assets/external/Chars/Arthur/`) antes de recolorear, así el original no cambia.

---

## 6. Cómo probar

- **Compilar sin pisar el build del editor:** `dotnet build Metroidvania.csproj -o <scratch>/build -p:BaseIntermediateOutputPath=<scratch>/obj/ --no-incremental`.
  - Para correr Godot headless con el código nuevo hace falta `dotnet build` normal (actualiza `.godot/mono/temp/bin`).
- **Simulación headless:** un script `extends SceneTree` que instancia la escena, pone `current_scene` (si no, los SFX fallan), avanza los diálogos con `InputEventAction` mientras `paused` esté activo, y cuenta animaciones, cuadros donde entra el daño y excepciones.
  - Los métodos de C# llamados desde GDScript necesitan **todos** los argumentos, también los opcionales.
- **Ojo:** un `SetCurrentHealth` dentro de un `HealthChanged` sin guarda hace recursión infinita. Si el jugador muere, la pantalla de muerte recarga la escena y el test se cuelga (usar `GodMode`).
- **Captura real:** Godot con ventana fuera de pantalla (`--position 4000,4000`). La imagen sale en lineal por el HDR 2D, así que se ve más oscura que en el juego.
