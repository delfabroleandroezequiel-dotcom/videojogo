using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// ElfSwordsman's heavy cleave sends this forward along the floor: a standing crescent of elf-green
// neon (glow + white-hot core, drawn in code) that slides away from him, shedding sparks. Only sees
// the player; stops at a wall or after Range, fading out. Hits once.
public partial class ElfSwordWave : Area2D
{
	public float Direction = 1f;
	public float Speed = 380f;
	public float Range = 320f;
	public float DamageMultiplier = 0.7f;
	public float KnockbackForce = 200f;
	public Stats ThrowerStats;
	public Color GlowColor = new(0.35f, 1f, 0.45f, 1f);

	private const float Height = 46f;
	private float _travelled;
	private bool _done;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		AddChild(new CollisionShape2D { Position = new Vector2(0f, -Height * 0.5f), Shape = new RectangleShape2D { Size = new Vector2(22f, Height) } });

		// The crescent: an arc bulging forward, from the floor up to Height.
		const int segments = 14;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Lerp(-Mathf.Pi * 0.42f, Mathf.Pi * 0.42f, i / (float)segments);
			points[i] = new Vector2(Mathf.Cos(a) * 16f - 8f, Mathf.Sin(a) * Height * 0.5f - Height * 0.5f);
		}
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.15f));
		taper.AddPoint(new Vector2(0.5f, 1f));
		taper.AddPoint(new Vector2(1f, 0.15f));
		var visual = new Node2D { Scale = new Vector2(Direction, 1f) };
		visual.AddChild(new Line2D { Points = points, Width = 12f, WidthCurve = taper, DefaultColor = GlowColor with { A = 0.7f }, Material = additive });
		visual.AddChild(new Line2D { Points = points, Width = 3.5f, WidthCurve = taper, DefaultColor = new Color(0.9f, 1f, 0.92f, 1f), Material = additive });
		AddChild(visual);

		AddChild(new GpuParticles2D
		{
			Amount = 18,
			Lifetime = 0.4,
			LocalCoords = false,
			Material = additive,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Modulate = GlowColor,
			Position = new Vector2(0f, -Height * 0.5f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(-Direction, -0.3f, 0f),
				Spread = 30f,
				InitialVelocityMin = 30f,
				InitialVelocityMax = 90f,
				Gravity = new Vector3(0f, 60f, 0f),
				ScaleMin = 0.05f,
				ScaleMax = 0.1f,
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(4f, Height * 0.45f, 0f),
			},
		});
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float step = Speed * (float)delta;
		Vector2 from = GlobalPosition + new Vector2(0f, -Height * 0.5f);
		var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(Direction * step, 0f),
			1u | PhysicsLayers.ClimbableWalls);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0)
		{
			Fade();
			return;
		}
		GlobalPosition += new Vector2(Direction * step, 0f);
		_travelled += step;
		if (_travelled >= Range)
			Fade();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done || !body.IsInGroup("player"))
			return;
		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		stats.TakeDamage(damage, isProjectile: true);
		if (body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", new Vector2(Direction, -0.2f).Normalized(), KnockbackForce);
		ImpactEffect.SpawnAt(this, body.GlobalPosition);
		Fade();
	}

	private void Fade()
	{
		if (_done)
			return;
		_done = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, 0.18f);
		tween.Parallel().TweenProperty(this, "scale", new Vector2(1.3f, 0.6f), 0.18f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
