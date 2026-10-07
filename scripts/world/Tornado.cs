using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Procedural tornado (no art): smooth spinning wind rings, Joe Higashi "Hurricane Upper" twist.
// A cone that starts as a point on the ground and widens upward, made of elliptical ring arcs that
// keep climbing the column (fading in at the base, out at the top). Each ring is a soft body line
// with a thin additive crest riding on top for volume, plus short streaks flung off its ends; debris
// spirals up the funnel and dust puffs at the base. Tinted by the caster's element (white, fire, poison, ice,
// lightning). Grows in from the ground, travels, then fades.
// Gameplay: damage only — it never moves enemies (forced displacement caused bugs: enemies pushed
// into walls, off ledges, AI desync). Every TickInterval everything inside the column takes a hit;
// caster decides the stats (the player's spell passes its own attack power in Setup).
public partial class Tornado : Node2D
{
	private const int ArcPoints = 24;

	[ExportGroup("Shape")]
	[Export] public float Height = 220f;
	// Starts as a point on the ground and widens up to TopRadius (ConePower < 1 = widens fast).
	[Export] public float TipRadius = 3f;
	[Export] public float TopRadius = 62f;
	[Export] public float ConePower = 0.65f;
	// Ellipse squash: how flat each ring looks (depth read).
	[Export] public float Flatten = 0.32f;
	[Export] public float BandCount = 13f;
	// Ring line width at the bottom / top of the column.
	[Export] public float ThicknessBottom = 4f;
	[Export] public float ThicknessTop = 14f;
	// How much of each ring's circle is drawn, in degrees (gaps read as swirling wind).
	[Export] public float RingArcDegrees = 230f;
	// Rings per second climbing the column.
	[Export] public float RiseSpeed = 1.4f;
	// Ring spin, radians per second.
	[Export] public float SpinSpeed = 7f;
	[Export] public float SwayAmplitude = 5f;
	// Brightest to darkest: crest, streaks, ring body.
	[Export] public Color[] WindColors = { new(1f, 1f, 1f), new(0.84f, 0.886f, 0.949f), new(0.576f, 0.655f, 0.769f) };
	[Export] public Color[] FireColors = { new(1f, 0.965f, 0.784f), new(0.965f, 0.753f, 0.306f), new(0.722f, 0.416f, 0.18f) };
	[Export] public Color[] PoisonColors = { new(0.92f, 1f, 0.72f), new(0.6f, 0.95f, 0.35f), new(0.3f, 0.6f, 0.2f) };
	[Export] public Color[] IceColors = { new(1f, 1f, 1f), new(0.75f, 0.95f, 1f), new(0.42f, 0.7f, 0.95f) };
	[Export] public Color[] LightningColors = { new(1f, 1f, 0.85f), new(1f, 0.9f, 0.3f), new(0.8f, 0.62f, 0.12f) };
	[Export] public Color DebrisColor = new(0.55f, 0.5f, 0.42f, 1f);

	[ExportGroup("Motion")]
	[Export] public float Speed = 150f;
	[Export] public float Lifetime = 3.5f;
	[Export] public float GrowTime = 0.3f;
	[Export] public float FadeTime = 0.4f;

	[ExportGroup("Damage")]
	[Export] public int AttackPower = 10;
	[Export] public float TickInterval = 0.3f;
	// Half-width of the damage zone — roughly the visible body of the funnel.
	[Export] public float HitHalfWidth = 40f;
	// Enemy layer.
	[Export(PropertyHint.Layers2DPhysics)] public uint TargetMask = 4;

	[ExportGroup("Growing variant")]
	// Size gained per new enemy touched, and the cap (Tornado creciente).
	[Export] public float GrowPerEnemy = 0.18f;
	[Export] public float MaxSizeScale = 1.6f;

	// Size multiplier on the whole column (shape, hit zone and, when growing, damage). Mini
	// tornadoes start small; the growing variant raises it as it touches enemies.
	public float SizeScale { get; private set; } = 1f;

	private sealed class Ring
	{
		public Line2D Body;
		public Line2D Crest;
		public Line2D[] Streaks;
	}

	private Vector2 _travel = Vector2.Right;
	private DamageElement _element;
	private bool _growing;
	private int _baseAttackPower;
	private float _time;
	private float _tickTimer;
	private float _presence;
	private Color[] _palette;
	private Node2D _ringRoot;
	private readonly List<Ring> _rings = new();
	private CpuParticles2D _debris;
	private CpuParticles2D _dust;
	private Area2D _area;
	private CollisionShape2D _hitShape;
	private readonly HashSet<ulong> _touched = new();

	public void Setup(float direction, int attackPower, DamageElement element = DamageElement.Normal) =>
		Setup(new Vector2(direction >= 0f ? 1f : -1f, 0f), attackPower, element);

	// travel: direction of motion (normalized; non-horizontal for airborne mini tornadoes).
	// sizeScale < 1 = mini. growing = Tornado creciente (grows + hits harder per enemy touched).
	public void Setup(Vector2 travel, int attackPower, DamageElement element = DamageElement.Normal, float sizeScale = 1f, bool growing = false)
	{
		_travel = travel.LengthSquared() > 0f ? travel.Normalized() : Vector2.Right;
		AttackPower = attackPower;
		_baseAttackPower = attackPower;
		_element = element;
		SizeScale = sizeScale;
		_growing = growing;
		// A growing tornado starts weak and earns its damage back as it grows (see DamageEnemies).
		if (growing)
			AttackPower = Mathf.Max(1, Mathf.RoundToInt(attackPower * sizeScale));
	}

	public override void _Ready()
	{
		_palette = _element switch
		{
			DamageElement.Fire => FireColors,
			DamageElement.Poison => PoisonColors,
			DamageElement.Ice => IceColors,
			DamageElement.Lightning => LightningColors,
			_ => WindColors,
		};
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		_ringRoot = new Node2D();
		AddChild(_ringRoot);
		int count = Mathf.Max(1, Mathf.RoundToInt(BandCount));
		for (int i = 0; i < count; i++)
		{
			var ring = new Ring
			{
				Body = NewLine(Ends(_palette[2])),
				Crest = NewLine(Ends(_palette[0])),
				Streaks = new[] { NewLine(Tail(_palette[1])), NewLine(Tail(_palette[1])) },
			};
			ring.Crest.Material = additive;
			ring.Body.Points = new Vector2[ArcPoints];
			ring.Crest.Points = new Vector2[ArcPoints];
			_ringRoot.AddChild(ring.Body);
			_ringRoot.AddChild(ring.Crest);
			foreach (Line2D streak in ring.Streaks)
				_ringRoot.AddChild(streak);
			_rings.Add(ring);
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
			Color = _element == DamageElement.Normal ? DebrisColor : _palette[1],
			LocalCoords = true,
		};
		AddChild(_debris);

		// Dust puffing out at the base — only for tornadoes standing on the ground.
		_dust = new CpuParticles2D
		{
			Amount = 18,
			Lifetime = 0.8f,
			Texture = soft,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			Direction = Vector2.Up,
			Spread = 80f,
			Gravity = new Vector2(0f, 20f),
			InitialVelocityMin = 30f,
			InitialVelocityMax = 70f,
			ScaleAmountMin = 0.25f,
			ScaleAmountMax = 0.45f,
			ScaleAmountCurve = shrink,
			Color = _palette[1] with { A = 0.35f },
			LocalCoords = false,
			Emitting = Mathf.IsZeroApprox(_travel.Y),
		};
		AddChild(_dust);

		_area = new Area2D { CollisionLayer = 0, CollisionMask = TargetMask, Monitorable = false };
		_hitShape = new CollisionShape2D { Shape = new RectangleShape2D() };
		_area.AddChild(_hitShape);
		AddChild(_area);

		ApplySize();
		UpdateRings();
	}

	private static Line2D NewLine(Gradient gradient) => new()
	{
		JointMode = Line2D.LineJointMode.Round,
		BeginCapMode = Line2D.LineCapMode.Round,
		EndCapMode = Line2D.LineCapMode.Round,
		Gradient = gradient,
		DefaultColor = Colors.White,
	};

	// Faint at both ends of the arc, strongest in the middle (the front of the ring).
	private static Gradient Ends(Color c)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, c with { A = 0f });
		gradient.SetColor(1, c with { A = 0f });
		gradient.AddPoint(0.5f, c);
		return gradient;
	}

	// Solid where it leaves the ring, gone at its far tip.
	private static Gradient Tail(Color c)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, c);
		gradient.SetColor(1, c with { A = 0f });
		return gradient;
	}

	// Sizes the hit zone and the particles to the (scaled) column.
	private void ApplySize()
	{
		float s = SizeScale;
		float height = Height * s;
		((RectangleShape2D)_hitShape.Shape).Size = new Vector2(HitHalfWidth * 2f * s, height);
		_hitShape.Position = new Vector2(0f, -height / 2f);
		_debris.EmissionRectExtents = new Vector2(Mathf.Max(TipRadius * s, 4f), 4f);
		_debris.ScaleAmountMin = 0.05f * Mathf.Max(s, 0.5f);
		_debris.ScaleAmountMax = 0.1f * Mathf.Max(s, 0.5f);
		_dust.EmissionRectExtents = new Vector2(Mathf.Max(TipRadius * s, 6f) * 1.5f + 6f, 2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;

		// Grow in, hold, fade out.
		_presence = _time < GrowTime ? _time / GrowTime
			: _time > Lifetime - FadeTime ? Mathf.Max(0f, (Lifetime - _time) / FadeTime)
			: 1f;
		Modulate = new Color(1f, 1f, 1f, _time > Lifetime - FadeTime ? _presence : 1f);
		if (_time >= Lifetime)
		{
			QueueFree();
			return;
		}

		Position += _travel * Speed * dt;
		UpdateRings();

		_tickTimer -= dt;
		if (_tickTimer <= 0f)
		{
			_tickTimer = TickInterval;
			DamageEnemies();
		}
	}

	private static float Hash(float a) => Mathf.PosMod(Mathf.Sin(a * 127.1f) * 43758.5453f, 1f);

	private void UpdateRings()
	{
		float s = SizeScale;
		float height = Height * s;
		float tip = TipRadius * Mathf.Max(s, 0.6f);
		float top = TopRadius * s;
		float sway = SwayAmplitude * s;
		// Grows bottom-up while appearing.
		float grown = _time < GrowTime ? _time / GrowTime : 1f;
		float arc = Mathf.DegToRad(RingArcDegrees);
		int count = _rings.Count;

		for (int k = 0; k < count; k++)
		{
			Ring ring = _rings[k];
			// Each ring climbs the column and wraps back to the point; `generation` re-rolls its
			// random look every lap so the column never visibly repeats.
			float cycle = (k + _time * RiseSpeed) / count;
			float u = Mathf.PosMod(cycle, 1f);
			float seed = k * 31f + Mathf.Floor(cycle) * 7f;
			float fade = Mathf.SmoothStep(0f, 0.08f, u) * (1f - Mathf.SmoothStep(0.82f, 1f, u));
			if (u > grown)
				fade = 0f;

			float y = -height * Mathf.Pow(u, 0.85f);
			float rx = Mathf.Lerp(tip, top, Mathf.Pow(u, ConePower));
			float ry = Mathf.Max(1.5f, rx * Flatten);
			float x0 = sway * u * Mathf.Sin(_time * 2.3f + u * 4f);
			float width = Mathf.Lerp(ThicknessBottom, ThicknessTop, u) * Mathf.Max(s, 0.5f);
			// Lower rings spin faster, like a real funnel.
			float a0 = Hash(seed) * Mathf.Tau + _time * SpinSpeed * (1.3f - 0.6f * u);

			Vector2[] points = ring.Body.Points;
			for (int p = 0; p < ArcPoints; p++)
			{
				float a = a0 + arc * p / (ArcPoints - 1);
				points[p] = new Vector2(x0 + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry);
			}

			ring.Body.Points = points;
			ring.Body.Width = width;
			ring.Body.SelfModulate = new Color(1f, 1f, 1f, 0.75f * fade);
			// The crest rides on the upper edge of the body, thinner and additive.
			ring.Crest.Points = points;
			ring.Crest.Width = width * 0.38f;
			ring.Crest.Position = new Vector2(0f, -width * 0.22f);
			ring.Crest.SelfModulate = new Color(1f, 1f, 1f, 0.95f * fade);

			// Streaks flung roughly horizontally off both ends of the arc.
			for (int e = 0; e < 2; e++)
			{
				Vector2 end = e == 0 ? points[0] : points[ArcPoints - 1];
				float side = Mathf.Sign(end.X - x0);
				if (side == 0f)
					side = 1f;
				float length = rx * (0.25f + 0.6f * Hash(seed + 3f + e));
				Line2D streak = ring.Streaks[e];
				streak.Points = new[] { end, end + new Vector2(side * length, -2f - 4f * Hash(seed + e)) };
				streak.Width = (1f + 1.8f * Hash(seed + 9f + e)) * Mathf.Max(s, 0.6f);
				streak.SelfModulate = new Color(1f, 1f, 1f, 0.8f * fade);
			}
		}
	}

	private void DamageEnemies()
	{
		bool grew = false;
		foreach (Node2D body in _area.GetOverlappingBodies())
		{
			if (body is not Enemy enemy || enemy.Stats is null)
				continue;
			enemy.Stats.TakeDamage(AttackPower, element: _element, ignoreInvulnerability: true, armInvulnerability: false);
			if (_growing && _touched.Add(enemy.GetInstanceId()) && SizeScale < MaxSizeScale)
			{
				SizeScale = Mathf.Min(MaxSizeScale, SizeScale + GrowPerEnemy);
				grew = true;
			}
		}
		if (grew)
		{
			// Damage follows size: a tornado that has grown back to full size hits like a normal one.
			AttackPower = Mathf.Max(1, Mathf.RoundToInt(_baseAttackPower * SizeScale));
			ApplySize();
		}
	}
}
