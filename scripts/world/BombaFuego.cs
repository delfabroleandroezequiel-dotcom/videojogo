using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Lobbed clay fire bomb (BandidoBombas): flies on a gravity arc trailing embers, burns whoever it
// hits and leaves a GroundFire patch where it lands, so a miss still denies ground for a moment.
// A parry just knocks it out of the air (it bursts harmlessly) instead of sending it back.
public partial class BombaFuego : Area2D
{
	[Export] public float ArcGravity = 700f;
	[Export] public float Lifetime = 3.5f;
	[Export] public int ContactDamage = 10;
	[Export] public PackedScene GroundFireScene;
	[Export] public Color TrailColor = new(1f, 0.45f, 0.08f, 1f);

	private Vector2 _velocity;
	private Stats _shooterStats;
	private bool _done;
	private Line2D _trail;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += () => Burst(spawnFire: false);

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 5f,
			Gradient = fade,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
		AddChild(_trail);
	}

	public void Launch(Vector2 velocity, Stats shooterStats)
	{
		_velocity = velocity;
		_shooterStats = shooterStats;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_velocity.Y += ArcGravity * dt;
		Position += _velocity * dt;
		Rotation += 10f * dt;

		_trail.AddPoint(GlobalPosition);
		if (_trail.GetPointCount() > 14)
			_trail.RemovePoint(0);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done)
			return;
		if (body.IsInGroup("player"))
		{
			if (ProjectileReflect.TryParry(body))
			{
				Burst(spawnFire: false);
				return;
			}
			Stats target = body.GetNodeOrNull<Stats>("Stats");
			if (target is not null && target != _shooterStats && !target.IsInvulnerable)
				target.TakeDamage(ContactDamage, isProjectile: true, element: DamageElement.Fire);
			Burst(spawnFire: true);
		}
		else if (body is StaticBody2D floor && (floor.CollisionLayer & PhysicsLayers.OneWayPlatforms) == 0)
		{
			Burst(spawnFire: true);
		}
	}

	private void Burst(bool spawnFire)
	{
		if (_done || !IsInstanceValid(this))
			return;
		_done = true;
		ImpactEffect.SpawnAt(this, GlobalPosition);
		if (spawnFire && GroundFireScene is not null && GetTree().CurrentScene is Node scene)
		{
			Vector2 at = GlobalPosition;
			Callable.From(() =>
			{
				var fire = GroundFireScene.Instantiate<Node2D>();
				scene.AddChild(fire);
				fire.GlobalPosition = at;
			}).CallDeferred();
		}
		QueueFree();
	}
}
