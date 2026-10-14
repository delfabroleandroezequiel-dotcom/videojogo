using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Necromancer's bolt: a slow necrotic orb that bends toward the player (limited turn rate, so a
// dash or a jump at the right time shakes it), dies on walls, and poisons on a hit that isn't
// blocked (same ApplyPoison contract as ElfDagger / PoisonSpit). Drawn in code: a glowing core,
// a soft halo, a fading streak and a small light.
public partial class NecroBolt : Node2D
{
	public Vector2 Direction = Vector2.Right;
	public Stats ThrowerStats;
	public float DamageMultiplier = 1f;
	public float Speed = 170f;
	public float TurnRateDegrees = 110f;
	public float Lifetime = 3.2f;
	public Color GlowColor = new(0.55f, 1f, 0.3f, 1f);
	public int PoisonTickDamage = 2;
	public float PoisonTickInterval = 0.5f;
	public float PoisonDuration = 2.5f;

	private const float HitRadius = 12f;

	private Line2D _trail;
	private readonly List<Vector2> _points = new();
	private float _age;
	private bool _done;
	private bool _reflected;
	// Who a returned bolt hunts: the thrower, or the nearest enemy once the thrower is gone.
	private Node2D _reflectTarget;
	private const float ReflectSearchRadius = 400f;

	public override void _Ready()
	{
		Direction = Direction.Normalized();
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 8f,
			Gradient = fade,
			Modulate = GlowColor,
			Material = additive,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
		AddChild(_trail);
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = GlowColor, Scale = Vector2.One * 0.45f });
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = new Color(0.9f, 1f, 0.8f, 1f), Scale = Vector2.One * 0.16f });
		AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GlowColor,
			Energy = 0.8f,
			TextureScale = 0.3f,
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_age += dt;
		if (_age >= Lifetime)
		{
			End(false);
			return;
		}

		if (_reflected)
		{
			if (!IsInstanceValid(_reflectTarget) || !_reflectTarget.IsInsideTree()
				|| _reflectTarget.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 })
				_reflectTarget = ProjectileReflect.FindEnemyNear(this, ReflectSearchRadius);
			if (_reflectTarget is not null)
				SteerToward(_reflectTarget.GlobalPosition + new Vector2(0f, -6f), dt);
			if (ProjectileReflect.FindEnemyNear(this, HitRadius + 14f) is { } enemy)
			{
				HitEnemy(enemy);
				return;
			}
		}
		// Bend toward the player, limited by the turn rate.
		else if (PlayerReads.Find(this) is { } player)
		{
			SteerToward(player.GlobalPosition + new Vector2(0f, -6f), dt);

			if (GlobalPosition.DistanceTo(player.GlobalPosition + new Vector2(0f, -6f)) < HitRadius + 14f)
			{
				if (ProjectileReflect.TryParry(player))
				{
					// Mirror back and hunt the necromancer that cast it.
					_reflected = true;
					_reflectTarget = ThrowerStats?.GetParent() as Node2D;
					Direction = -Direction;
					_age = 0f;
					Modulate = ProjectileReflect.ReflectedModulate;
					return;
				}
				HitPlayer(player);
				return;
			}
		}

		Vector2 next = GlobalPosition + Direction * Speed * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 1u);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0)
		{
			End(true);
			return;
		}
		GlobalPosition = next;
		_points.Add(next);
		if (_points.Count > 14)
			_points.RemoveAt(0);
		_trail.Points = _points.ToArray();
	}

	private void SteerToward(Vector2 target, float dt)
	{
		Vector2 want = (target - GlobalPosition).Normalized();
		float angle = Direction.AngleTo(want);
		float maxTurn = Mathf.DegToRad(TurnRateDegrees) * dt;
		Direction = Direction.Rotated(Mathf.Clamp(angle, -maxTurn, maxTurn));
	}

	private void HitEnemy(Node2D enemy)
	{
		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		if (ProjectileReflect.TryHitEnemy(enemy, damage, Direction, 0f, DamageElement.Poison)
			&& PoisonDuration > 0f && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", PoisonTickDamage, PoisonTickInterval, PoisonDuration);
		End(true);
	}

	private void HitPlayer(Metroidvania.Player.Player player)
	{
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is not null && !stats.IsInvulnerable)
		{
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier)),
				isProjectile: true, element: DamageElement.Poison);
			// A blocked bolt doesn't poison (and PoisonDuration 0 means a bolt that never does).
			if (stats.CurrentHealth < before && PoisonDuration > 0f)
				player.ApplyPoison(PoisonTickDamage, PoisonTickInterval, PoisonDuration);
		}
		End(true);
	}

	private void End(bool impact)
	{
		_done = true;
		if (impact)
			ImpactEffect.SpawnAt(this, GlobalPosition);
		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, 0.15f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
