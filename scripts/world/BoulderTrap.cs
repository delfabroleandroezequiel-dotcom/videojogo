using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum BoulderTrigger
{
	// Rolls as soon as the scene starts and comes back RespawnDelay after every smash.
	Loop,
	// Waits off-screen until something calls Activate() (a PressurePlate, a lever...), then
	// rumbles, drops in at its placed position and rolls. Re-arms after each smash.
	Manual,
}

// Indiana-Jones rolling boulder, everything drawn in code (successor of the sprite-based
// RollingBoulder, which stays as-is for the maps already using it). Node origin = boulder centre.
// Rolls along the floor at Speed in Direction (ramping up from rest so it reads heavy), keeps its
// momentum off ledges and lands with a thud, and smashes to pieces against the first wall.
// Kills (or hurts) the player, crushes enemies in its way and lights barrels it rolls into.
// Look: a seeded, slightly lumpy rock silhouette whose surface detail (facets, strata, cracks,
// knobs) turns with the roll while the light (top-left highlight, bottom-right shadow) stays put —
// so it reads as a lit sphere rolling, not a sticker spinning. Dust and grit trail it, the camera
// rumbles as it nears, and before every (re)appearance dust and pebbles trickle from above.
// [Tool] so the rock previews in the editor.
[Tool]
public partial class BoulderTrap : CharacterBody2D
{
	[Signal] public delegate void ReleasedEventHandler();
	[Signal] public delegate void SmashedEventHandler();

	private float _radius = 54f;
	[Export(PropertyHint.Range, "16,160,1")]
	public float Radius { get => _radius; set { _radius = value; RebuildAll(); } }

	private int _seed = 3;
	[Export]
	public int Seed { get => _seed; set { _seed = value; RebuildAll(); } }

	private Color _rockColor = new(0.47f, 0.43f, 0.38f, 1f);
	private Color _shadowColor = new(0.2f, 0.18f, 0.17f, 1f);
	private Color _highlightColor = new(0.86f, 0.8f, 0.68f, 1f);
	[Export]
	public Color RockColor { get => _rockColor; set { _rockColor = value; RebuildAll(); } }
	[Export]
	public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; RebuildAll(); } }
	[Export]
	public Color HighlightColor { get => _highlightColor; set { _highlightColor = value; RebuildAll(); } }

	[ExportGroup("Motion")]
	[Export] public BoulderTrigger Trigger = BoulderTrigger.Loop;
	[Export] public int Direction = -1; // -1 = rolls left, 1 = rolls right
	[Export] public float Speed = 260f;
	// Seconds to reach Speed from rest.
	[Export] public float SpinUpTime = 0.45f;
	[Export] public float Gravity = 1400f;
	[Export] public float RespawnDelay = 1.2f;
	// Dust + pebbles trickling at the spawn point before it appears.
	[Export] public float WarningTime = 0.7f;
	[Export] public float MaxAirTime = 10f;

	[ExportGroup("Damage")]
	[Export] public bool InstantKill = true;
	[Export] public int Damage = 60;
	[Export] public float KnockbackForce = 500f;
	[Export] public int EnemyDamage = 120;
	// Camera rumble reaches the player within this distance.
	[Export] public float RumbleRange = 700f;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	private enum State { Waiting, Warning, Rolling, Gone }

	private State _state = State.Waiting;
	private Node2D _visual;       // rotates with the roll
	private Node2D _lighting;     // child of the rock, counter-rotated so the light stays fixed
	private Polygon2D _contactShadow;
	private CpuParticles2D _dust;
	private CpuParticles2D _grit;
	private CollisionShape2D _collision;
	private Hazard _hazard;
	private Area2D _crush;
	private Vector2[] _outline;
	private Vector2 _spawnPosition;
	private float _speedNow;
	private float _airTime;
	private float _rumbleTimer;
	private bool _wasOnFloor = true;
	private float _fallSpeedBeforeLanding;
	private readonly HashSet<ulong> _crushed = new();
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		RebuildAll();
		if (Engine.IsEditorHint())
			return;
		_spawnPosition = Position;
		CollisionLayer = 0;
		CollisionMask = 1;
		FloorSnapLength = 12f;
		if (Trigger == BoulderTrigger.Loop)
			Appear(false);
		else
			Stow(false);
	}

	// ── Public API ──────────────────────────────────────────────────────────────────────────────

	// Pressure plate / lever hook (Trigger = Manual): rumble, drop in, roll.
	public void Activate()
	{
		if (_state == State.Waiting)
			_ = WarnThenAppear();
	}

	// ── Lifecycle ───────────────────────────────────────────────────────────────────────────────

	private async System.Threading.Tasks.Task WarnThenAppear()
	{
		_state = State.Warning;
		if (WarningTime > 0f)
		{
			SpawnWarning();
			await ToSignal(GetTree().CreateTimer(WarningTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}
		Appear(true);
	}

	private void Appear(bool fadeIn)
	{
		Position = _spawnPosition;
		Velocity = Vector2.Zero;
		_speedNow = 0f;
		_airTime = 0f;
		_crushed.Clear();
		_visual.Rotation = 0f;
		Visible = true;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, true);
		_crush.SetDeferred(Area2D.PropertyName.Monitoring, true);
		_state = State.Rolling;
		EmitSignal(SignalName.Released);
		// Silent on the scene-start roll: the scene is still adding children (no audio node allowed yet).
		if (fadeIn)
			Sfx.PlayAt(this, "World/Mining", "mine");
		if (fadeIn)
		{
			Modulate = Modulate with { A = 0f };
			CreateTween().TweenProperty(this, "modulate:a", 1f, 0.2f);
		}
	}

	private void Stow(bool smashed)
	{
		_state = smashed ? State.Gone : State.Waiting;
		Visible = false;
		Velocity = Vector2.Zero;
		_dust.Emitting = false;
		_grit.Emitting = false;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_crush.SetDeferred(Area2D.PropertyName.Monitoring, false);
	}

	private async void Smash()
	{
		Vector2 contact = GlobalPosition + new Vector2(0f, _radius);
		Stow(true);
		EmitSignal(SignalName.Smashed);
		Sfx.PlayAt(this, "World/Mining", "mine");
		SpawnDustBurst(contact);
		SpawnChunks(contact);
		ShakeNearby(9f, 0.35f, RumbleRange * 1.4f);

		await ToSignal(GetTree().CreateTimer(RespawnDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		if (Trigger == BoulderTrigger.Loop)
			await WarnThenAppear();
		else
			_state = State.Waiting;
	}

	// ── Rolling ─────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _state != State.Rolling)
			return;
		float dt = (float)delta;
		bool onFloor = IsOnFloor();

		// Ramp up on the ground; in the air it keeps whatever speed it had (flies off ledges).
		if (onFloor)
			_speedNow = Mathf.MoveToward(_speedNow, Speed, Speed / Mathf.Max(SpinUpTime, 0.01f) * dt);
		Vector2 velocity = Velocity;
		velocity.X = Direction * _speedNow;
		velocity.Y = onFloor ? 0f : velocity.Y + Gravity * dt;
		if (!onFloor)
			_fallSpeedBeforeLanding = velocity.Y;
		Velocity = velocity;
		MoveAndSlide();

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			Vector2 normal = GetSlideCollision(i).GetNormal();
			if (Mathf.Abs(normal.X) > 0.8f && Mathf.Sign(normal.X) == -Direction)
			{
				Smash();
				return;
			}
		}

		onFloor = IsOnFloor();
		if (onFloor && !_wasOnFloor && _fallSpeedBeforeLanding > 250f)
			Land(_fallSpeedBeforeLanding);
		_wasOnFloor = onFloor;

		_airTime = onFloor ? 0f : _airTime + dt;
		if (_airTime >= MaxAirTime)
		{
			Smash();
			return;
		}

		// Rolling without slipping: the turn matches the distance covered.
		_visual.Rotation += Velocity.X * dt / _radius;

		_dust.Emitting = onFloor && _speedNow > 40f;
		_grit.Emitting = _dust.Emitting;
		_dust.Position = new Vector2(-Direction * _radius * 0.35f, _radius - 4f);
		_grit.Position = _dust.Position;
		_contactShadow.Modulate = _contactShadow.Modulate with { A = onFloor ? 1f : 0.35f };

		CrushEnemies();
		Rumble(dt);
	}

	public override void _Process(double delta)
	{
		// Light stays put while the rock turns underneath it.
		if (_lighting is not null && _visual is not null)
			_lighting.Rotation = -_visual.Rotation;
	}

	private void Land(float impactSpeed)
	{
		float strength = Mathf.Clamp(impactSpeed / 900f, 0.3f, 1f);
		Sfx.PlayAt(this, "World/Mining", "mine");
		ShakeNearby(6f * strength, 0.22f, RumbleRange);
		GroundSlamFx.Chip(this, GlobalPosition + new Vector2(0f, _radius), 0.6f * strength * _radius / 45f, new GroundSlamFx.Palette
		{
			Hot = _highlightColor.Darkened(0.3f),
			Core = _highlightColor,
			Rock = _rockColor,
			Dust = _rockColor.Lightened(0.15f) with { A = 0.6f },
			Light = new Color(0.5f, 0.45f, 0.4f),
		});
		// Squash on impact, spring back.
		Tween squash = CreateTween();
		_visual.Scale = new Vector2(1f + 0.1f * strength, 1f - 0.1f * strength);
		squash.TweenProperty(_visual, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	private void CrushEnemies()
	{
		// Barrels by distance: the crush area never reported a resting barrel (a StaticBody2D) as
		// overlapping, so don't rely on it for them.
		foreach (Node node in GetTree().GetNodesInGroup("explosive_barrel"))
		{
			if (node is ExplosiveBarrel barrel && !_crushed.Contains(barrel.GetInstanceId())
				&& (barrel.GlobalPosition + new Vector2(0f, -19f)).DistanceTo(GlobalPosition) < _radius + 16f)
			{
				_crushed.Add(barrel.GetInstanceId());
				barrel.IgniteAfter(0.05f);
			}
		}
		foreach (Node2D body in _crush.GetOverlappingBodies())
		{
			ulong id = body.GetInstanceId();
			if (_crushed.Contains(id))
				continue;
			if (body is ExplosiveBarrel barrel)
			{
				_crushed.Add(id);
				barrel.IgniteAfter(0.05f);
			}
			else if (body.IsInGroup("enemy"))
			{
				_crushed.Add(id);
				ProjectileReflect.TryHitEnemy(body, EnemyDamage, new Vector2(Direction, -0.6f).Normalized(), KnockbackForce);
			}
		}
	}

	private void Rumble(float dt)
	{
		_rumbleTimer -= dt;
		if (_rumbleTimer > 0f || !IsOnFloor())
			return;
		_rumbleTimer = 0.14f;
		if (GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player)
			return;
		float d = player.GlobalPosition.DistanceTo(GlobalPosition);
		if (d < RumbleRange)
			player.ShakeCamera(0.6f + 2.2f * (1f - d / RumbleRange), 0.16f);
	}

	private void ShakeNearby(float strength, float duration, float range)
	{
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player
			&& player.GlobalPosition.DistanceTo(GlobalPosition) < range)
			player.ShakeCamera(strength, duration);
	}

	// ── Build ───────────────────────────────────────────────────────────────────────────────────

	private void RebuildAll()
	{
		if (!IsInsideTree())
			return;
		BuildBody();
		BuildVisual();
	}

	private void BuildBody()
	{
		_collision?.QueueFree();
		_hazard?.QueueFree();
		_crush?.QueueFree();
		_collision = new CollisionShape2D { Name = "CollisionShape2D", Shape = new CircleShape2D { Radius = _radius } };
		AddChild(_collision);
		_hazard = new Hazard { Name = "HazardArea", CollisionLayer = 0, CollisionMask = 2, InstantKill = InstantKill, Damage = Damage, KnockbackForce = KnockbackForce };
		_hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = _radius * 0.92f } });
		AddChild(_hazard);
		_crush = new Area2D { Name = "CrushArea", CollisionLayer = 0, CollisionMask = 4, Monitorable = false };
		_crush.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = _radius + 4f } });
		AddChild(_crush);
	}

	private void BuildVisual()
	{
		_visual?.QueueFree();
		_contactShadow?.QueueFree();
		_dust?.QueueFree();
		_grit?.QueueFree();
		var rng = new RandomNumberGenerator { Seed = (ulong)(_seed * 7919 + 17) };
		float r = _radius;

		// Soft contact shadow on the ground (doesn't roll).
		_contactShadow = new Polygon2D { Name = "ContactShadow", Polygon = Ellipse(r * 0.85f, r * 0.13f, new Vector2(0f, r - 1f), 20), Color = new Color(0f, 0f, 0f, 0.38f), ZIndex = -1 };
		AddChild(_contactShadow);

		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);

		// Lumpy silhouette: radius noise smoothed with its neighbours so the edge reads as weathered
		// stone, not a jagged star.
		const int n = 26;
		var raw = new float[n];
		for (int i = 0; i < n; i++)
			raw[i] = rng.RandfRange(0.88f, 1.04f);
		_outline = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float k = (raw[(i + n - 1) % n] + raw[i] * 2f + raw[(i + 1) % n]) / 4f;
			float a = Mathf.Tau * i / n;
			_outline[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * k;
		}
		var rock = new Polygon2D { Name = "Rock", Polygon = _outline, Color = _rockColor, ClipChildren = ClipChildrenMode.AndDraw };
		_visual.AddChild(rock);

		// Facets: big chiselled plates in slightly different tones (clipped to the silhouette).
		for (int f = 0; f < 9; f++)
		{
			Vector2 c = Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * r * rng.RandfRange(0.1f, 0.8f);
			float size = r * rng.RandfRange(0.35f, 0.6f);
			var pts = new Vector2[5];
			float rot = rng.RandfRange(0f, Mathf.Tau);
			for (int p = 0; p < 5; p++)
				pts[p] = c + Vector2.FromAngle(rot + Mathf.Tau * p / 5f + rng.RandfRange(-0.3f, 0.3f)) * size * rng.RandfRange(0.7f, 1.1f);
			Color tone = rng.Randf() < 0.5f ? _rockColor.Lightened(rng.RandfRange(0.05f, 0.14f)) : _rockColor.Darkened(rng.RandfRange(0.06f, 0.18f));
			rock.AddChild(new Polygon2D { Polygon = pts, Color = tone with { A = 0.55f } });
		}

		// Sediment strata: soft curved bands across the stone — the clearest read of the roll.
		for (int s = 0; s < 3; s++)
		{
			float y = Mathf.Lerp(-r * 0.55f, r * 0.55f, s / 2f) + rng.RandfRange(-r * 0.08f, r * 0.08f);
			var band = new Vector2[9];
			for (int p = 0; p < 9; p++)
			{
				float x = Mathf.Lerp(-r * 1.1f, r * 1.1f, p / 8f);
				band[p] = new Vector2(x, y + Mathf.Sin(p * 0.9f + s) * r * 0.07f);
			}
			rock.AddChild(new Line2D { Points = band, Width = r * 0.13f, DefaultColor = _shadowColor with { A = 0.22f }, JointMode = Line2D.LineJointMode.Round });
		}

		// Cracks: jagged dark lines with a lit lip along one side.
		for (int c = 0; c < 4; c++)
		{
			Vector2 start = Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * r * rng.RandfRange(0.55f, 1f);
			Vector2 dir = (-start).Normalized().Rotated(rng.RandfRange(-0.7f, 0.7f));
			var crack = new List<Vector2> { start };
			int segments = rng.RandiRange(3, 5);
			for (int p = 0; p < segments; p++)
				crack.Add(crack[^1] + dir.Rotated(rng.RandfRange(-0.6f, 0.6f)) * r * rng.RandfRange(0.12f, 0.22f));
			Vector2[] cp = crack.ToArray();
			rock.AddChild(new Line2D { Points = cp, Width = 2.2f, DefaultColor = _shadowColor.Darkened(0.3f), WidthCurve = TaperCurve() });
			rock.AddChild(new Line2D { Points = cp, Width = 1f, DefaultColor = _highlightColor with { A = 0.3f }, Position = new Vector2(-1f, -1f), WidthCurve = TaperCurve() });
		}

		// Pockmarks: irregular chipped hollows — a dark pit with a faint lit lower lip (light from
		// the top-left falls into the hollow and catches its far edge).
		for (int k = 0; k < 5; k++)
		{
			Vector2 c = Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * r * rng.RandfRange(0.15f, 0.8f);
			float kr = r * rng.RandfRange(0.05f, 0.11f);
			var pit = new Vector2[7];
			for (int p = 0; p < 7; p++)
				pit[p] = c + Vector2.FromAngle(Mathf.Tau * p / 7f) * kr * rng.RandfRange(0.6f, 1.15f);
			rock.AddChild(new Polygon2D { Polygon = pit, Color = _shadowColor with { A = 0.55f } });
			rock.AddChild(new Line2D { Points = new[] { pit[0], pit[1], pit[2] }, Width = 1f, DefaultColor = _highlightColor with { A = 0.22f } });
		}

		// Grain: a scatter of tiny light and dark specks so the stone has texture up close.
		int specks = Mathf.RoundToInt(r * 2.2f);
		for (int g = 0; g < specks; g++)
		{
			Vector2 c = Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * r * Mathf.Sqrt(rng.Randf()) * 0.95f;
			float sz = rng.RandfRange(0.6f, 1.6f);
			bool light = rng.Randf() < 0.45f;
			Color tone = light ? _highlightColor with { A = rng.RandfRange(0.1f, 0.25f) } : _shadowColor with { A = rng.RandfRange(0.25f, 0.5f) };
			rock.AddChild(new Polygon2D { Polygon = new[] { c, c + new Vector2(sz, 0f), c + new Vector2(sz, sz), c + new Vector2(0f, sz) }, Color = tone });
		}

		// Fixed lighting (counter-rotated every frame): a broad top-left highlight, a deep
		// bottom-right core shadow and a thin bounce light on the bottom rim — the sphere read.
		_lighting = new Node2D { Name = "Lighting" };
		rock.AddChild(_lighting);
		_lighting.AddChild(new Sprite2D { Texture = Dot, Position = new Vector2(r * 0.55f, r * 0.6f), Scale = Vector2.One * r * 0.034f, Modulate = _shadowColor with { A = 0.95f } });
		_lighting.AddChild(new Sprite2D { Texture = Dot, Position = new Vector2(r * 0.85f, r * 0.2f), Scale = Vector2.One * r * 0.022f, Modulate = _shadowColor with { A = 0.6f } });
		_lighting.AddChild(new Sprite2D { Texture = Dot, Position = new Vector2(-r * 0.38f, -r * 0.42f), Scale = Vector2.One * r * 0.024f, Modulate = _highlightColor with { A = 0.42f } });
		_lighting.AddChild(new Sprite2D { Texture = Dot, Position = new Vector2(-r * 0.42f, -r * 0.48f), Scale = Vector2.One * r * 0.008f, Modulate = _highlightColor with { A = 0.5f } });
		var bounce = new Vector2[10];
		for (int p = 0; p < 10; p++)
			bounce[p] = Vector2.FromAngle(Mathf.Lerp(0.55f, 2.3f, p / 9f)) * r * 0.9f;
		_lighting.AddChild(new Line2D { Points = bounce, Width = r * 0.06f, DefaultColor = _highlightColor with { A = 0.12f } });

		// Dark outline on the silhouette (rolls with it, sits on top of the clipped layers).
		var outline = new Line2D { Points = _outline, Closed = true, Width = 2f, DefaultColor = _shadowColor.Darkened(0.45f) with { A = 0.9f } };
		_visual.AddChild(outline);

		// Rolling dust (soft, drifting up and back) and grit (small hard specks thrown back).
		_dust = new CpuParticles2D
		{
			Name = "Dust", Texture = Dot, Amount = 28, Lifetime = 0.9f, Emitting = false, LocalCoords = false,
			Direction = new Vector2(-Direction, -0.6f), Spread = 35f, Gravity = new Vector2(0f, -25f),
			InitialVelocityMin = 25f, InitialVelocityMax = 70f, DampingMin = 20f, DampingMax = 40f,
			ScaleAmountMin = r * 0.0016f, ScaleAmountMax = r * 0.0036f,
			ColorRamp = new Gradient { Colors = new[] { _rockColor.Lightened(0.25f) with { A = 0.5f }, _rockColor.Lightened(0.2f) with { A = 0f } }, Offsets = new[] { 0f, 1f } },
			ZIndex = -1,
		};
		AddChild(_dust);
		_grit = new CpuParticles2D
		{
			Name = "Grit", Texture = Dot, Amount = 10, Lifetime = 0.5f, Emitting = false, LocalCoords = false,
			Direction = new Vector2(-Direction, -1.2f), Spread = 30f, Gravity = new Vector2(0f, 900f),
			InitialVelocityMin = 90f, InitialVelocityMax = 200f, ScaleAmountMin = 0.025f, ScaleAmountMax = 0.05f,
			Color = _shadowColor.Lightened(0.1f),
		};
		AddChild(_grit);
	}

	// ── FX ──────────────────────────────────────────────────────────────────────────────────────

	// Before it (re)appears: a curtain of dust and pebbles falling onto the spawn point, plus a
	// growing rumble — the "something is coming" tell.
	private void SpawnWarning()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		Vector2 at = (GetParent() as Node2D)?.ToGlobal(_spawnPosition) ?? _spawnPosition;
		var dust = new CpuParticles2D
		{
			Texture = Dot, Amount = 24, Lifetime = 0.8f, LocalCoords = false, Explosiveness = 0f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(_radius * 0.8f, 4f),
			Direction = Vector2.Down, Spread = 10f, Gravity = new Vector2(0f, 300f),
			InitialVelocityMin = 10f, InitialVelocityMax = 40f, ScaleAmountMin = 0.06f, ScaleAmountMax = 0.14f,
			ColorRamp = new Gradient { Colors = new[] { _rockColor.Lightened(0.25f) with { A = 0.55f }, _rockColor with { A = 0f } }, Offsets = new[] { 0f, 1f } },
		};
		var pebbles = new CpuParticles2D
		{
			Texture = Dot, Amount = 8, Lifetime = 0.7f, LocalCoords = false,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(_radius * 0.7f, 4f),
			Direction = Vector2.Down, Spread = 5f, Gravity = new Vector2(0f, 1100f),
			InitialVelocityMin = 0f, InitialVelocityMax = 30f, ScaleAmountMin = 0.035f, ScaleAmountMax = 0.06f,
			Color = _shadowColor.Lightened(0.15f),
		};
		foreach (CpuParticles2D p in new[] { dust, pebbles })
		{
			scene.AddChild(p);
			p.GlobalPosition = at + new Vector2(0f, -_radius - 40f);
			p.Emitting = true;
			p.GetTree().CreateTimer(WarningTime + 1f).Timeout += p.QueueFree;
		}
		Sfx.PlayAt(this, "World/Mining", "mine");
		ShakeNearby(1.5f, WarningTime, RumbleRange);
	}

	// Stone, not fire: a billowing cloud of rock dust where it burst, a low dust wave rolling out
	// along the floor both ways, fine grit, and a brief dull flash.
	private void SpawnDustBurst(Vector2 contact)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		float r = _radius;
		Vector2 center = contact - new Vector2(0f, r);
		Color dust = _rockColor.Lightened(0.12f);
		var ramp = new Gradient { Colors = new[] { dust with { A = 0.75f }, dust with { A = 0.45f }, dust.Darkened(0.2f) with { A = 0f } }, Offsets = new[] { 0f, 0.4f, 1f } };
		var cloud = new CpuParticles2D
		{
			Texture = Dot, Amount = 34, Lifetime = 1.3f, OneShot = true, Explosiveness = 0.95f, LocalCoords = false,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere, EmissionSphereRadius = r * 0.6f,
			Direction = new Vector2(-Direction * 0.6f, -1f), Spread = 70f, Gravity = new Vector2(0f, -20f),
			InitialVelocityMin = 40f, InitialVelocityMax = 150f, DampingMin = 60f, DampingMax = 110f,
			ScaleAmountMin = r * 0.005f, ScaleAmountMax = r * 0.011f, ColorRamp = ramp, ZIndex = 4,
		};
		var wave = new CpuParticles2D
		{
			Texture = Dot, Amount = 24, Lifetime = 1f, OneShot = true, Explosiveness = 1f, LocalCoords = false,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(r * 0.4f, 2f),
			Direction = new Vector2(1f, -0.12f), Spread = 8f, Gravity = Vector2.Zero,
			InitialVelocityMin = 120f, InitialVelocityMax = 260f, DampingMin = 180f, DampingMax = 260f,
			ScaleAmountMin = r * 0.003f, ScaleAmountMax = r * 0.006f, ColorRamp = ramp, ZIndex = 4,
		};
		var grit = new CpuParticles2D
		{
			Texture = Dot, Amount = 26, Lifetime = 0.45f, OneShot = true, Explosiveness = 1f, LocalCoords = false,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere, EmissionSphereRadius = r * 0.5f,
			Direction = new Vector2(-Direction * 0.5f, -1f), Spread = 45f, Gravity = new Vector2(0f, 1100f),
			InitialVelocityMin = 150f, InitialVelocityMax = 380f, ScaleAmountMin = 0.03f, ScaleAmountMax = 0.06f,
			Color = _shadowColor.Lightened(0.15f), ZIndex = 4,
		};
		cloud.Position = center;
		wave.Position = contact + new Vector2(0f, -6f);
		// Mirror of the floor wave rolling the other way (one emitter with a wide spread sent dust
		// down through the floor).
		var waveBack = (CpuParticles2D)wave.Duplicate();
		waveBack.Direction = new Vector2(-1f, -0.12f);
		grit.Position = center;
		foreach (CpuParticles2D p in new[] { cloud, wave, waveBack, grit })
		{
			scene.AddChild(p);
			p.GlobalPosition = p.Position;
			p.Emitting = true;
			p.GetTree().CreateTimer(1.8f).Timeout += p.QueueFree;
		}
		var flash = new PointLight2D { Texture = Dot, TextureScale = r * 0.08f, Energy = 0.7f, Color = _highlightColor };
		scene.AddChild(flash);
		flash.GlobalPosition = center;
		Tween fade = flash.CreateTween();
		fade.TweenProperty(flash, "energy", 0f, 0.35f);
		fade.TweenCallback(Callable.From(flash.QueueFree));
	}

	// The boulder breaking apart: rock chunks of its own colours flung up and out, each bouncing
	// once on the floor before crumbling away.
	private void SpawnChunks(Vector2 contact)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		Vector2 center = contact - new Vector2(0f, _radius);
		int count = Mathf.RoundToInt(10 + _radius / 6f);
		for (int i = 0; i < count; i++)
		{
			float size = _radius * _rng.RandfRange(0.12f, 0.3f);
			var pts = new Vector2[_rng.RandiRange(4, 6)];
			for (int p = 0; p < pts.Length; p++)
				pts[p] = Vector2.FromAngle(Mathf.Tau * p / pts.Length + _rng.RandfRange(-0.3f, 0.3f)) * size * _rng.RandfRange(0.6f, 1f);
			var chunk = new Polygon2D { Polygon = pts, Color = _rockColor.Darkened(_rng.RandfRange(-0.08f, 0.25f)), ZIndex = 3 };
			// Two-tone: the back half of each chunk in shadow so they read as solid bits of stone.
			var shade = new List<Vector2> { Vector2.Zero };
			for (int p = pts.Length / 2; p < pts.Length; p++)
				shade.Add(pts[p]);
			chunk.AddChild(new Polygon2D { Polygon = shade.ToArray(), Color = _shadowColor with { A = 0.55f } });
			chunk.AddChild(new Line2D { Points = new[] { pts[0], pts[1] }, Width = 1.2f, DefaultColor = _highlightColor with { A = 0.4f } });
			scene.AddChild(chunk);
			Vector2 start = center + Vector2.FromAngle(_rng.RandfRange(0f, Mathf.Tau)) * _radius * _rng.RandfRange(0.1f, 0.7f);
			chunk.GlobalPosition = start;
			// Mostly thrown back from the wall it hit, up and out.
			Vector2 v = new(-Direction * _rng.RandfRange(30f, 260f) + _rng.RandfRange(-60f, 60f), -_rng.RandfRange(150f, 420f));
			float spin = _rng.RandfRange(-9f, 9f);
			float floorY = contact.Y - size * 0.5f;
			float life = _rng.RandfRange(0.9f, 1.4f);
			Vector2 pos = start;
			float t = 0f;
			bool bounced = false;
			Tween tween = chunk.CreateTween();
			tween.TweenMethod(Callable.From<float>(now =>
			{
				if (!IsInstanceValid(chunk))
					return;
				float dt = now - t;
				t = now;
				v.Y += 1300f * dt;
				pos += v * dt;
				if (pos.Y > floorY && v.Y > 0f)
				{
					pos.Y = floorY;
					v = bounced ? new Vector2(v.X * 0.4f, 0f) : new Vector2(v.X * 0.55f, -v.Y * 0.3f);
					spin *= 0.5f;
					bounced = true;
				}
				chunk.GlobalPosition = pos;
				chunk.Rotation += spin * dt;
				chunk.Modulate = chunk.Modulate with { A = 1f - Mathf.Clamp((now - life * 0.65f) / (life * 0.35f), 0f, 1f) };
			}), 0f, life, life);
			tween.TweenCallback(Callable.From(chunk.QueueFree));
		}
	}

	private static Vector2[] Ellipse(float rx, float ry, Vector2 center, int points)
	{
		var pts = new Vector2[points];
		for (int i = 0; i < points; i++)
		{
			float a = Mathf.Tau * i / points;
			pts[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		return pts;
	}

	private static Curve TaperCurve()
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0.2f));
		return curve;
	}
}
