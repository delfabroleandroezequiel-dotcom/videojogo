using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Poza de ácido: the mine's flooded sump — the "floor is lava" under ropes, carts and trestles.
// The node is the pool's surface-left corner; Width x Depth is the liquid (draw it inside a pit).
// Touching it costs Damage and puts the player back on the last solid, static ground they stood
// on (Hollow Knight style: a missed rope swing is a punishment, not a trip back to the hoguera).
// Moving platforms and carts never count as "safe ground", so you can't be dropped back over the
// chasm you just fell into. Enemies that fall in die (knock a bandit off the cart over it).
// InstantKill turns it into a classic death pit instead. Procedural: dark body, glowing neon
// surface that ripples, rising bubbles, a light.
[Tool]
public partial class AcidPool : Node2D
{
	private float _width = 300f;
	private float _depth = 60f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(16f, value); QueueRedraw(); }
	}

	[Export]
	public float Depth
	{
		get => _depth;
		set { _depth = Mathf.Max(8f, value); QueueRedraw(); }
	}

	[Export] public int Damage = 25;
	[Export] public bool InstantKill;
	[Export] public Color AcidColor = new(0.45f, 1f, 0.25f, 1f);

	// Last safe standing spot, shared by every pool in the scene (static: one player).
	private static Vector2? _lastSafe;
	private static ulong _lastSafeFrame;
	private static float _standTime;

	private Line2D _surface;
	private Line2D _surfaceGlow;
	private float _time;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		_lastSafe = null;
		BuildVisual();

		var area = new Area2D { Name = "Acido", CollisionLayer = 0, CollisionMask = 2 | 4, Monitorable = false };
		// The hurt line sits a few px under the surface so skimming the edge of a pit doesn't count.
		area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, _depth) }, Position = new Vector2(_width / 2f, _depth / 2f + 6f) });
		AddChild(area);
		area.BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		_time += (float)delta;
		AnimateSurface();
		TrackSafeGround((float)delta);
	}

	// Only one pool per frame does the tracking (they share the static).
	private void TrackSafeGround(float dt)
	{
		ulong frame = Engine.GetPhysicsFrames();
		if (_lastSafeFrame == frame)
			return;
		_lastSafeFrame = frame;
		if (GetTree().GetFirstNodeInGroup("player") is not CharacterBody2D player || !player.IsOnFloor())
		{
			_standTime = 0f;
			return;
		}
		GodotObject floor = null;
		for (int i = 0; i < player.GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D c = player.GetSlideCollision(i);
			if (c.GetNormal().Y < -0.5f)
				floor = c.GetCollider();
		}
		if (floor is AnimatableBody2D || floor is null)
		{
			_standTime = 0f;
			return;
		}
		_standTime += dt;
		if (_standTime > 0.15f)
			_lastSafe = player.GlobalPosition;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("enemy"))
		{
			ProjectileReflect.TryHitEnemy(body, 9999, Vector2.Up, 0f);
			Splash(body.GlobalPosition);
			return;
		}
		if (!body.IsInGroup("player") || body.GetNodeOrNull<Stats>("Stats") is not { } stats)
			return;

		Splash(body.GlobalPosition);
		if (InstantKill || _lastSafe == null)
		{
			stats.Kill();
			return;
		}
		// Traps are never parried (same reason as Hazard).
		System.Func<bool> interceptor = stats.IncomingHitInterceptor;
		stats.IncomingHitInterceptor = null;
		try
		{
			stats.TakeDamage(Damage);
		}
		finally
		{
			stats.IncomingHitInterceptor = interceptor;
		}
		if (stats.CurrentHealth <= 0)
			return;
		Vector2 safe = _lastSafe.Value;
		Callable.From(() =>
		{
			if (!IsInstanceValid(body))
				return;
			body.GlobalPosition = safe;
			if (body is CharacterBody2D c)
				c.Velocity = Vector2.Zero;
		}).CallDeferred();
	}

	// ── Visual ─────────────────────────────────────────────────────────────────────────────────

	private void BuildVisual()
	{
		AddChild(new Polygon2D
		{
			Polygon = new[] { Vector2.Zero, new Vector2(_width, 0f), new Vector2(_width, _depth), new Vector2(0f, _depth) },
			Color = AcidColor.Darkened(0.75f) with { A = 0.92f },
		});
		_surfaceGlow = new Line2D { Width = 8f, DefaultColor = AcidColor with { A = 0.35f }, Material = Additive };
		_surface = new Line2D { Width = 2.5f, DefaultColor = AcidColor, Material = Additive };
		AddChild(_surfaceGlow);
		AddChild(_surface);
		AnimateSurface();

		AddChild(new CpuParticles2D
		{
			Texture = Dot,
			Material = Additive,
			Amount = Mathf.Max(6, (int)(_width / 25f)),
			Lifetime = 1.4f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width / 2f, _depth / 3f),
			Position = new Vector2(_width / 2f, _depth * 0.55f),
			Direction = Vector2.Up,
			Spread = 8f,
			InitialVelocityMin = 12f,
			InitialVelocityMax = 30f,
			Gravity = Vector2.Zero,
			ScaleAmountMin = 0.02f,
			ScaleAmountMax = 0.05f,
			Color = AcidColor with { A = 0.8f },
		});
		for (float x = 60f; x < _width; x += 160f)
			AddChild(new PointLight2D { Texture = Dot, Position = new Vector2(x, 0f), TextureScale = 3f, Energy = 0.5f, Color = AcidColor });
	}

	private void AnimateSurface()
	{
		if (_surface == null)
			return;
		int n = Mathf.Max(2, (int)(_width / 12f)) + 1;
		var pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float x = _width * i / (n - 1);
			pts[i] = new Vector2(x, Mathf.Sin(x * 0.05f + _time * 2.2f) * 1.6f + Mathf.Sin(x * 0.13f - _time * 3.1f) * 0.8f);
		}
		_surface.Points = pts;
		_surfaceGlow.Points = pts;
	}

	private void Splash(Vector2 at)
	{
		var splash = new CpuParticles2D
		{
			Texture = Dot,
			Material = Additive,
			Amount = 24,
			Lifetime = 0.6f,
			OneShot = true,
			Explosiveness = 1f,
			Direction = Vector2.Up,
			Spread = 50f,
			InitialVelocityMin = 80f,
			InitialVelocityMax = 200f,
			Gravity = new Vector2(0f, 600f),
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.06f,
			Color = AcidColor,
		};
		AddChild(splash);
		splash.GlobalPosition = new Vector2(at.X, GlobalPosition.Y);
		splash.Emitting = true;
		GetTree().CreateTimer(1f).Timeout += splash.QueueFree;
		Sfx.PlayAt(this, "Magic", "Spell Impact");
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		DrawRect(new Rect2(0f, 0f, _width, _depth), AcidColor with { A = 0.35f });
		DrawLine(Vector2.Zero, new Vector2(_width, 0f), AcidColor, 2f);
	}
}
