using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// ViejoProta's spell: a ball of fire built in code (white-hot core, orange glow, ember stream and a
// light) flying straight at the player. Bursts on the player or on terrain; on the floor it leaves
// a GroundFire patch if one is given.
public partial class ProtaFireball : Area2D
{
	public Vector2 Direction = Vector2.Right;
	public float Speed = 330f;
	public float Lifetime = 2.2f;
	public float DamageMultiplier = 0.8f;
	public float KnockbackForce = 180f;
	public Stats ThrowerStats;
	public PackedScene GroundFireScene;

	private float _age;
	private bool _done;
	private bool _reflected;
	private Sprite2D _glow;
	private ParticleProcessMaterial _embers;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 8f } });
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

		_glow = new Sprite2D { Texture = dot, Material = additive, Modulate = new Color(1f, 0.45f, 0.1f, 1f), Scale = Vector2.One * 0.6f };
		AddChild(_glow);
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = new Color(1f, 0.95f, 0.75f, 1f), Scale = Vector2.One * 0.25f });
		Tween flicker = _glow.CreateTween().SetLoops();
		flicker.TweenProperty(_glow, "scale", Vector2.One * 0.7f, 0.07f);
		flicker.TweenProperty(_glow, "scale", Vector2.One * 0.55f, 0.07f);

		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.4f, 1f };
		ramp.Colors = new[] { new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.45f, 0.08f, 0.85f), new Color(0.5f, 0.05f, 0f, 0f) };
		_embers = new ParticleProcessMaterial
		{
			Direction = new Vector3(-Direction.X, -Direction.Y, 0f),
			Spread = 25f,
			InitialVelocityMin = 20f,
			InitialVelocityMax = 70f,
			Gravity = new Vector3(0f, -40f, 0f),
			ScaleMin = 0.08f,
			ScaleMax = 0.18f,
			ColorRamp = new GradientTexture1D { Gradient = ramp },
		};
		AddChild(new GpuParticles2D
		{
			Amount = 26,
			Lifetime = 0.35,
			LocalCoords = false,
			Material = additive,
			Texture = dot,
			ProcessMaterial = _embers,
		});
		AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = new Color(1f, 0.55f, 0.2f),
			Energy = 1f,
			TextureScale = 0.4f,
		});
		Direction = Direction.Normalized();
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		_age += (float)delta;
		Vector2 from = GlobalPosition;
		Vector2 to = from + Direction * Speed * (float)delta;
		var query = PhysicsRayQueryParameters2D.Create(from, to, 1u | PhysicsLayers.ClimbableWalls);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			GlobalPosition = (Vector2)hit["position"];
			Burst(((Vector2)hit["normal"]).Y < -0.5f);
			return;
		}
		GlobalPosition = to;
		if (_age >= Lifetime)
			Burst(false);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done)
			return;
		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		if (_reflected)
		{
			if (ProjectileReflect.TryHitEnemy(body, damage, Direction, KnockbackForce, DamageElement.Fire))
				Burst(false);
			return;
		}
		if (!body.IsInGroup("player"))
			return;
		if (ProjectileReflect.TryParry(body))
		{
			// Mirror back; a fresh Lifetime for the return trip.
			_reflected = true;
			Direction = -Direction;
			_embers.Direction = new Vector3(-Direction.X, -Direction.Y, 0f);
			_age = 0f;
			ProjectileReflect.Retarget(this);
			return;
		}
		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		stats.TakeDamage(damage, isProjectile: true, element: DamageElement.Fire);
		if (body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", Direction, KnockbackForce);
		Burst(false);
	}

	private void Burst(bool onFloor)
	{
		if (_done)
			return;
		_done = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		var palette = new GroundSlamFx.Palette
		{
			Hot = new Color(1f, 0.45f, 0.08f, 1f),
			Core = new Color(1f, 0.92f, 0.7f, 1f),
			Rock = new Color(0.25f, 0.18f, 0.15f, 1f),
			Dust = new Color(0.3f, 0.25f, 0.22f, 0.5f),
			Light = new Color(1f, 0.5f, 0.2f, 1f),
		};
		GroundSlamFx.Chip(this, GlobalPosition, 0.6f, palette);
		if (onFloor && GroundFireScene is not null && GetTree().CurrentScene is { } scene)
		{
			Vector2 at = GlobalPosition;
			PackedScene fireScene = GroundFireScene;
			Callable.From(() =>
			{
				var fire = fireScene.Instantiate<Node2D>();
				scene.AddChild(fire);
				fire.GlobalPosition = at;
			}).CallDeferred();
		}
		Tween tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector2.One * 1.8f, 0.12f);
		tween.Parallel().TweenProperty(this, "modulate:a", 0f, 0.15f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
