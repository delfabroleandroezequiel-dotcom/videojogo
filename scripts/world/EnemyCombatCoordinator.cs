namespace Metroidvania.World;

// Lightweight shared "how many enemies are currently mid-attack against the player" tally. Not a
// Node/autoload — just a static counter, so it doesn't fall under the project's autoload policy
// (no scene tree presence, no lifecycle). Enemies that fail to acquire a slot back off and retry
// shortly instead of attacking anyway, which is what actually reads as "enemies taking turns"
// instead of a pile of mobs swinging in sync the instant they're each off cooldown.
public static class EnemyCombatCoordinator
{
	public const int MaxConcurrentAttackers = 2;

	private static int _activeAttackers;

	public static bool TryAcquireAttackSlot()
	{
		if (_activeAttackers >= MaxConcurrentAttackers)
			return false;

		_activeAttackers++;
		return true;
	}

	public static void ReleaseAttackSlot()
	{
		if (_activeAttackers > 0)
			_activeAttackers--;
	}

	// The counter is static, so it survives a scene reload/change even though every enemy that
	// was holding a slot just got destroyed with the old scene. LevelBootstrap calls this once per
	// level load to guarantee a clean slate regardless of how the previous scene ended — a belt-
	// and-suspenders backstop alongside Enemy.OnDefeated()'s own immediate release, which is what
	// actually covers an enemy killed mid-attack while the rest of that same scene is still live.
	public static void Reset() => _activeAttackers = 0;
}
