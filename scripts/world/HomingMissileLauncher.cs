using Godot;

namespace Metroidvania.World;

// Lanzamisiles teledirigidos (after Super Meat Boy's missile launchers): a turret that, while the
// player is within Range, fires a missile every Interval. Missiles steer toward the player at
// TurnRate (rad/s), so you dodge by making them overshoot or lure them into walls; they explode on
// the world, on the player, or when cut by the sword. Origin = the turret.
public partial class HomingMissileLauncher : Node2D
{
	[Export] public float Range = 700f;
	[Export] public float Interval = 2.4f;
	[Export] public float MissileSpeed = 230f;
	[Export] public float TurnRate = 2.4f;
	[Export] public float Lifetime = 6f;
	[Export] public int Damage = 25;
	[Export] public float BlastRadius = 55f;
	[Export] public float Knockback = 420f;
	[Export] public Color GlowColor = new(1f, 0.35f, 0.25f, 1f);

	private float _timer;
	private Node2D _barrel;

	public override void _Ready()
	{
		AddChild(TrapKit.Poly(TrapKit.Circle(Vector2.Zero, 18f, 16), new Color(0.2f, 0.2f, 0.24f)));
		_barrel = new Node2D();
		AddChild(_barrel);
		_barrel.AddChild(TrapKit.Poly(TrapKit.Rect(new Vector2(16f, 0f), new Vector2(26f, 12f)), new Color(0.3f, 0.3f, 0.34f)));
		_barrel.AddChild(TrapKit.Line(new[] { new Vector2(28f, -6f), new Vector2(28f, 6f) }, 2f, GlowColor));
		_timer = Interval * 0.5f;
	}

	public override void _PhysicsProcess(double delta)
	{
		var player = TrapKit.FindPlayer(this);
		if (player is null)
			return;
		Vector2 to = player.GlobalPosition + new Vector2(0f, -20f) - GlobalPosition;
		_barrel.Rotation = Mathf.LerpAngle(_barrel.Rotation, to.Angle(), 0.1f);
		if (to.Length() > Range)
			return;
		_timer += (float)delta;
		if (_timer < Interval)
			return;
		_timer = 0f;
		var m = new Missile { Launcher = this, Heading = Vector2.Right.Rotated(_barrel.Rotation) };
		GetParent().AddChild(m);
		m.GlobalPosition = GlobalPosition + m.Heading * 30f;
		TrapKit.Burst(this, m.GlobalPosition, GlowColor, 8, 120f);
	}

	public partial class Missile : CharacterBody2D
	{
		public HomingMissileLauncher Launcher;
		public Vector2 Heading;
		private float _age;
		private Area2D _hurt;
		private Node2D _visual;
		private bool _done;

		public override void _Ready()
		{
			CollisionLayer = 4;
			CollisionMask = 0;
			AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 9f } });
			var stats = new Metroidvania.Shared.Stats { Name = "Stats", MaxHealth = 1 };
			AddChild(stats);
			stats.Died += () => CallDeferred(MethodName.Explode);
			_hurt = new Area2D { CollisionLayer = 0, CollisionMask = 1u | 2u, Monitorable = false };
			_hurt.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 9f } });
			AddChild(_hurt);
			_hurt.BodyEntered += b => { if (b != this && b is not HomingMissileLauncher) CallDeferred(MethodName.Explode); };
			_visual = new Node2D();
			AddChild(_visual);
			_visual.AddChild(TrapKit.Poly(new[] { new Vector2(-10f, -5f), new Vector2(6f, -5f), new Vector2(12f, 0f), new Vector2(6f, 5f), new Vector2(-10f, 5f) }, new Color(0.75f, 0.75f, 0.78f)));
			_visual.AddChild(TrapKit.Poly(new[] { new Vector2(-10f, -5f), new Vector2(-14f, -9f), new Vector2(-6f, -5f) }, Launcher.GlowColor.Darkened(0.3f)));
			_visual.AddChild(TrapKit.Poly(new[] { new Vector2(-10f, 5f), new Vector2(-14f, 9f), new Vector2(-6f, 5f) }, Launcher.GlowColor.Darkened(0.3f)));
			_visual.AddChild(new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = Launcher.GlowColor, Position = new Vector2(-12f, 0f), Scale = Vector2.One * 0.15f });
			var smoke = new GpuParticles2D
			{
				Amount = 30, Lifetime = 0.6, LocalCoords = false, Texture = TrapKit.Dot(), Modulate = new Color(0.6f, 0.6f, 0.65f, 0.5f), Position = new Vector2(-12f, 0f),
				ProcessMaterial = new ParticleProcessMaterial { Spread = 30f, InitialVelocityMin = 5f, InitialVelocityMax = 20f, Gravity = new Vector3(0f, -20f, 0f), ScaleMin = 0.05f, ScaleMax = 0.1f },
			};
			_visual.AddChild(smoke);
			_visual.AddChild(TrapKit.Light(Launcher.GlowColor, 0.8f, 0.7f));
		}

		public override void _PhysicsProcess(double delta)
		{
			if (_done || !IsInstanceValid(Launcher))
			{
				QueueFree();
				return;
			}
			float dt = (float)delta;
			_age += dt;
			if (TrapKit.FindPlayer(this) is Node2D p)
			{
				float want = (p.GlobalPosition + new Vector2(0f, -20f) - GlobalPosition).Angle();
				float cur = Heading.Angle();
				float diff = Mathf.Wrap(want - cur, -Mathf.Pi, Mathf.Pi);
				Heading = Vector2.Right.Rotated(cur + Mathf.Clamp(diff, -Launcher.TurnRate * dt, Launcher.TurnRate * dt));
			}
			GlobalPosition += Heading * Launcher.MissileSpeed * dt;
			_visual.Rotation = Heading.Angle();
			if (_age >= Launcher.Lifetime)
				Explode();
		}

		private void Explode()
		{
			if (_done)
				return;
			_done = true;
			TrapKit.Blast(this, GlobalPosition, Launcher.BlastRadius, Launcher.GlowColor);
			if (TrapKit.FindPlayer(this) is Node2D p && p.GlobalPosition.DistanceTo(GlobalPosition) <= Launcher.BlastRadius + 20f)
				TrapKit.Hurt(p, Launcher.Damage, p.GlobalPosition - GlobalPosition, Launcher.Knockback);
			QueueFree();
		}
	}
}
