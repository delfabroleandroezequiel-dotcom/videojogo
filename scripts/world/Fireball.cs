using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Procedural fireball (no art): a white-hot core inside an orange glow, a flickering fire tail and
// embers, lit by its own PointLight2D (so it still reads in dark maps). Flies straight; on hitting an
// enemy (or a wall) it deals Fire damage to whatever is in BlastRadius and bursts into an Explosion.
// Caster sets direction/damage via Launch; mask/targets default to the player's use (world + enemies).
public partial class Fireball : Area2D
{
	[Export] public float Speed = 430f;
	[Export] public float Lifetime = 1.6f;
	[Export] public int Damage = 12;
	[Export] public float BlastRadius = 40f;
	[Export] public float CoreRadius = 7f;
	[Export] public float ExplosionScale = 0.55f;
	[Export] public Color GlowColor = new(1f, 0.45f, 0.1f, 1f);
	[Export] public Color CoreColor = new(1f, 0.95f, 0.75f, 1f);
	// World (1) + enemies (4).
	[Export(PropertyHint.Layers2DPhysics)] public uint TargetMask = 1u | 4u;

	private Vector2 _velocity;
	private Stats _casterStats;
	private float _age;
	private bool _exploded;
	private Sprite2D _glow;
	private Line2D _tail;
	private readonly System.Collections.Generic.List<Vector2> _tailPoints = new();

	public void Launch(Vector2 direction, int damage, Stats casterStats)
	{
		_velocity = direction.Normalized() * Speed;
		Damage = damage;
		_casterStats = casterStats;
	}

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = TargetMask;
		Monitorable = false;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = CoreRadius } });

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

		// Fire tail: glow ramp from deep red (old end) to orange (at the ball).
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(0.8f, 0.1f, 0.02f, 0f));
		ramp.SetColor(1, GlowColor);
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.1f));
		taper.AddPoint(new Vector2(1f, 1f));
		_tail = new Line2D
		{
			Width = CoreRadius * 2.6f,
			WidthCurve = taper,
			Gradient = ramp,
			Material = additive,
			TopLevel = true,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
		AddChild(_tail);

		_glow = new Sprite2D { Texture = soft, Material = additive, Modulate = GlowColor, Scale = Vector2.One * CoreRadius / 9f };
		AddChild(_glow);
		AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = CoreColor, Scale = Vector2.One * CoreRadius / 20f });

		AddChild(new GpuParticles2D
		{
			Amount = 24,
			Lifetime = 0.45f,
			ProcessMaterial = GD.Load<ParticleProcessMaterial>("res://resources/particles/FlameEmberProcess.tres"),
			Texture = soft,
			Material = additive,
			LocalCoords = false,
			Scale = Vector2.One * 0.6f,
		});

		AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GlowColor,
			Energy = 1.1f,
			TextureScale = 0.45f,
		});

		BodyEntered += OnBodyEntered;
		Sfx.PlayAt(this, "Magic", "Fireball");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_exploded)
			return;
		float dt = (float)delta;
		_age += dt;
		if (_age >= Lifetime)
		{
			Explode();
			return;
		}

		GlobalPosition += _velocity * dt;
		// Flicker.
		_glow.Scale = Vector2.One * (CoreRadius / 9f) * (0.9f + 0.2f * Mathf.Sin(_age * 40f));

		_tailPoints.Add(GlobalPosition);
		if (_tailPoints.Count > 9)
			_tailPoints.RemoveAt(0);
		_tail.Points = _tailPoints.ToArray();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_exploded)
			return;
		if (body.GetNodeOrNull<Stats>("Stats") is Stats stats && stats == _casterStats)
			return;
		if (body is StaticBody2D staticBody && (staticBody.CollisionLayer & PhysicsLayers.OneWayPlatforms) != 0)
			return;
		CallDeferred(MethodName.Explode);
	}

	private void Explode()
	{
		if (_exploded || !IsInstanceValid(this))
			return;
		_exploded = true;

		// Blast: Fire damage to every enemy within BlastRadius (a direct hit is always inside it).
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new CircleShape2D { Radius = BlastRadius },
			Transform = new Transform2D(0f, GlobalPosition),
			CollisionMask = TargetMask & ~1u,
			CollideWithAreas = false,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 16))
		{
			if (hit["collider"].AsGodotObject() is Node2D target && target.GetNodeOrNull<Stats>("Stats") is Stats stats
				&& stats != _casterStats)
				stats.TakeDamage(Damage, isProjectile: true, element: DamageElement.Fire, ignoreInvulnerability: true);
		}

		if (GD.Load<PackedScene>("res://scenes/world/Explosion.tscn").Instantiate() is Node2D explosion)
		{
			if (explosion is Explosion boom)
				boom.TargetScale = ExplosionScale;
			GetTree().CurrentScene.AddChild(explosion);
			explosion.GlobalPosition = GlobalPosition;
		}
		QueueFree();
	}
}
