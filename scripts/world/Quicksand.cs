using Godot;

namespace Metroidvania.World;

// Arenas movedizas: a pit of sand you can walk onto — and then you start going under. Standing in
// it sinks you SinkSpeed px/s; every jump only lifts you JumpLift px out (jumps are weak in sand,
// capped at JumpCap) and walking is dragged; once you're SinkToDeath px deep, it swallows you.
// Hop out sideways before that. Built as a one-way "footing" platform that slides down with you,
// so the player keeps real floor contact (jumps work normally) while it sinks. The sand is drawn in
// front of the player so they visibly go under. Origin = top-left of the sand surface.
public partial class Quicksand : Node2D
{
	[Export] public float Width = 260f;
	[Export] public float Depth = 120f;
	[Export] public float SinkSpeed = 22f;
	[Export] public float JumpLift = 22f;
	[Export] public float JumpCap = 340f;
	[Export] public float Drag = 0.35f;
	[Export] public float SinkToDeath = 70f;
	[Export] public Color SandColor = new(0.62f, 0.5f, 0.32f, 1f);

	private AnimatableBody2D _footing;
	private Area2D _area;
	private float _sink;
	private bool _wasOnFooting;
	private Line2D _surface;
	private GpuParticles2D _bubbles;

	public override void _Ready()
	{
		// Bottom: a solid floor at Depth so nothing falls through the level.
		var bottom = new StaticBody2D { CollisionLayer = 1 };
		bottom.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, 20f) }, Position = new Vector2(Width / 2f, Depth + 10f) });
		AddChild(bottom);

		_footing = new AnimatableBody2D { SyncToPhysics = true, CollisionLayer = 1u | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms };
		_footing.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, 10f) }, Position = new Vector2(Width / 2f, 5f), OneWayCollision = true });
		AddChild(_footing);

		_area = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, Depth + 40f) }, Position = new Vector2(Width / 2f, Depth / 2f - 10f) });
		AddChild(_area);

		// Sand body, in front of the player.
		var sand = TrapKit.Poly(new[] { new Vector2(0f, 0f), new Vector2(Width, 0f), new Vector2(Width, Depth + 20f), new Vector2(0f, Depth + 20f) }, SandColor);
		sand.ZIndex = 10;
		AddChild(sand);
		_surface = TrapKit.Line(System.Array.Empty<Vector2>(), 3f, SandColor.Lightened(0.3f), false);
		_surface.ZIndex = 11;
		AddChild(_surface);
		_bubbles = new GpuParticles2D
		{
			Amount = 14, Lifetime = 0.7, ZIndex = 11, Texture = TrapKit.Dot(), Modulate = SandColor.Lightened(0.4f),
			Position = new Vector2(Width / 2f, 2f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(Width / 2f, 1f, 0f),
				Direction = new Vector3(0f, -1f, 0f), Spread = 20f, InitialVelocityMin = 10f, InitialVelocityMax = 30f, Gravity = new Vector3(0f, 40f, 0f), ScaleMin = 0.02f, ScaleMax = 0.05f,
			},
		};
		AddChild(_bubbles);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		float t = (float)Time.GetTicksMsec() * 0.003f;
		var pts = new Vector2[13];
		for (int i = 0; i <= 12; i++)
			pts[i] = new Vector2(Width * i / 12f, Mathf.Sin(t + i * 0.9f) * 1.5f);
		_surface.Points = pts;

		CharacterBody2D player = null;
		foreach (Node2D b in _area.GetOverlappingBodies())
			if (b is CharacterBody2D c && TrapKit.IsPlayer(c))
				player = c;

		if (player is null)
		{
			// Nobody in it: the footing floats back to the surface.
			_sink = Mathf.Max(0f, _sink - 120f * dt);
			_wasOnFooting = false;
		}
		else
		{
			bool onFooting = player.IsOnFloor() && player.GetFloorNormal().Y < -0.5f && Mathf.Abs(player.GlobalPosition.Y + 32f - _footing.GlobalPosition.Y) < 12f;
			if (onFooting)
				_sink += SinkSpeed * dt;
			// Leaving the footing upward = a jump out of the sand: it's weak, and lifts you a little.
			if (_wasOnFooting && !player.IsOnFloor() && player.Velocity.Y < 0f)
			{
				_sink = Mathf.Max(0f, _sink - JumpLift);
				if (player.Velocity.Y < -JumpCap)
					player.Velocity = new Vector2(player.Velocity.X, -JumpCap);
			}
			_wasOnFooting = onFooting;
			if (onFooting && player.HasMethod("AddExternalDisplacement"))
				player.Call("AddExternalDisplacement", new Vector2(-player.Velocity.X * Drag * dt, 0f));
			if (_sink >= SinkToDeath && player.GetNodeOrNull<Metroidvania.Shared.Stats>("Stats") is { } stats && stats.CurrentHealth > 0)
			{
				stats.Kill();
				_sink = 0f;
			}
		}
		_footing.Position = new Vector2(0f, Mathf.Min(_sink, Depth - 4f));
	}
}
