using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum StalactiteTrigger
{
	// Falls when the player walks into the column underneath it.
	PlayerBelow,
	// Falls on its own every Interval seconds (it regrows in between) — a rhythm to read.
	Timed,
	// Only falls when something calls Drop() (a pressure plate, a lever's Activated signal...).
	Manual,
}

// A stalactite that breaks off the ceiling and falls. Place the node where it hangs from (the top of
// the rock); it hangs Length below that. Idle it drips now and then; when triggered it shakes harder
// and harder, sheds dust and pebbles and creaks (the tell), then drops under gravity until it meets
// whatever floor is under it, hurting the player — and enemies, so one can be lured underneath — on
// the way down, and shatters into shards and dust. Afterwards it either regrows from the ceiling or
// leaves a solid pile of rubble that works as a step.
//
// Everything is drawn in code from a seed (jagged two-tone rock, lit edge, cracks, wet tip), so it fits
// any cave without sprite art; tune the three colors to the zone's palette. [Tool] so the shape, the
// trigger column and the landing spot preview live in the editor; the _initialized guard keeps the
// property setters from touching child nodes while Godot is still restoring saved values.
[Tool]
public partial class FallingStalactite : Node2D
{
	[Signal] public delegate void TriggeredEventHandler();
	[Signal] public delegate void ShatteredEventHandler();

	private float _length = 110f;
	private float _width = 34f;
	private int _seed = 1;
	private Color _rockColor = new(0.42f, 0.45f, 0.53f, 1f);
	private Color _shadowColor = new(0.23f, 0.25f, 0.32f, 1f);
	private Color _highlightColor = new(0.76f, 0.84f, 0.95f, 1f);

	[ExportGroup("Shape")]
	[Export(PropertyHint.Range, "30,400,1")]
	public float Length { get => _length; set { _length = value; RebuildVisual(); } }
	[Export(PropertyHint.Range, "10,140,1")]
	public float Width { get => _width; set { _width = value; RebuildVisual(); } }
	// Different seed = a different jagged outline for the same size.
	[Export]
	public int Seed { get => _seed; set { _seed = value; RebuildVisual(); } }
	[Export]
	public Color RockColor { get => _rockColor; set { _rockColor = value; RebuildVisual(); } }
	[Export]
	public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; RebuildVisual(); } }
	[Export]
	public Color HighlightColor { get => _highlightColor; set { _highlightColor = value; RebuildVisual(); } }

	[ExportGroup("Trigger")]
	[Export] public StalactiteTrigger Trigger = StalactiteTrigger.PlayerBelow;
	// Extra width either side of the rock that still counts as "underneath" — a walking player is
	// caught a step before they are directly below, so the shake reads as the warning it is.
	[Export] public float DetectionMargin = 26f;
	[Export] public float Interval = 3.5f;
	// Timed only: shifts this one's rhythm so a row doesn't fall in sync.
	[Export] public float StartOffset;

	[ExportGroup("Fall")]
	[Export] public float TelegraphDuration = 0.55f;
	[Export] public float TelegraphShake = 2.2f;
	[Export] public float FallGravity = 2400f;
	[Export] public float MaxFallSpeed = 1400f;
	// How far down it looks for a floor to shatter on; past that it just falls out of the scene.
	[Export] public float MaxFallDistance = 1400f;
	[Export] public uint FloorMask = 1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms;

	[ExportGroup("Damage")]
	[Export] public bool InstantKill;
	[Export] public int Damage = 35;
	[Export] public float KnockbackForce = 260f;
	[Export] public bool HurtsEnemies = true;
	[Export] public int EnemyDamage = 60;

	[ExportGroup("Afterwards")]
	// > 0: regrows from the ceiling this long after shattering. 0: gone for the rest of the visit.
	[Export] public float RespawnDelay = 5f;
	// Leaves a solid mound of rubble where it lands (a step up to a ledge, a low wall...). Cleared
	// again when it regrows.
	[Export] public bool LeaveRubble;
	[Export] public float ImpactShakeStrength = 5f;
	[Export] public float ImpactShakeRange = 520f;

	[ExportGroup("Ambience")]
	// Faint cool light on the spike so it stays readable in a dark cave (0 = none).
	[Export] public float TipLightEnergy = 1.0f;
	[Export] public bool Drips = true;
	[Export] public Vector2 DripInterval = new(2.5f, 6f);

	private enum State { Idle, Telegraph, Falling, Gone, Regrowing }

	private const float HurtPortion = 0.6f;
	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

	private bool _initialized;
	private State _state = State.Idle;
	private float _timer;
	private float _dripTimer;
	private float _fallSpeed;
	private float _floorY = float.NaN; // local Y of the floor below the tip, NaN = none found
	private Vector2[] _outline = System.Array.Empty<Vector2>();
	private readonly RandomNumberGenerator _rng = new();

	private Node2D _rock;        // everything that falls
	private Node2D _visual;      // drawn rock (shaken during the telegraph)
	private Area2D _detector;
	private Hazard _hurtZone;
	private CollisionShape2D _hurtShape;
	private Area2D _enemyZone;
	private CollisionShape2D _enemyShape;
	private readonly HashSet<Node> _enemiesHit = new();
	private StaticBody2D _rubble;
	private Node2D _root;        // ceiling clump: stays put when the rock breaks off
	private Line2D _breakLine;   // glowing seam at the break point, flickers during the telegraph
	private PointLight2D _tipLight;

	public override void _Ready()
	{
		_initialized = true;
		_rock = new Node2D { Name = "Rock" };
		AddChild(_rock);
		RebuildVisual();
		if (Engine.IsEditorHint())
		{
			FindFloor();
			QueueRedraw();
			return;
		}

		_rng.Randomize();
		BuildHurtZones();
		InitAfterPhysicsReady();
		_dripTimer = _rng.RandfRange(DripInterval.X, DripInterval.Y);
		if (Trigger == StalactiteTrigger.Timed)
			_timer = Mathf.Max(0.05f, Interval - StartOffset);
	}

	// Floors built by other pieces in their own _Ready (MiniCorridor, tilemaps) aren't in the physics
	// space until the next physics frame, so the floor below — and the trigger column sized from it —
	// are only measured then.
	private async void InitAfterPhysicsReady()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (!IsInstanceValid(this))
			return;
		FindFloor();
		BuildDetector();
	}

	// Same as Drop() — the common entry point pressure plates call on any trap.
	public void Activate() => Drop();

	// Breaks it off now (telegraph first), whatever its trigger mode — for pressure plates, levers, etc.
	public void Drop()
	{
		if (_state != State.Idle)
			return;
		_state = State.Telegraph;
		_timer = TelegraphDuration;
		EmitSignal(SignalName.Triggered);
		Sfx.PlayAt(this, "World/Mining", "mine", 900f);
		StartTelegraphDust();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;

		switch (_state)
		{
			case State.Idle:
				TickDrip(dt);
				if (Trigger == StalactiteTrigger.Timed)
				{
					_timer -= dt;
					if (_timer <= 0f)
						Drop();
				}
				break;
			case State.Telegraph:
				_timer -= dt;
				// Shake grows towards the drop so the last moment reads as "now".
				float t = 1f - Mathf.Clamp(_timer / Mathf.Max(0.01f, TelegraphDuration), 0f, 1f);
				float amp = TelegraphShake * (0.35f + 0.65f * t);
				_visual.Position = new Vector2(_rng.RandfRange(-amp, amp), _rng.RandfRange(-amp * 0.3f, amp * 0.3f));
				// The seam brightens and flickers as it gives way.
				_breakLine.DefaultColor = _highlightColor with { A = Mathf.Clamp(0.25f + 0.75f * t, 0f, 1f) * _rng.RandfRange(0.55f, 1f) };
				if (_timer <= 0f)
					StartFall();
				break;
			case State.Gone:
				if (RespawnDelay > 0f)
				{
					_timer -= dt;
					if (_timer <= 0f)
						Regrow();
				}
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _state != State.Falling)
			return;
		float dt = (float)delta;
		_fallSpeed = Mathf.Min(_fallSpeed + FallGravity * dt, MaxFallSpeed);
		float step = _fallSpeed * dt;
		float tipY = _rock.Position.Y + _length;

		if (!float.IsNaN(_floorY) && tipY + step >= _floorY)
		{
			_rock.Position = new Vector2(0f, _floorY - _length);
			HitEnemiesOverlapping();
			Shatter();
			return;
		}
		_rock.Position += new Vector2(0f, step);
		HitEnemiesOverlapping();
		if (_rock.Position.Y > MaxFallDistance + _length)
			Vanish();
	}

	// ── States ──────────────────────────────────────────────────────────────────────────────────

	private void StartFall()
	{
		_state = State.Falling;
		_fallSpeed = 0f;
		_visual.Position = Vector2.Zero;
		_breakLine.DefaultColor = _highlightColor with { A = 0f };
		// Re-measured now: the floor may have changed since load (a crumbled floor, rubble...).
		FindFloor();
		_enemiesHit.Clear();
		_hurtShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		_enemyShape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
	}

	private void Shatter()
	{
		Vector2 impact = GlobalPosition + new Vector2(0f, _floorY);
		EndFall();
		EmitSignal(SignalName.Shattered);
		SpawnShards(impact);
		SpawnImpactDust(impact);
		SpawnImpactFlash(impact);
		Sfx.PlayAt(this, "World/Mining", "mine", 1100f);
		if (ImpactShakeStrength > 0f && GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player
			&& player.GlobalPosition.DistanceTo(impact) <= ImpactShakeRange)
			player.ShakeCamera(ImpactShakeStrength, 0.22f);
		if (LeaveRubble)
			BuildRubble(impact);
	}

	// Fell off the bottom of the scene without meeting a floor.
	private void Vanish() => EndFall();

	private void EndFall()
	{
		_hurtShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_enemyShape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_rock.Visible = false;
		_state = State.Gone;
		_timer = RespawnDelay;
	}

	private void Regrow()
	{
		_state = State.Regrowing;
		_rock.Position = Vector2.Zero;
		_rock.Visible = true;
		_rock.Scale = new Vector2(1f, 0.05f);
		_rock.Modulate = _rock.Modulate with { A = 0f };
		if (IsInstanceValid(_rubble))
		{
			StaticBody2D rubble = _rubble;
			_rubble = null;
			Tween clear = rubble.CreateTween();
			clear.TweenProperty(rubble, "modulate:a", 0f, 0.6f);
			clear.TweenCallback(Callable.From(rubble.QueueFree));
		}
		Tween grow = CreateTween();
		grow.TweenProperty(_rock, "scale:y", 1f, 0.8f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		grow.Parallel().TweenProperty(_rock, "modulate:a", 1f, 0.4f);
		grow.TweenCallback(Callable.From(() =>
		{
			_state = State.Idle;
			if (Trigger == StalactiteTrigger.Timed)
				_timer = Interval;
		}));
	}

	// ── Detection & damage ──────────────────────────────────────────────────────────────────────

	// Local Y of the first floor straight below the tip (NaN when none within MaxFallDistance).
	private void FindFloor()
	{
		if (!IsInsideTree())
			return;
		Vector2 from = GlobalPosition + new Vector2(0f, _length + 2f);
		var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0f, MaxFallDistance), FloorMask);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		_floorY = hit.Count > 0 ? ((Vector2)hit["position"]).Y - GlobalPosition.Y : float.NaN;
	}

	private void BuildDetector()
	{
		if (Trigger != StalactiteTrigger.PlayerBelow)
			return;
		float depth = float.IsNaN(_floorY) ? 600f : _floorY - _length;
		_detector = new Area2D { Name = "Detector", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_detector.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width + DetectionMargin * 2f, Mathf.Max(depth, 8f)) },
			Position = new Vector2(0f, _length + depth / 2f),
		});
		AddChild(_detector);
		_detector.BodyEntered += body =>
		{
			if (body.IsInGroup("player"))
				Drop();
		};
	}

	private void BuildHurtZones()
	{
		var size = new Vector2(_width * 0.7f, _length * HurtPortion);
		var center = new Vector2(0f, _length - size.Y / 2f);

		_hurtZone = new Hazard { Name = "HurtZone", CollisionLayer = 0, CollisionMask = 2, InstantKill = InstantKill, Damage = Damage, KnockbackForce = KnockbackForce };
		_hurtShape = new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = center, Disabled = true };
		_hurtZone.AddChild(_hurtShape);
		_rock.AddChild(_hurtZone);

		if (!HurtsEnemies)
			return;
		_enemyZone = new Area2D { Name = "EnemyZone", CollisionLayer = 0, CollisionMask = 4, Monitorable = false };
		_enemyShape = new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = center, Disabled = true };
		_enemyZone.AddChild(_enemyShape);
		_rock.AddChild(_enemyZone);
	}

	private void HitEnemiesOverlapping()
	{
		if (_enemyZone is null || !_enemyZone.Monitoring)
			return;
		foreach (Node2D body in _enemyZone.GetOverlappingBodies())
		{
			if (_enemiesHit.Contains(body))
				continue;
			if (ProjectileReflect.TryHitEnemy(body, EnemyDamage, Vector2.Down, KnockbackForce * 0.5f))
				_enemiesHit.Add(body);
		}
	}

	// ── Visual ──────────────────────────────────────────────────────────────────────────────────

	private void RebuildVisual()
	{
		if (!_initialized || _rock is null)
			return;
		_visual?.QueueFree();
		_visual = new Node2D { Name = "Visual" };
		_rock.AddChild(_visual);
		_rock.MoveChild(_visual, 0);

		var rng = new RandomNumberGenerator { Seed = (ulong)(uint)_seed * 2654435761UL + 17UL };
		_outline = BuildOutline(rng);

		// Ceiling clump the spike grows out of.
		BuildRoot(rng);
		// Body, then the shadowed right side over it, then a lit sliver down the left edge.
		_visual.AddChild(new Polygon2D { Polygon = _outline, Color = _rockColor });
		_visual.AddChild(new Polygon2D { Polygon = BuildShadowSide(), Color = _shadowColor });
		var lit = new Line2D { Points = BuildLitEdge(), Width = Mathf.Clamp(_width * 0.08f, 1.5f, 4f), DefaultColor = _highlightColor, JointMode = Line2D.LineJointMode.Round };
		lit.WidthCurve = TaperCurve();
		_visual.AddChild(lit);
		AddCracks(rng);
		// Wet glint near the tip.
		_visual.AddChild(new Sprite2D
		{
			Texture = Dot,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = _highlightColor with { A = 0.35f },
			Scale = Vector2.One * Mathf.Clamp(_width / 160f, 0.08f, 0.3f),
			Position = new Vector2(-_width * 0.04f, _length * 0.86f),
		});
		_visual.AddChild(new Line2D { Points = Closed(_outline), Width = 1f, DefaultColor = _shadowColor.Darkened(0.45f) });
		QueueRedraw();
	}

	// The clump the spike grows out of stays on the ceiling (a sibling of the falling rock), with a
	// thin seam where it will break; a faint cool light at the tip keeps the spike readable in a
	// dark cave (same per-prop PointLight2D pattern the caves use).
	private void BuildRoot(RandomNumberGenerator rng)
	{
		_root?.QueueFree();
		_root = new Node2D { Name = "Root" };
		AddChild(_root);
		_root.AddChild(new Polygon2D { Polygon = BuildBaseClump(rng), Color = _shadowColor.Lerp(_rockColor, 0.45f) });
		float seamHalf = _width * 0.42f;
		_breakLine = new Line2D
		{
			Points = new[] { new Vector2(-seamHalf, 7f), new Vector2(-seamHalf * 0.3f, 9f), new Vector2(seamHalf * 0.2f, 6.5f), new Vector2(seamHalf, 8.5f) },
			Width = 1.4f,
			DefaultColor = _highlightColor with { A = 0f },
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		};
		_root.AddChild(_breakLine);

		_tipLight?.QueueFree();
		_tipLight = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = _highlightColor,
			Energy = TipLightEnergy,
			TextureScale = Mathf.Clamp(_length / 140f, 0.5f, 1.6f),
			Position = new Vector2(0f, _length * 0.72f),
		};
		_rock.AddChild(_tipLight);
	}

	// Left edge down to the tip and back up the right edge, tapering and jagged; bulges get rarer
	// towards the tip so it reads as one hanging spike rather than a ragged blob.
	private Vector2[] BuildOutline(RandomNumberGenerator rng)
	{
		const int steps = 10;
		var left = new List<Vector2>();
		var right = new List<Vector2>();
		for (int i = 0; i < steps; i++)
		{
			float t = (float)i / steps;
			float half = _width * 0.5f * Mathf.Pow(1f - t, 0.8f);
			float jag = _width * 0.11f * (1f - t);
			float y = t * _length + rng.RandfRange(-0.02f, 0.02f) * _length;
			left.Add(new Vector2(-half + rng.RandfRange(-jag, jag * 0.6f), y));
			right.Add(new Vector2(half + rng.RandfRange(-jag * 0.6f, jag), y + rng.RandfRange(-0.015f, 0.015f) * _length));
		}
		var points = new List<Vector2>(left) { new(rng.RandfRange(-1.5f, 1.5f), _length) };
		right.Reverse();
		points.AddRange(right);
		// Broken top edge (where it parts from the ceiling clump) instead of a clean cut.
		for (int i = 1; i <= 3; i++)
			points.Add(new Vector2(Mathf.Lerp(right[^1].X, left[0].X, i / 4f), rng.RandfRange(1f, 7f)));
		return points.ToArray();
	}

	private Vector2[] BuildBaseClump(RandomNumberGenerator rng)
	{
		float w = _width * 0.75f;
		var pts = new List<Vector2> { new(-w, -2f) };
		for (int i = 0; i <= 6; i++)
		{
			float x = Mathf.Lerp(-w, w, i / 6f);
			pts.Add(new Vector2(x, rng.RandfRange(4f, 10f) * (1f - Mathf.Abs(x) / w * 0.6f)));
		}
		pts.Add(new Vector2(w, -2f));
		return pts.ToArray();
	}

	// The right side of the body, from a line a little right of centre to the right edge.
	private Vector2[] BuildShadowSide()
	{
		int n = _outline.Length;
		int tip = n / 2;
		var pts = new List<Vector2>();
		for (int i = tip; i < n; i++)
			pts.Add(_outline[i]);
		for (int i = n - 1; i > tip; i--)
		{
			Vector2 r = _outline[i];
			pts.Add(new Vector2(r.X * 0.25f + _width * 0.04f * (1f - r.Y / _length), r.Y));
		}
		return pts.ToArray();
	}

	private Vector2[] BuildLitEdge()
	{
		int tip = _outline.Length / 2;
		var pts = new Vector2[tip];
		for (int i = 0; i < tip; i++)
			pts[i] = _outline[i] + new Vector2(Mathf.Clamp(_width * 0.07f, 1.2f, 3f), 0f);
		return pts;
	}

	private void AddCracks(RandomNumberGenerator rng)
	{
		int cracks = _length > 80f ? 2 : 1;
		for (int c = 0; c < cracks; c++)
		{
			float y = rng.RandfRange(0.12f, 0.45f) * _length;
			float half = _width * 0.5f * Mathf.Pow(1f - y / _length, 0.8f);
			var pts = new List<Vector2>();
			float x = rng.RandfRange(-half * 0.5f, half * 0.2f);
			for (int i = 0; i < 4; i++)
			{
				pts.Add(new Vector2(x, y));
				x += rng.RandfRange(-3f, 3f);
				y += rng.RandfRange(4f, 9f);
			}
			_visual.AddChild(new Line2D { Points = pts.ToArray(), Width = 1f, DefaultColor = _shadowColor.Darkened(0.35f) with { A = 0.8f } });
		}
	}

	private static Curve TaperCurve()
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0.15f));
		return curve;
	}

	private static Vector2[] Closed(Vector2[] pts)
	{
		var closed = new Vector2[pts.Length + 1];
		pts.CopyTo(closed, 0);
		closed[^1] = pts[0];
		return closed;
	}

	// ── Effects ─────────────────────────────────────────────────────────────────────────────────

	private void TickDrip(float dt)
	{
		if (!Drips || float.IsNaN(_floorY))
			return;
		_dripTimer -= dt;
		if (_dripTimer > 0f)
			return;
		_dripTimer = _rng.RandfRange(DripInterval.X, DripInterval.Y);
		if (GetTree().CurrentScene is not Node scene)
			return;
		var drop = new Polygon2D
		{
			Polygon = new[] { new Vector2(0f, -3f), new Vector2(1.4f, 0f), new Vector2(0f, 1.6f), new Vector2(-1.4f, 0f) },
			Color = _highlightColor.Lightened(0.3f) with { A = 0.85f },
			ZIndex = ZIndex,
		};
		scene.AddChild(drop);
		Vector2 start = GlobalPosition + new Vector2(0f, _length);
		drop.GlobalPosition = start;
		float fall = _floorY - _length;
		float time = Mathf.Sqrt(2f * fall / 1200f);
		Tween tween = drop.CreateTween();
		tween.TweenProperty(drop, "global_position:y", start.Y + fall, time).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(() =>
		{
			Splash(drop.GlobalPosition, 4, 45f, _highlightColor with { A = 0.7f }, 1.1f, 0.35f);
			drop.QueueFree();
		}));
	}

	private void StartTelegraphDust()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var dust = new CpuParticles2D
		{
			Texture = Dot,
			Amount = 14,
			Lifetime = 0.9f,
			OneShot = false,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width * 0.6f, 3f),
			Direction = Vector2.Down,
			Spread = 20f,
			Gravity = new Vector2(0f, 260f),
			InitialVelocityMin = 10f,
			InitialVelocityMax = 40f,
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.07f,
			Color = _rockColor.Lightened(0.25f) with { A = 0.7f },
			LocalCoords = false,
		};
		scene.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + new Vector2(0f, 6f);
		// Pebbles: a few chips breaking off the base ahead of the fall.
		for (int i = 0; i < 4; i++)
		{
			var pebble = new Shard(Pebble(_rng.RandfRange(1.5f, 3f)), new Vector2(_rng.RandfRange(-30f, 30f), _rng.RandfRange(0f, 60f)), 900f,
				float.IsNaN(_floorY) ? float.NaN : GlobalPosition.Y + _floorY, 1.2f, _rng.RandfRange(-6f, 6f));
			scene.AddChild(pebble);
			pebble.GlobalPosition = GlobalPosition + new Vector2(_rng.RandfRange(-_width * 0.5f, _width * 0.5f), _rng.RandfRange(4f, 14f));
		}
		GetTree().CreateTimer(TelegraphDuration + 0.1f).Timeout += () =>
		{
			if (IsInstanceValid(dust))
			{
				dust.Emitting = false;
				dust.GetTree().CreateTimer(1f).Timeout += dust.QueueFree;
			}
		};
	}

	private Polygon2D Pebble(float size)
	{
		var pts = new Vector2[5];
		for (int i = 0; i < 5; i++)
			pts[i] = Vector2.Right.Rotated(i * Mathf.Tau / 5f + _rng.RandfRange(-0.4f, 0.4f)) * size * _rng.RandfRange(0.7f, 1.2f);
		return new Polygon2D { Polygon = pts, Color = _rockColor.Lightened(_rng.RandfRange(-0.1f, 0.2f)) };
	}

	// The rock bursts into pieces of itself: bigger chunks from the base, slivers from the tip.
	private void SpawnShards(Vector2 impact)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		int count = Mathf.Clamp(Mathf.RoundToInt(_width * _length / 450f), 6, 16);
		for (int i = 0; i < count; i++)
		{
			float size = _rng.RandfRange(0.12f, 0.3f) * _width;
			var piece = new Node2D();
			Polygon2D body = Pebble(size);
			body.Color = (_rng.Randf() < 0.4f ? _shadowColor : _rockColor).Lightened(_rng.RandfRange(-0.05f, 0.12f));
			piece.AddChild(body);
			piece.AddChild(new Line2D { Points = Closed(body.Polygon), Width = 1f, DefaultColor = _highlightColor with { A = 0.85f } });
			var velocity = new Vector2(_rng.RandfRange(-220f, 220f), _rng.RandfRange(-380f, -140f));
			var shard = new Shard(piece, velocity, 1100f, impact.Y, _rng.RandfRange(0.9f, 1.6f), _rng.RandfRange(-12f, 12f));
			scene.AddChild(shard);
			shard.GlobalPosition = impact + new Vector2(_rng.RandfRange(-_width * 0.3f, _width * 0.3f), -_rng.RandfRange(4f, 20f));
		}
	}

	private void SpawnImpactDust(Vector2 impact)
	{
		// Billowing dust, a low skirt of grit sliding out along the floor, and rock-on-rock sparks.
		Splash(impact, 26, 150f, _rockColor.Lightened(0.45f) with { A = 0.75f }, Mathf.Clamp(_width / 110f, 0.14f, 0.45f), 1.0f, true);
		Splash(impact + new Vector2(0f, -2f), 14, 220f, _rockColor.Lightened(0.2f) with { A = 0.6f }, 0.06f, 0.5f);
		Splash(impact, 12, 320f, _highlightColor.Lightened(0.4f), 0.035f, 0.35f, false, true);
	}

	// Brief cool flash on the impact point so the hit reads even in a dark cave.
	private void SpawnImpactFlash(Vector2 impact)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var flash = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = _highlightColor,
			Energy = 1.05f,
			TextureScale = Mathf.Clamp(_width / 75f, 0.4f, 1.1f),
		};
		scene.AddChild(flash);
		flash.GlobalPosition = impact + new Vector2(0f, -10f);
		Tween fade = flash.CreateTween();
		fade.TweenProperty(flash, "energy", 0f, 0.35f).SetEase(Tween.EaseType.Out);
		fade.TweenCallback(Callable.From(flash.QueueFree));
	}

	// One-shot burst of soft dots fanning up out of a point (drip splash, impact dust, chips).
	private void Splash(Vector2 at, int amount, float speed, Color color, float scale, float lifetime, bool billow = false, bool additive = false)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var burst = new CpuParticles2D
		{
			Texture = Dot,
			Amount = amount,
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = 0.95f,
			Direction = Vector2.Up,
			Spread = billow ? 80f : 60f,
			Gravity = billow ? new Vector2(0f, -20f) : new Vector2(0f, 500f),
			InitialVelocityMin = speed * 0.4f,
			InitialVelocityMax = speed,
			DampingMin = billow ? 160f : 0f,
			DampingMax = billow ? 200f : 0f,
			ScaleAmountMin = scale * 0.6f,
			ScaleAmountMax = scale * (billow ? 1.8f : 1.2f),
			Color = color,
			LocalCoords = false,
		};
		var fade = new Gradient();
		fade.SetColor(0, Colors.White);
		fade.SetColor(1, Colors.White with { A = 0f });
		burst.ColorRamp = fade;
		if (additive)
			burst.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		scene.AddChild(burst);
		burst.GlobalPosition = at;
		burst.Emitting = true;
		burst.GetTree().CreateTimer(lifetime + 0.3f).Timeout += burst.QueueFree;
	}

	private void BuildRubble(Vector2 impact)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		float w = Mathf.Clamp(_width * 1.1f, 24f, 140f);
		float h = Mathf.Clamp(_width * 0.45f, 12f, 34f);
		var mound = new[] { new Vector2(-w / 2f, 0f), new Vector2(-w * 0.3f, -h * 0.8f), new Vector2(0f, -h), new Vector2(w * 0.32f, -h * 0.75f), new Vector2(w / 2f, 0f) };
		_rubble = new StaticBody2D { Name = "Escombro", CollisionLayer = 1, CollisionMask = 0 };
		_rubble.AddChild(new CollisionPolygon2D { Polygon = mound });
		_rubble.AddChild(new Polygon2D { Polygon = mound, Color = _rockColor });
		for (int i = 0; i < 5; i++)
		{
			Polygon2D chunk = Pebble(_rng.RandfRange(0.12f, 0.22f) * w);
			chunk.Position = new Vector2(_rng.RandfRange(-w * 0.35f, w * 0.35f), -_rng.RandfRange(h * 0.2f, h * 0.8f));
			chunk.Color = _rng.Randf() < 0.5f ? _shadowColor : _rockColor.Lightened(0.1f);
			_rubble.AddChild(chunk);
		}
		scene.CallDeferred(Node.MethodName.AddChild, _rubble);
		_rubble.Position = impact;
	}

	// Editor preview: the trigger column and where it will land.
	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		float floor = float.IsNaN(_floorY) ? _length + 300f : _floorY;
		if (Trigger == StalactiteTrigger.PlayerBelow)
		{
			float half = _width / 2f + DetectionMargin;
			DrawRect(new Rect2(-half, _length, half * 2f, floor - _length), new Color(1f, 0.6f, 0.2f, 0.12f));
		}
		DrawLine(new Vector2(-_width, floor), new Vector2(_width, floor), new Color(1f, 0.4f, 0.2f, 0.8f), 2f);
		DrawLine(new Vector2(0f, _length), new Vector2(0f, floor), new Color(1f, 0.5f, 0.2f, 0.35f), 1f);
	}

	// A flying piece with its own gravity and spin that bounces once off the impact floor and fades.
	private partial class Shard : Node2D
	{
		private Vector2 _velocity;
		private readonly float _gravity;
		private readonly float _floorY;
		private readonly float _lifetime;
		private readonly float _spin;
		private float _age;
		private bool _bounced;

		public Shard() { }

		public Shard(Node2D visual, Vector2 velocity, float gravity, float floorY, float lifetime, float spin)
		{
			AddChild(visual);
			_velocity = velocity;
			_gravity = gravity;
			_floorY = floorY;
			_lifetime = lifetime;
			_spin = spin;
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			_age += dt;
			_velocity.Y += _gravity * dt;
			Position += _velocity * dt;
			Rotation += _spin * dt;
			if (!float.IsNaN(_floorY) && GlobalPosition.Y > _floorY - 2f && _velocity.Y > 0f)
			{
				GlobalPosition = new Vector2(GlobalPosition.X, _floorY - 2f);
				if (_bounced)
				{
					_velocity = new Vector2(_velocity.X * 0.6f, 0f);
				}
				else
				{
					_velocity = new Vector2(_velocity.X * 0.5f, -_velocity.Y * 0.3f);
					_bounced = true;
				}
			}
			if (_age > _lifetime * 0.6f)
				Modulate = Modulate with { A = Mathf.Max(0f, 1f - (_age - _lifetime * 0.6f) / (_lifetime * 0.4f)) };
			if (_age >= _lifetime)
				QueueFree();
		}
	}
}
