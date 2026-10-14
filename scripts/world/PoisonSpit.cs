using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A lobbed poison glob: arcs under gravity like a real spit rather than flying straight (compare
// Projectile.cs's straight-line shot). On hitting the player it applies a poison damage-over-time
// tick using the same ApplyPoison contract PoisonPool already relies on, and on any impact it
// leaves a PoisonPuddle behind instead of just vanishing.
public partial class PoisonSpit : Area2D
{
	[Export] public float ArcGravity = 620f;
	[Export] public float Lifetime = 4f;
	[Export] public int ContactDamage = 12;
	[Export] public int PoisonTickDamage = 4;
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDuration = 3f;
	[Export] public PackedScene PuddleScene;
	// Passed on to the puddle it leaves: who it poisons and which physics layers it watches (0 = keep
	// the puddle scene's own mask). The player's spit sets "enemy" + the enemy layer.
	[Export] public string PuddleTargetGroup = "player";
	[Export(PropertyHint.Layers2DPhysics)] public uint PuddleCollisionMask;

	private Vector2 _velocity;
	private Stats _shooterStats;
	private bool _splattered;
	private bool _reflected;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += () => Splatter(GlobalPosition);
	}

	public void Launch(Vector2 velocity, Stats shooterStats)
	{
		_velocity = velocity;
		_shooterStats = shooterStats;
		Rotation = velocity.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_velocity.Y += ArcGravity * dt;
		Position += _velocity * dt;
		Rotation = _velocity.Angle();
	}

	private void OnBodyEntered(Node2D body)
	{
		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (!_reflected && targetStats is not null && targetStats != _shooterStats && ProjectileReflect.TryParry(body))
		{
			// Parried: reversing the velocity retraces the same arc back to the shooter. Now the
			// player's spit, so it (and its puddle) hits enemies instead.
			_reflected = true;
			_velocity = -_velocity;
			_shooterStats = targetStats;
			PuddleTargetGroup = "enemy";
			PuddleCollisionMask = ProjectileReflect.EnemyLayer;
			ProjectileReflect.Retarget(this);
			return;
		}
		if (targetStats is not null && targetStats != _shooterStats && !targetStats.IsInvulnerable)
		{
			targetStats.TakeDamage(ContactDamage, isProjectile: true, element: DamageElement.Poison);
			if (body.HasMethod("ApplyPoison"))
				body.Call("ApplyPoison", PoisonTickDamage, PoisonTickInterval, PoisonDuration);
			Splatter(GlobalPosition);
		}
		else if (body is StaticBody2D staticBody)
		{
			if ((staticBody.CollisionLayer & PhysicsLayers.OneWayPlatforms) != 0)
				return;

			Splatter(GlobalPosition);
		}
	}

	private void Splatter(Vector2 position)
	{
		if (_splattered || !IsInstanceValid(this))
			return;
		_splattered = true;

		if (PuddleScene is not null)
		{
			// Deferred: Splatter can run from inside the BodyEntered physics callback, and adding
			// a new collision-carrying node (the puddle is an Area2D) synchronously mid-query-flush
			// throws "Can't change this state while flushing queries".
			Node currentScene = GetTree().CurrentScene;
			Callable.From(() =>
			{
				var puddle = PuddleScene.Instantiate<Node2D>();
				if (puddle is PoisonPuddle poisonPuddle)
				{
					poisonPuddle.TargetGroup = PuddleTargetGroup;
					if (PuddleCollisionMask != 0)
						poisonPuddle.CollisionMask = PuddleCollisionMask;
				}
				currentScene.AddChild(puddle);
				puddle.GlobalPosition = position;
			}).CallDeferred();
		}

		QueueFree();
	}
}
