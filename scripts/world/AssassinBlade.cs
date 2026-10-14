using System;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandido6's thrown blades (the AssassinBandit pack's own Blades_* art), built entirely in code so
// the thrower can tune each throw. Three flight modes:
//   Straight  — flies in a line; sticks in a wall (harmless, fades) or in the floor, where it
//               stays as a caltrop for CaltropDuration and nicks the player if they stand on it.
//   Lob       — same, but on a gravity arc (the blade rain); lands as a caltrop.
//   Boomerang — slows to a stop OutDistance away, then flies back to the thrower (who catches
//               it). Passes through terrain on the way back. Hits on the way out AND back, and a
//               hit from behind ignores the player's block — the answer to turtling.
// Any blocked hit plays the Blades_HitShield deflect. Leaves a fading crimson neon streak.
public partial class AssassinBlade : Area2D
{
	public enum Mode { Straight, Lob, Boomerang }

	public Mode FlightMode = Mode.Straight;
	public Vector2 Velocity;
	public float Gravity = 900f;
	public float Lifetime = 3f;
	public float DamageMultiplier = 1f;
	public float KnockbackForce = 150f;
	public float OutDistance = 260f;
	public float ReturnSpeed = 520f;
	public float CaltropDuration = 2f;
	public float CaltropDamageMultiplier = 0.4f;
	public float CaltropTick = 0.6f;
	public Node2D Thrower;
	public Stats ThrowerStats;
	public Color TrailColor = new(1f, 0.08f, 0.2f, 1f);

	// true = hit the player, false = expired/stuck without hitting. Raised once.
	public event Action<bool> Resolved;

	private const string FramesPath = "res://resources/sprites/AssassinBladeSpriteFrames.tres";
	private const float SpriteScale = 1.25f;

	private AnimatedSprite2D _sprite;
	private Line2D _trail;
	private readonly System.Collections.Generic.List<Vector2> _trailPoints = new();
	private float _age;
	private bool _resolved;
	private bool _stuck;
	private bool _caltrop;
	private float _caltropTimer;
	private float _caltropTickTimer;
	private Sprite2D _caltropGlow;
	private bool _returning;
	private float _outTravelled;
	private float _outSpeed;
	private Vector2 _outDirection;
	private float _hitCooldown;
	private bool _reflected;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 7f } });

		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Scale = Vector2.One * SpriteScale,
			FlipH = Velocity.X < 0f,
		};
		AddChild(_sprite);
		_sprite.Play("fly");

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.1f));
		taper.AddPoint(new Vector2(1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 4f,
			WidthCurve = taper,
			Gradient = fade,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			ZIndex = -1,
		};
		AddChild(_trail);

		if (FlightMode == Mode.Boomerang)
		{
			_outSpeed = Velocity.Length();
			_outDirection = Velocity.Normalized();
		}
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_hitCooldown -= dt;
		if (_caltrop)
		{
			UpdateCaltrop(dt);
			return;
		}
		if (_stuck)
			return;

		_age += dt;
		if (_age >= Lifetime)
		{
			Fade(0.2f);
			return;
		}

		Vector2 from = GlobalPosition;
		switch (FlightMode)
		{
			case Mode.Lob:
				Velocity.Y += Gravity * dt;
				break;
			case Mode.Boomerang:
				UpdateBoomerang(dt);
				break;
		}
		Vector2 to = from + Velocity * dt;

		// Terrain (not on a boomerang's way back): floor → caltrop, wall → stuck.
		if (!(FlightMode == Mode.Boomerang && _returning) && TryHitTerrain(from, to))
			return;

		GlobalPosition = to;
		if (FlightMode != Mode.Boomerang)
			_sprite.FlipH = Velocity.X < 0f;
		RecordTrail();
	}

	private void UpdateBoomerang(float dt)
	{
		if (!_returning)
		{
			// Bleeds speed linearly to a stop over OutDistance, then turns back.
			float t = Mathf.Clamp(_outTravelled / Mathf.Max(1f, OutDistance), 0f, 1f);
			float speed = Mathf.Lerp(_outSpeed, 40f, t);
			Velocity = _outDirection * speed;
			_outTravelled += speed * dt;
			if (t >= 1f)
				_returning = true;
			return;
		}

		if (!IsInstanceValid(Thrower) || !Thrower.IsInsideTree())
			return; // keeps its last velocity until Lifetime ends
		Vector2 target = Thrower.GlobalPosition + new Vector2(0f, -14f);
		Vector2 toThrower = target - GlobalPosition;
		if (toThrower.Length() < 22f)
		{
			// Caught.
			Finish(false, fade: 0.05f);
			return;
		}
		Velocity = Velocity.MoveToward(toThrower.Normalized() * ReturnSpeed, ReturnSpeed * 3f * dt);
	}

	private bool TryHitTerrain(Vector2 from, Vector2 to)
	{
		uint mask = 1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms;
		var query = PhysicsRayQueryParameters2D.Create(from, to, mask);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;

		Vector2 point = (Vector2)hit["position"];
		Vector2 normal = (Vector2)hit["normal"];
		bool oneWay = hit["collider"].AsGodotObject() is CollisionObject2D body
			&& (body.CollisionLayer & PhysicsLayers.OneWayPlatforms) != 0
			&& (body.CollisionLayer & 1u) == 0;
		bool floor = normal.Y < -0.5f;
		if (oneWay && !floor)
			return false; // one-way platforms only catch things coming down onto them

		if (_reflected)
		{
			// A returned blade doesn't leave a caltrop the player could step on: it just breaks.
			_stuck = true;
			GlobalPosition = point;
			Finish(false, fade: 0.2f);
			return true;
		}

		if (FlightMode == Mode.Boomerang)
		{
			// A wall on the way out just turns it back early.
			_returning = true;
			return false;
		}

		GlobalPosition = point;
		if (floor)
			BecomeCaltrop();
		else
			StickInWall(normal);
		return true;
	}

	private void StickInWall(Vector2 normal)
	{
		_stuck = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		_sprite.FlipH = normal.X > 0f;
		_sprite.Play("hit_wall");
		Finish(false, fade: 0f);
		DelayedFade(1.2f);
	}

	private void BecomeCaltrop()
	{
		_stuck = true;
		_caltrop = true;
		_caltropTimer = CaltropDuration;
		_caltropTickTimer = 0f;
		// The stuck-blades frames sit low in their 64px cell (rows 40-47): lift them onto the floor.
		_sprite.Position = new Vector2(0f, -(47f - 32f) * SpriteScale);
		_sprite.Play("hit_ground");
		// A pulsing crimson glow so the hazard reads at a glance.
		_caltropGlow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailColor with { A = 0.5f },
			Scale = new Vector2(0.55f, 0.22f),
			Position = new Vector2(0f, -3f),
			ShowBehindParent = true,
		};
		AddChild(_caltropGlow);
		Tween pulse = _caltropGlow.CreateTween().SetLoops();
		pulse.TweenProperty(_caltropGlow, "modulate:a", 0.15f, 0.35f);
		pulse.TweenProperty(_caltropGlow, "modulate:a", 0.55f, 0.35f);
		// The hitbox moves down to the stuck blades.
		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				shape.Shape = new RectangleShape2D { Size = new Vector2(22f, 10f) };
				shape.Position = new Vector2(0f, -5f);
			}
		}
		Finish(false, fade: 0f);
	}

	private void UpdateCaltrop(float dt)
	{
		_caltropTimer -= dt;
		_caltropTickTimer -= dt;
		if (_caltropTimer <= 0f)
		{
			_caltrop = false;
			Fade(0.3f);
			return;
		}
		if (_caltropTickTimer > 0f)
			return;
		foreach (Node2D body in GetOverlappingBodies())
		{
			if (!body.IsInGroup("player") || body.GetNodeOrNull<Stats>("Stats") is not { IsInvulnerable: false } stats)
				continue;
			int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * CaltropDamageMultiplier));
			stats.TakeDamage(damage, isProjectile: true);
			_caltropTickTimer = CaltropTick;
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_stuck || _hitCooldown > 0f)
			return;
		Vector2 direction = Velocity.LengthSquared() > 1f ? Velocity.Normalized() : Vector2.Right;
		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		if (_reflected)
		{
			if (ProjectileReflect.TryHitEnemy(body, damage, direction, KnockbackForce))
			{
				ImpactEffect.SpawnAt(this, GlobalPosition);
				Finish(false, fade: 0.05f);
			}
			return;
		}
		if (!body.IsInGroup("player"))
			return;

		var player = body as Metroidvania.Player.Player;
		// Coming from behind the player: the block (omnidirectional in Player) doesn't save them.
		bool fromBehind = player is not null && (player.IsFacingRight ? direction.X > 0.2f : direction.X < -0.2f);
		if (!fromBehind && ProjectileReflect.TryParry(body))
		{
			// Mirror back (a lob retraces its arc; a boomerang stops homing on its thrower and just
			// flies straight back). A fresh Lifetime for the return trip.
			_reflected = true;
			Velocity = -Velocity;
			if (FlightMode == Mode.Boomerang)
			{
				// It may be parried near its turnaround, almost stopped: send it back at full speed.
				FlightMode = Mode.Straight;
				Velocity = Velocity.Normalized() * Mathf.Max(Velocity.Length(), ReturnSpeed);
			}
			_sprite.FlipH = Velocity.X < 0f;
			_age = 0f;
			ReportResolved(false);
			ProjectileReflect.Retarget(this);
			return;
		}

		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		bool blocked = player is not null && player.IsBlocking && !fromBehind;

		if (fromBehind && player is not null && player.IsBlocking)
		{
			Func<bool> interceptor = stats.IncomingHitInterceptor;
			stats.IncomingHitInterceptor = null;
			stats.TakeDamage(damage, isProjectile: true);
			stats.IncomingHitInterceptor = interceptor;
		}
		else
		{
			stats.TakeDamage(damage, isProjectile: true);
		}

		if (blocked)
		{
			SpawnDeflect();
			Finish(false, fade: 0.05f);
			return;
		}

		if (body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", direction, KnockbackForce);
		ImpactEffect.SpawnAt(this, GlobalPosition);
		if (FlightMode == Mode.Boomerang)
		{
			// Keeps flying (can still connect on the way back), just not twice in a row.
			_hitCooldown = 0.35f;
			ReportResolved(true);
			return;
		}
		Finish(true, fade: 0.05f);
	}

	private void SpawnDeflect()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var deflect = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Scale = Vector2.One * SpriteScale,
			FlipH = Velocity.X < 0f,
		};
		scene.AddChild(deflect);
		deflect.GlobalPosition = GlobalPosition;
		deflect.AnimationFinished += deflect.QueueFree;
		deflect.Play("hit_shield");
	}

	private void ReportResolved(bool hit)
	{
		if (_resolved)
			return;
		_resolved = true;
		Resolved?.Invoke(hit);
	}

	// Stops being a threat (except as a caltrop), hands the streak off to fade on its own.
	private void Finish(bool hit, float fade)
	{
		ReportResolved(hit);
		DetachTrail();
		if (fade > 0f)
			Fade(fade);
	}

	private void Fade(float duration)
	{
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		DetachTrail();
		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, duration);
		tween.TweenCallback(Callable.From(QueueFree));
	}

	private async void DelayedFade(float delay)
	{
		await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			Fade(0.3f);
	}

	private void RecordTrail()
	{
		if (_trail is null)
			return;
		Vector2 p = GlobalPosition;
		if (_trailPoints.Count > 0 && _trailPoints[^1].DistanceSquaredTo(p) < 36f)
			_trailPoints[^1] = p;
		else
		{
			_trailPoints.Add(p);
			if (_trailPoints.Count > 14)
				_trailPoints.RemoveAt(0);
		}
		_trail.Points = _trailPoints.ToArray();
	}

	private void DetachTrail()
	{
		if (_trail is null)
			return;
		Vector2[] points = _trail.Points;
		Line2D trail = _trail;
		_trail = null;
		if (points.Length < 2 || GetTree().CurrentScene is not Node scene)
		{
			trail.QueueFree();
			return;
		}
		RemoveChild(trail);
		trail.TopLevel = false;
		scene.AddChild(trail);
		trail.Points = points;
		Tween fade = trail.CreateTween();
		fade.TweenProperty(trail, "modulate:a", 0f, 0.3f);
		fade.TweenCallback(Callable.From(trail.QueueFree));
	}
}
