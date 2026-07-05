# Metroidvania — Project Architecture Notes

## Autoload policy (decided 2026-07-05)

Priority: keep the project simple and avoid unnecessary technical debt. Don't add abstraction layers until there's a real need for them.

Before adding a new entry to `[autoload]` in `project.godot`, answer these three questions:

1. Does it need to survive a scene change?
2. Do 3+ unrelated systems need to call it (not just its parent/children in the scene tree)?
3. Is there exactly one instance of it in the whole running game?

**If any answer is "no"** → it's not an autoload. Make it a regular node owned by whatever scene actually needs it, referenced via `GetNode`/signals/`[Export]` like any other component.

**If all three are "yes"** → don't create a new autoload by default. Check whether it fits an existing hub first:

- **Persistent global data/logic** (economy, achievements, settings, fast-travel, etc.) → add it as a property/sub-class composed by `SaveManager` or `GameConfig`, not as a new manager autoload.
- **Global UI overlay/screen** → don't create a new autoload per screen. A shared `UIManager` entry point for overlays is deliberately **not** introduced yet — only create it the day a new UI system genuinely needs one common entry point across overlays, not preemptively.
- Only create a brand-new autoload when the system genuinely doesn't fit any existing hub.

**Current autoloads are frozen as-is under this policy** — it governs *new* systems only, it is not a mandate to refactor what already exists: `SaveManager`, `QuestManager`, `ItemDatabase`, `LocaleManager`, `GameConfig`, `PauseMenu`, `ConfirmPrompt`, `DialogueBox`, `QuestLog`, `InventoryUI`, `DeathScreen`, `RestScreen`, `ZoneTitle`.

**If a future system seems to violate this policy, flag it before implementing and propose the simplest alternative** instead of just adding the autoload.
