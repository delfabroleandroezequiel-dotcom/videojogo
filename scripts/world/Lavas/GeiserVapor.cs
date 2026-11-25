using Godot;

namespace Metroidvania.World;

// Géiser de vapor (Las Lavas): a vent in the rock that bubbles, warns and then blows a column of
// steam that throws the player upward. It's transport, not a trap — the zone teaches it as the only
// way up, then chains and staggers them. Fixed rhythm: Interval idle → WarningTime bubbling (the
// tell: shaking lip, sputtering puffs) → BurstTime blowing. LaunchVelocity behaves like BouncePad's
// (rise ≈ v² / 1800: 700 ≈ 270 px, 820 ≈ 370 px, 950 ≈ 500 px).
// Marea (optional): a MareaDeLava whose surface, when it's above the vent, drowns it — no bursts
// until the lava goes back down (Cámara de la marea: wait for low tide to ride them).
// Node origin = the middle of the vent's mouth; the column grows up (−Y). Drawn in code.
[Tool]
public partial class GeiserVapor : Node2D
{
	private float _width = 56f;
	private float _height = 360f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(24f, value); QueueRedraw(); }
	}

	// How high the steam column reaches (also the zone where it grabs the player).
	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(60f, value); QueueRedraw(); }
	}

	[Export] public float LaunchVelocity = 820f;
	[Export] public float Interval = 1.4f;
	[Export] public float WarningTime = 0.6f;
	[Export] public float BurstTime = 0.8f;
	// Seconds into the cycle this vent starts at (to stagger a chain of them).
	[Export] public float StartOffset;
	[Export] public NodePath Marea;
	[Export] public Color SteamColor = new(1f, 0.93f, 0.85f, 1f);
	[Export] public Color GlowColor = new(1f, 0.45f, 0.12f, 1f);

	private enum Phase { Idle, Warning, Burst }

	private Phase _phase = Phase.Idle;
	private float _timer;
	private float _shake;
	private Area2D _zone;
	private CpuParticles2D _steam;
	private CpuParticles2D _sputter;
	private MareaDeLava _marea;

	public bool IsBlowing => _phase == Phase.Burst && !Drowned;

	private bool Drowned => _marea is not null && _marea.SurfaceY < GlobalPosition.Y - 4f;

	public override void _Ready()
	{
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;
		_marea = GetNodeOrNull<MareaDeLava>(Marea);
		_zone = new Area2D { Name = "Columna", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_zone.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width, _height) },
			Position = new Vector2(0f, -_height / 2f),
		});
		AddChild(_zone);

		float life = 0.9f;
		_steam = new CpuParticles2D
		{
			Name = "Vapor",
			Emitting = false,
			Amount = 70,
			Lifetime = life,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width * 0.3f, 4f),
			Direction = Vector2.Up,
			Spread = 7f,
			Gravity = Vector2.Zero,
			InitialVelocityMin = _height / life * 0.9f,
			InitialVelocityMax = _height / life * 1.15f,
			DampingMin = 10f,
			DampingMax = 40f,
			ScaleAmountMin = 3f,
			ScaleAmountMax = 7f,
			Color = SteamColor,
			ZIndex = 2,
		};
		_steam.ScaleAmountCurve = new Curve();
		_steam.ScaleAmountCurve.AddPoint(new Vector2(0f, 0.5f));
		_steam.ScaleAmountCurve.AddPoint(new Vector2(1f, 1.6f));
		var fade = new Gradient();
		fade.SetColor(0, new Color(SteamColor, 0.85f));
		fade.SetColor(1, new Color(SteamColor, 0f));
		_steam.ColorRamp = fade;
		AddChild(_steam);

		_sputter = new CpuParticles2D
		{
			Name = "Burbujeo",
			Emitting = false,
			Amount = 14,
			Lifetime = 0.45f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width * 0.35f, 2f),
			Direction = Vector2.Up,
			Spread = 25f,
			Gravity = new Vector2(0f, 300f),
			InitialVelocityMin = 60f,
			InitialVelocityMax = 150f,
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			Color = GlowColor,
		};
		AddChild(_sputter);

		_timer = Interval - (StartOffset % (Interval + WarningTime + BurstTime));
		if (_timer < 0f)
			_timer += Interval + WarningTime + BurstTime;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		_timer -= dt;
		if (_timer <= 0f)
			Advance();

		bool drowned = Drowned;
		_sputter.Emitting = _phase == Phase.Warning && !drowned;
		_steam.Emitting = _phase == Phase.Burst && !drowned;
		_shake = _phase == Phase.Warning && !drowned ? 1.5f : _shake * 0.8f;

		if (IsBlowing)
		{
			foreach (Node2D body in _zone.GetOverlappingBodies())
			{
				if (!body.IsInGroup("player") || body is not CharacterBody2D player)
					continue;
				if (player.Velocity.Y > -LaunchVelocity * 0.55f)
					player.Velocity = new Vector2(player.Velocity.X, -LaunchVelocity);
			}
		}
		QueueRedraw();
	}

	private void Advance()
	{
		switch (_phase)
		{
			case Phase.Idle:
				_phase = Phase.Warning;
				_timer += WarningTime;
				break;
			case Phase.Warning:
				_phase = Phase.Burst;
				_timer += BurstTime;
				break;
			default:
				_phase = Phase.Idle;
				_timer += Interval;
				break;
		}
	}

	public override void _Draw()
	{
		float jitter = _shake > 0.05f ? (float)GD.RandRange(-_shake, _shake) : 0f;
		float w = _width;
		// The vent's lip: an uneven dark rock mouth with a glowing throat.
		var lip = new Vector2[]
		{
			new(-w * 0.75f + jitter, 10f), new(-w * 0.6f, -4f + jitter), new(-w * 0.32f, -9f), new(-w * 0.12f, -3f),
			new(w * 0.14f, -4f), new(w * 0.36f, -10f + jitter), new(w * 0.62f, -3f), new(w * 0.78f + jitter, 10f),
		};
		DrawColoredPolygon(lip, new Color(0.13f, 0.09f, 0.08f, 1f));
		bool hot = _phase != Phase.Idle && !Engine.IsEditorHint() && !Drowned;
		float glow = hot ? (_phase == Phase.Burst ? 1f : 0.65f) : 0.3f;
		DrawRect(new Rect2(-w * 0.32f, -6f, w * 0.64f, 8f), new Color(GlowColor, glow));
		DrawRect(new Rect2(-w * 0.2f, -5f, w * 0.4f, 5f), new Color(1f, 0.85f, 0.5f, glow));
		if (Engine.IsEditorHint())
			DrawRect(new Rect2(-w / 2f, -_height, w, _height), new Color(SteamColor, 0.08f), true);
	}
}
