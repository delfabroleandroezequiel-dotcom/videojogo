using System;
using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// RangerArcher's arrows: the elf archer's arrow sprite (Arrow1.png) over a teal neon glow and streak.
// Like ElfArrow they only see the player — they fly through terrain and stop on a hit or when Lifetime runs out. Modes:
//   Straight — a plain fast arrow.
//   Homing   — slower, steers toward the player with a capped turn rate for HomingDuration, then
//              flies on straight. A dash shakes it off: while the player dashes (and LostTime
//              after) it stops steering. Glowing tip + sparks so it reads as "the one that follows".
//   Piercing — the charged sniper shot: very fast, thicker white-hot streak, passes through the
//              player (hits once) instead of stopping.
public partial class RangerArrow : Area2D
{
	public enum Mode { Straight, Homing, Piercing }

	public Mode FlightMode = Mode.Straight;
	public Vector2 Direction = Vector2.Right;
	public float Speed = 440f;
	public float Lifetime = 2.2f;
	public float DamageMultiplier = 1f;
	public float KnockbackForce = 160f;
	public float HomingDuration = 2.2f;
	public float TurnRateDegrees = 150f;
	public float LostTime = 0.4f;
	public Stats ShooterStats;
	public Color GlowColor = new(0.2f, 1f, 0.85f, 1f);

	// true = hit the player, false = expired without hitting. Raised once.
	public event Action<bool> Resolved;

	private const string ArrowTexturePath = "res://assets/external/CharacterPacks/Elves_Pack/Elf_Archer/Arrow/Arrow1.png";

	private Line2D _trail;
	private readonly List<Vector2> _trailPoints = new();
	private GpuParticles2D _sparks;
	private float _age;
	private float _lostTimer;
	private bool _hitOnce;
	private bool _done;
	private bool _reflected;
	private int _reflectedDamage;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		AddChild(new CollisionShape2D { Position = new Vector2(4f, 0f), Shape = new RectangleShape2D { Size = new Vector2(22f, 6f) } });

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		bool piercing = FlightMode == Mode.Piercing;

		// The arrow itself: the same sprite as ElfArrow (points along +X; the node rotates to the
		// flight direction), over a soft neon glow along the shaft.
		AddChild(new Line2D
		{
			Points = new[] { new Vector2(-15f, 0f), new Vector2(10f, 0f) },
			Width = piercing ? 6f : 5f,
			DefaultColor = GlowColor with { A = piercing ? 0.55f : 0.35f },
			Material = additive,
		});
		AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>(ArrowTexturePath),
			Scale = Vector2.One * 1.4f,
		});

		if (FlightMode == Mode.Homing)
		{
			// A pulsing glow at the tip and a stream of sparks — "this one is coming for you".
			var tip = new Sprite2D
			{
				Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
				Material = additive,
				Modulate = GlowColor,
				Position = new Vector2(12f, 0f),
				Scale = Vector2.One * 0.35f,
			};
			AddChild(tip);
			Tween pulse = tip.CreateTween().SetLoops();
			pulse.TweenProperty(tip, "scale", Vector2.One * 0.5f, 0.15f);
			pulse.TweenProperty(tip, "scale", Vector2.One * 0.3f, 0.15f);

			_sparks = new GpuParticles2D
			{
				Amount = 16,
				Lifetime = 0.35,
				LocalCoords = false,
				Material = additive,
				Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
				Modulate = GlowColor,
				ProcessMaterial = new ParticleProcessMaterial
				{
					Direction = new Vector3(-1f, 0f, 0f),
					Spread = 35f,
					InitialVelocityMin = 20f,
					InitialVelocityMax = 60f,
					Gravity = Vector3.Zero,
					ScaleMin = 0.04f,
					ScaleMax = 0.09f,
				},
				Position = new Vector2(-12f, 0f),
			};
			AddChild(_sparks);
		}

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.1f));
		taper.AddPoint(new Vector2(1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = piercing ? 6f : 3.5f,
			WidthCurve = taper,
			Gradient = fade,
			Modulate = piercing ? GlowColor.Lightened(0.5f) : GlowColor,
			Material = additive,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			ZIndex = -1,
		};
		AddChild(_trail);

		Direction = Direction.Normalized();
		Rotation = Direction.Angle();
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_age += dt;

		if (FlightMode == Mode.Homing && _age < HomingDuration)
			Steer(dt);

		GlobalPosition += Direction * Speed * dt;
		Rotation = Direction.Angle();
		RecordTrail();

		if (_age >= Lifetime)
			Finish(hit: _hitOnce);
	}

	private void Steer(float dt)
	{
		var player = PlayerReads.Find(this);
		if (player is null)
			return;
		if (player.IsDashing)
			_lostTimer = LostTime;
		_lostTimer -= dt;
		if (_lostTimer > 0f)
			return;

		Vector2 toPlayer = (player.GlobalPosition + new Vector2(0f, -8f) - GlobalPosition).Normalized();
		float angle = Direction.AngleTo(toPlayer);
		float maxTurn = Mathf.DegToRad(TurnRateDegrees) * dt;
		Direction = Direction.Rotated(Mathf.Clamp(angle, -maxTurn, maxTurn));
	}

	private void RecordTrail()
	{
		int maxPoints = FlightMode == Mode.Piercing ? 22 : 16;
		Vector2 p = GlobalPosition;
		if (_trailPoints.Count > 0 && _trailPoints[^1].DistanceSquaredTo(p) < 36f)
			_trailPoints[^1] = p;
		else
		{
			_trailPoints.Add(p);
			if (_trailPoints.Count > maxPoints)
				_trailPoints.RemoveAt(0);
		}
		_trail.Points = _trailPoints.ToArray();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done)
			return;
		if (_reflected)
		{
			if (ProjectileReflect.TryHitEnemy(body, _reflectedDamage, Direction, KnockbackForce))
			{
				ImpactEffect.SpawnAt(this, GlobalPosition);
				// The piercing shot keeps flying through on the way back too.
				if (FlightMode != Mode.Piercing)
					Finish(hit: false);
			}
			return;
		}
		if (_hitOnce || !body.IsInGroup("player"))
			return;

		int damage = Mathf.Max(1, Mathf.RoundToInt((ShooterStats?.AttackPower ?? 10) * DamageMultiplier));
		if (ProjectileReflect.TryParry(body))
		{
			// Mirror back with the damage it would have dealt; a homing arrow stops steering.
			_reflected = true;
			_reflectedDamage = damage;
			if (FlightMode == Mode.Homing)
				FlightMode = Mode.Straight;
			Direction = -Direction;
			Rotation = Direction.Angle();
			_age = 0f;
			Resolved?.Invoke(false);
			Resolved = null;
			ProjectileReflect.Retarget(this);
			return;
		}

		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;

		stats.TakeDamage(damage, isProjectile: true);
		if (body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", Direction, KnockbackForce);
		ImpactEffect.SpawnAt(this, GlobalPosition);
		_hitOnce = true;

		// The piercing shot keeps flying through.
		if (FlightMode != Mode.Piercing)
			Finish(hit: true);
	}

	private void Finish(bool hit)
	{
		if (_done)
			return;
		_done = true;
		Resolved?.Invoke(hit);
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		if (_sparks is not null)
			_sparks.Emitting = false;

		// Hand the streak to the scene so it fades after the arrow is gone.
		Vector2[] points = _trail.Points;
		if (points.Length > 1 && GetTree().CurrentScene is { } scene)
		{
			RemoveChild(_trail);
			_trail.TopLevel = false;
			scene.AddChild(_trail);
			_trail.Points = points;
			Line2D trail = _trail;
			Tween fade = trail.CreateTween();
			fade.TweenProperty(trail, "modulate:a", 0f, 0.35f);
			fade.TweenCallback(Callable.From(trail.QueueFree));
		}
		QueueFree();
	}
}
