using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A kamikaze flier: hunts the player using FlyingEnemy's default hover-and-hunt (no changes there),
// then once close enough locks in place for a brief telegraph (pulsing bigger/brighter, same
// "warn before it hits" idea as every other enemy this project uses) before detonating. The
// detonation reuses the project's existing Explosion.tscn (via Enemy.OnDefeated's own
// SpawnExplosion call) instead of the sheet's own explosion frames — the sheet only supplies the
// 5-frame flying-skull loop (named "fly" to match FlyingEnemy._Ready()'s autoplay).
public partial class BurningSkull : FlyingEnemy
{
	[Export] public float ExplodeDistance = 45f;
	[Export] public float DetonateDelay = 0.4f;
	[Export] public float PulseScaleAmount = 0.15f;
	[Export] public float PulseSpeed = 18f;
	[Export] public float ExplosionRadius = 70f;

	private bool _detonating;
	private float _detonateTimer;

	protected override bool OverrideSteering(double delta)
	{
		if (_detonating)
		{
			_detonateTimer -= (float)delta;
			float pulse = 1f + PulseScaleAmount * Mathf.Sin(_detonateTimer * -PulseSpeed);
			Visual.Scale = new Vector2((FacingRight ? 1f : -1f) * pulse, pulse);

			if (_detonateTimer <= 0f)
				Detonate();

			return true;
		}

		if (Player is not null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= ExplodeDistance)
		{
			_detonating = true;
			_detonateTimer = DetonateDelay;
			return true;
		}

		return false;
	}

	private void Detonate()
	{
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		if (Player is not null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= ExplosionRadius)
		{
			Stats targetStats = Player.GetNodeOrNull<Stats>("Stats");
			targetStats?.TakeDamage(Mathf.RoundToInt(Stats.AttackPower));
		}

		OnDefeated();
	}
}
