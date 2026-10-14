using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A cactus spine flung by the CactusBedouin: a short thin dart (pale tip, green shaft) on a light
// ballistic arc that points along its flight, dies on terrain and pricks the player once. Drawn in
// code — a couple of lines — no sprite.
public partial class CactusNeedle : Node2D
{
	public Vector2 Velocity;
	public Stats ThrowerStats;
	public float DamageMultiplier = 0.35f;
	public float GravityScale = 520f;
	public float Lifetime = 1.6f;

	private float _age;
	private bool _done;
	private bool _reflected;

	public override void _Ready()
	{
		AddChild(new Line2D
		{
			Points = new[] { new Vector2(-7f, 0f), new Vector2(3f, 0f) },
			Width = 2f,
			DefaultColor = new Color(0.35f, 0.62f, 0.25f, 1f),
		});
		AddChild(new Line2D
		{
			Points = new[] { new Vector2(3f, 0f), new Vector2(7f, 0f) },
			Width = 1.5f,
			DefaultColor = new Color(0.98f, 0.95f, 0.8f, 1f),
		});
		Rotation = Velocity.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_age += dt;
		if (_age > Lifetime)
		{
			QueueFree();
			return;
		}
		Velocity.Y += GravityScale * dt;
		Vector2 next = GlobalPosition + Velocity * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 1u);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0)
		{
			Stick();
			return;
		}
		GlobalPosition = next;
		Rotation = Velocity.Angle();

		// DamageMultiplier 0 = cosmetic spine (the thorny-hide burst).
		if (DamageMultiplier <= 0f)
			return;
		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		if (_reflected)
		{
			if (ProjectileReflect.FindEnemyNear(this, 18f) is { } enemy
				&& ProjectileReflect.TryHitEnemy(enemy, damage, Velocity.Normalized(), 0f))
				QueueFree();
			return;
		}
		if (PlayerReads.Find(this) is { } player && GlobalPosition.DistanceTo(player.GlobalPosition) < 18f)
		{
			if (ProjectileReflect.TryParry(player))
			{
				// Reversing the velocity retraces the same arc back to the thrower.
				_reflected = true;
				Velocity = -Velocity;
				Rotation = Velocity.Angle();
				_age = 0f;
				Modulate = ProjectileReflect.ReflectedModulate;
				return;
			}
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
				stats.TakeDamage(damage, isProjectile: true);
			QueueFree();
		}
	}

	// Lodged in the ground/wall for a moment, then gone.
	private void Stick()
	{
		_done = true;
		Tween tween = CreateTween();
		tween.TweenInterval(0.6f);
		tween.TweenProperty(this, "modulate:a", 0f, 0.3f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
