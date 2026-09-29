using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Procedural tornado (no art): a funnel of stacked elliptical wind rings — narrow at the ground,
// wide at the top — each spinning at its own speed and drawn as a partial arc (brighter in front),
// the whole column snaking side to side, plus debris spiralling up it and dust kicked up at the
// base. Grows in, travels, then disperses.
// Gameplay: enemies inside the pull radius get sucked toward the center and lifted (bosses only take
// damage), and every TickInterval everything inside takes a hit. Caster decides the stats (the
// player's spell passes its own attack power in Setup).
public partial class Tornado : Node2D
{
	[ExportGroup("Shape")]
	[Export] public float Height = 150f;
	[Export] public float BaseRadius = 10f;
	[Export] public float TopRadius = 55f;
	// Ellipse squash: how flat each ring looks (depth read).
	[Export] public float RingFlatten = 0.28f;
	[Export] public int RingCount = 22;
	[Export] public float RingWidth = 2.5f;
	// How much of each ring's circle is drawn, in degrees (gaps read as swirling wind).
	[Export] public float RingArcDegrees = 230f;
	[Export] public float SpinSpeed = 7f;
	[Export] public float SwayAmplitude = 12f;
	[Export] public float SwaySpeed = 3f;
	[Export] public Color WindColor = new(0.85f, 0.9f, 0.95f, 0.7f);
	[Export] public Color DebrisColor = new(0.55f, 0.5f, 0.42f, 1f);

	[ExportGroup("Motion")]
	[Export] public float Speed = 150f;
	[Export] public float Lifetime = 3.5f;
	[Export] public float GrowTime = 0.3f;
	[Export] public float FadeTime = 0.4f;

	[ExportGroup("Damage")]
	[Export] public int AttackPower = 10;
	[Export] public float TickInterval = 0.3f;
	[Export] public float PullRadius = 90f;
	[Export] public float PullSpeed = 140f;
	[Export] public float LiftSpeed = 280f;
	// Enemy layer.
	[Export(PropertyHint.Layers2DPhysics)] public uint TargetMask = 4;

	private float _direction = 1f;
	private float _time;
	private float _tickTimer;
	private float _presence;
	private Node2D _rings;
	private readonly List<Line2D> _ringLines = new();
	private readonly List<float> _ringPhase = new();
	private Area2D _area;
	private CpuParticles2D _debris;
	private CpuParticles2D _dust;

	public void Setup(float direction, int attackPower)
	{
		_direction = direction >= 0f ? 1f : -1f;
		AttackPower = attackPower;
	}

	public override void _Ready()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		_rings = new Node2D();
		AddChild(_rings);
		for (int i = 0; i < RingCount; i++)
		{
			var line = new Line2D
			{
				Width = RingWidth,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Gradient = RingGradient(),
				DefaultColor = Colors.White,
			};
			_rings.AddChild(line);
			_ringLines.Add(line);
			_ringPhase.Add(rng.RandfRange(0f, Mathf.Tau));
		}

		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var shrink = new Curve();
		shrink.AddPoint(new Vector2(0f, 1f));
		shrink.AddPoint(new Vector2(1f, 0.2f));

		// Debris: rises up the funnel while orbiting its axis.
		_debris = new CpuParticles2D
		{
			Amount = 40,
			Lifetime = 1.1f,
			Texture = soft,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(BaseRadius, 4f),
			Direction = Vector2.Up,
			Spread = 12f,
			Gravity = new Vector2(0f, -120f),
			InitialVelocityMin = 90f,
			InitialVelocityMax = 150f,
			OrbitVelocityMin = 0.6f,
			OrbitVelocityMax = 1.1f,
			RadialAccelMin = 30f,
			RadialAccelMax = 60f,
			ScaleAmountMin = 0.05f,
			ScaleAmountMax = 0.1f,
			ScaleAmountCurve = shrink,
			Color = DebrisColor,
			LocalCoords = true,
		};
		AddChild(_debris);

		// Dust puffing out at the base.
		_dust = new CpuParticles2D
		{
			Amount = 18,
			Lifetime = 0.8f,
			Texture = soft,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(BaseRadius * 1.5f, 2f),
			Direction = Vector2.Up,
			Spread = 80f,
			Gravity = new Vector2(0f, 20f),
			InitialVelocityMin = 30f,
			InitialVelocityMax = 70f,
			ScaleAmountMin = 0.25f,
			ScaleAmountMax = 0.45f,
			ScaleAmountCurve = shrink,
			Color = WindColor with { A = 0.35f },
			LocalCoords = false,
		};
		AddChild(_dust);

		_area = new Area2D { CollisionLayer = 0, CollisionMask = TargetMask, Monitorable = false };
		_area.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(PullRadius * 2f, Height) },
			Position = new Vector2(0f, -Height / 2f),
		});
		AddChild(_area);

		UpdateRings();
	}

	private Gradient RingGradient()
	{
		// Faint at both ends of the arc, strongest in the middle (the front of the ring).
		var gradient = new Gradient();
		gradient.SetColor(0, WindColor with { A = 0f });
		gradient.SetColor(1, WindColor with { A = 0f });
		gradient.AddPoint(0.5f, WindColor);
		return gradient;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;

		// Grow in, hold, fade out.
		_presence = _time < GrowTime ? _time / GrowTime
			: _time > Lifetime - FadeTime ? Mathf.Max(0f, (Lifetime - _time) / FadeTime)
			: 1f;
		Modulate = new Color(1f, 1f, 1f, _presence);
		if (_time >= Lifetime)
		{
			QueueFree();
			return;
		}

		Position += new Vector2(_direction * Speed * dt, 0f);
		UpdateRings();

		_tickTimer -= dt;
		PullEnemies(dt);
		if (_tickTimer <= 0f)
		{
			_tickTimer = TickInterval;
			DamageEnemies();
		}
	}

	private float SwayAt(float t) => SwayAmplitude * t * Mathf.Sin(_time * SwaySpeed + t * 3f);

	private void UpdateRings()
	{
		const int points = 18;
		float arc = Mathf.DegToRad(RingArcDegrees);
		float scaleY = 0.35f + 0.65f * _presence;
		for (int i = 0; i < _ringLines.Count; i++)
		{
			float t = (float)i / Mathf.Max(1, _ringLines.Count - 1);
			float radius = Mathf.Lerp(BaseRadius, TopRadius, Mathf.Pow(t, 1.4f));
			float y = -Height * t * scaleY;
			float x = SwayAt(t);
			// Lower rings spin faster, like a real funnel.
			float start = _ringPhase[i] + _time * SpinSpeed * (1.3f - 0.6f * t);
			var arcPoints = new Vector2[points];
			for (int k = 0; k < points; k++)
			{
				float angle = start + arc * k / (points - 1);
				arcPoints[k] = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius * RingFlatten);
			}
			Line2D line = _ringLines[i];
			line.Points = arcPoints;
			line.Width = RingWidth * (0.7f + 0.6f * t);
		}
	}

	// Enemies near the funnel drift toward its axis (pull), and get lifted once they're close.
	private void PullEnemies(float dt)
	{
		foreach (Node2D body in _area.GetOverlappingBodies())
		{
			if (body is not Enemy enemy || enemy.IsInGroup("boss"))
				continue;
			float dx = GlobalPosition.X - enemy.GlobalPosition.X;
			float closeness = 1f - Mathf.Clamp(Mathf.Abs(dx) / PullRadius, 0f, 1f);
			var push = new Vector2(Mathf.Sign(dx) * PullSpeed * (0.4f + closeness), closeness > 0.6f ? -LiftSpeed : 0f);
			enemy.Launch(push, 0.12f);
		}
	}

	private void DamageEnemies()
	{
		foreach (Node2D body in _area.GetOverlappingBodies())
		{
			if (body is not Enemy enemy || enemy.Stats is null)
				continue;
			enemy.Stats.TakeDamage(AttackPower, ignoreInvulnerability: true, armInvulnerability: false);
		}
	}
}
