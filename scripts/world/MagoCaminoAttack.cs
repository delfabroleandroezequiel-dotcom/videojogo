using Godot;

namespace Metroidvania.World;

// Own attack-definition Resource for MagoCamino, deliberately not shared with HechiceroAttack —
// this enemy is meant to diverge from Hechicero later, so its tuning data stays independent from
// day one instead of the two casters being coupled through a shared Resource type.
[GlobalClass]
public partial class MagoCaminoAttack : Resource
{
	[Export] public bool Enabled = true;
	[Export] public string AttackName = "Bolt";
	[Export] public PackedScene ProjectileScene;
	[Export] public float ProjectileSpeed = 250f;
	[Export] public float Cooldown = 2f;
	[Export] public float Range = 350f;

	// Total time the "attack1" animation plays for, and how far into it the projectile actually
	// releases.
	[Export] public float CastDuration = 0.92f;
	[Export] public float ReleaseDelay = 0.67f;

	// How many projectiles a single cast fires (e.g. a 3-shot homing volley) and the delay
	// between each one in that burst. 1/0 means a plain single shot.
	[Export] public int ProjectileCount = 1;
	[Export] public float BurstInterval = 0.15f;
}
