using Godot;

namespace Metroidvania.World;

// Saco de huevos (after Spelunky 2's egg sac): a pulsing sac stuck to a wall/ceiling/floor. Touch
// it or hit it and it bursts, spilling SpawnCount spiders (SpawnScene, SpiderEnemy by default)
// that pop out in a fan. One-shot per visit. It's a StaticBody2D on the enemy layer with a Stats
// child so the player's sword can pop it from a distance (Hitbox looks for "Stats").
public partial class EggSac : StaticBody2D
{
	[Export] public PackedScene SpawnScene;
	[Export] public int SpawnCount = 3;
	[Export] public float Radius = 26f;
	[Export] public float PopSpeed = 220f;
	[Export] public Color SacColor = new(0.75f, 0.7f, 0.55f, 1f);
	[Export] public Color VeinColor = new(0.6f, 1f, 0.4f, 1f);

	private Area2D _touch;
	private Node2D _visual;
	private bool _burst;

	public override void _Ready()
	{
		SpawnScene ??= GD.Load<PackedScene>("res://scenes/world/Enemigos/SpiderEnemy.tscn");
		CollisionLayer = 4;
		CollisionMask = 0;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
		var stats = new Metroidvania.Shared.Stats { Name = "Stats", MaxHealth = 1 };
		AddChild(stats);
		stats.Died += () => CallDeferred(MethodName.Burst);
		_touch = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_touch.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius + 6f } });
		AddChild(_touch);
		_touch.BodyEntered += b => { if (TrapKit.IsPlayer(b)) CallDeferred(MethodName.Burst); };

		_visual = new Node2D();
		AddChild(_visual);
		_visual.AddChild(TrapKit.Poly(TrapKit.Circle(Vector2.Zero, Radius, 18), SacColor));
		var rng = new RandomNumberGenerator { Seed = 11 };
		for (int i = 0; i < 5; i++)
		{
			float a = Mathf.Tau * i / 5f + rng.RandfRange(-0.3f, 0.3f);
			Vector2 p1 = Vector2.Right.Rotated(a) * Radius * 0.2f, p2 = Vector2.Right.Rotated(a + 0.4f) * Radius * 0.6f, p3 = Vector2.Right.Rotated(a + 0.1f) * Radius * 0.95f;
			_visual.AddChild(TrapKit.Line(new[] { p1, p2, p3 }, 1.5f, VeinColor with { A = 0.6f }));
		}
		for (int i = 0; i < 4; i++)
			_visual.AddChild(TrapKit.Poly(TrapKit.Circle(new Vector2(rng.RandfRange(-10f, 10f), rng.RandfRange(-10f, 10f)), 5f, 8), SacColor.Darkened(0.35f)));
		var glow = TrapKit.Light(VeinColor, 0.5f, 0.7f);
		_visual.AddChild(glow);
	}

	public override void _Process(double delta)
	{
		float t = (float)Time.GetTicksMsec() * 0.004f;
		_visual.Scale = new Vector2(1f + Mathf.Sin(t) * 0.05f, 1f - Mathf.Sin(t) * 0.05f);
	}

	private void Burst()
	{
		if (_burst || !IsInsideTree())
			return;
		_burst = true;
		TrapKit.Burst(this, GlobalPosition, SacColor, 30, 260f);
		TrapKit.Burst(this, GlobalPosition, VeinColor, 14, 180f);
		Node parent = GetParent();
		for (int i = 0; i < SpawnCount; i++)
		{
			if (SpawnScene?.Instantiate() is not Node2D spider)
				break;
			spider.Name = $"{Name}_Cria{i}";
			parent.AddChild(spider);
			spider.GlobalPosition = GlobalPosition + new Vector2((i - (SpawnCount - 1) / 2f) * 24f, -6f);
			if (spider is CharacterBody2D body)
				body.Velocity = new Vector2((i - (SpawnCount - 1) / 2f) * PopSpeed * 0.6f, -PopSpeed);
		}
		QueueFree();
	}
}
