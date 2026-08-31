using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Summoned by LargeSkullBoss's ground-fire attack. Kamikaze grunt: chases the player (faster than
// they can outrun on foot — see MoveSpeed on its profile), and once close enough freezes and
// flickers as a telegraph before self-detonating, same "warn before it hits" idea as BurningSkull,
// just triggered by ground proximity instead of flight. Reuses the project's Explosion.tscn VFX
// like every other exploding enemy this session. Falls to the floor via Enemy's own normal gravity
// like anything else — no special spawn handling needed.
//
// Not a persistent world enemy — it's a transient summon, so IsDefeated() is overridden to never
// consult the save system. Dynamically-instantiated enemies sharing the same node name/parent get
// the same PersistenceId (derived from their scene path) once the previous instance is gone; the
// first one to explode was marking that id defeated, so every later summon self-removed in
// _Ready() before ever appearing.
public partial class BurningDamned : Enemy
{
	[Export] public float ExplodeDistance = 40f;
	[Export] public float TelegraphDuration = 0.6f;
	[Export] public float FlickerSpeed = 24f;
	[Export] public int ExplosionDamage = 50;
	[Export] public float ExplosionRadius = 90f;

	private bool _isDetonating;
	private float _detonateTimer;

	protected override bool IsDefeated() => false;

	// The explosion itself is the real threat now — a lingering contact tick on top of it while
	// approaching would just be redundant chip damage before the actual payoff.
	protected override bool ContactDamageEnabled => false;

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_isDetonating)
		{
			_detonateTimer -= (float)delta;
			float pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_detonateTimer * -FlickerSpeed));
			Sprite.Modulate = new Color(1f, 1f, 1f, pulse);

			if (_detonateTimer <= 0f)
				Detonate();
			return;
		}

		base._PhysicsProcess(delta);

		if (GetTree().GetFirstNodeInGroup("player") is Node2D player &&
			GlobalPosition.DistanceTo(player.GlobalPosition) <= ExplodeDistance)
		{
			_isDetonating = true;
			_detonateTimer = TelegraphDuration;
		}
	}

	private void Detonate()
	{
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		if (GetTree().GetFirstNodeInGroup("player") is Node2D player &&
			GlobalPosition.DistanceTo(player.GlobalPosition) <= ExplosionRadius)
		{
			Stats targetStats = player.GetNodeOrNull<Stats>("Stats");
			targetStats?.TakeDamage(ExplosionDamage);
		}

		OnDefeated();
	}
}
