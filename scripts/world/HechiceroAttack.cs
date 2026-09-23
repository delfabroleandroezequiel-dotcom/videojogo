using Godot;

namespace Metroidvania.World;

// One tunable attack definition for Hechicero — a plain data Resource so each attack shows up as
// an editable block in the Inspector instead of being hardcoded per-attack fields on the enemy
// itself. Hechicero picks one of these to drive its (inherited) RangedEnemy shoot loop.
[GlobalClass]
public partial class HechiceroAttack : Resource, ISpellDefinition
{
	[Export] public bool Enabled = true;
	[Export] public string AttackName = "Bolt";
	[Export] public PackedScene ProjectileScene;
	[Export] public float ProjectileSpeed = 250f;
	[Export] public float Cooldown = 2f;
	[Export] public float Range = 350f;

	// Total time the "shoot" animation plays for, and how far into it the projectile actually
	// releases — same meaning as RangedEnemy.ShootAnimDuration/ShootReleaseDelay, just per-attack.
	[Export] public float CastDuration = 0.92f;
	[Export] public float ReleaseDelay = 0.67f;

	// How many projectiles a single cast fires (e.g. a 3-shot homing volley) and the delay
	// between each one in that burst. 1/0 means a plain single shot, same as before this existed.
	[Export] public int ProjectileCount = 1;
	[Export] public float BurstInterval = 0.15f;

	bool ISpellDefinition.Enabled => Enabled;
	string ISpellDefinition.AttackName => AttackName;
	PackedScene ISpellDefinition.ProjectileScene => ProjectileScene;
	float ISpellDefinition.ProjectileSpeed => ProjectileSpeed;
	float ISpellDefinition.Cooldown => Cooldown;
	float ISpellDefinition.Range => Range;
	float ISpellDefinition.CastDuration => CastDuration;
	float ISpellDefinition.ReleaseDelay => ReleaseDelay;
	int ISpellDefinition.ProjectileCount => ProjectileCount;
	float ISpellDefinition.BurstInterval => BurstInterval;
}