using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Vagoneta suelta: a runaway ore cart, the mine's own rolling hazard. It sits braked at the top of
// its rail (RailPoints, relative to the node), and when released it rolls under gravity — slow off
// the line, faster and faster down every slope — until it smashes into the rail's end, bursts into
// planks and ore, and (RespawnDelay) a new one is wheeled back to the top.
//
// The read is different from the boulder: it's LOW (≈ 34 px) and fast, so the answer is to jump it
// (89 px jump clears it easily) or roll through it with i-frames, never to find a refuge. It also
// flattens enemies in its way, so luring bandits onto the line pays off.
//
// Release:
//  - Timed: every Interval seconds (StartOffset shifts the first one) — a pattern to read.
//  - Zone: the player entering ZoneSize (centred at ZoneOffset) releases it once per cart.
//  - Manual: only Activate() (a Lever/PressurePlate/TrapRelay target).
// Before rolling it always rattles for WarningTime with its red lamp blinking: never a cheap hit.
[Tool]
public partial class LooseMineCart : Node2D
{
	public enum ReleaseMode { Timed, Zone, Manual }

	private Vector2[] _railPoints = { Vector2.Zero, new(300f, 150f), new(600f, 150f) };

	[Export]
	public Vector2[] RailPoints
	{
		get => _railPoints;
		set { _railPoints = value ?? System.Array.Empty<Vector2>(); QueueRedraw(); }
	}

	[Export] public ReleaseMode Release = ReleaseMode.Timed;
	[Export] public float Interval = 4.5f;
	[Export] public float StartOffset;
	[Export] public Vector2 ZoneOffset = new(0f, -40f);
	[Export] public Vector2 ZoneSize = new(120f, 120f);
	[Export] public float WarningTime = 0.6f;
	[Export] public float StartSpeed = 60f;
	[Export] public float MaxSpeed = 420f;
	[Export] public float SlopeGravity = 700f;
	[Export] public float Friction = 20f;
	// Below this on a flat/uphill it just stops where it is and breaks (spent).
	[Export] public float StallSpeed = 25f;
	[Export] public float RespawnDelay = 1.5f;
	[Export] public int PlayerDamage = 25;
	[Export] public float Knockback = 380f;
	[Export] public int EnemyDamage = 80;
	// Draw the rail? Off when it shares a rail already drawn by another cart.
	[Export] public bool DrawRail = true;

	private const float W = 52f;
	private const float H = 34f;

	private enum State { Waiting, Warning, Rolling, Gone }

	private State _state = State.Waiting;
	private float _timer;
	private float _distance;
	private float _speed;
	private float _railLength;
	private Node2D _cart;
	private Node2D _tilt;
	private Sprite2D _lamp;
	private Hazard _hazard;
	private Area2D _zone;
	private bool _playerInZone;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public override void _Ready()
	{
		_railLength = MineRail.Length(_railPoints);
		if (DrawRail)
			MineRail.BuildVisual(this, _railPoints);
		BuildCart();
		PlaceCart();
		if (Engine.IsEditorHint())
			return;

		_hazard = Hazard.CreateArea(_cart, false, PlayerDamage, Knockback);
		_hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(W - 6f, H - 4f) }, Position = new Vector2(0f, -H / 2f) });
		_hazard.Monitoring = false;

		if (Release == ReleaseMode.Zone)
		{
			_zone = new Area2D { Name = "ZonaSuelta", CollisionLayer = 0, CollisionMask = 2, Monitorable = false, Position = ZoneOffset };
			_zone.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = ZoneSize } });
			AddChild(_zone);
			_zone.BodyEntered += b => { if (b.IsInGroup("player")) { _playerInZone = true; if (_state == State.Waiting) Activate(); } };
			_zone.BodyExited += b => { if (b.IsInGroup("player")) _playerInZone = false; };
		}
		_timer = Release == ReleaseMode.Timed ? Interval + StartOffset : 0f;
	}

	// Lever / plate / relay entry point: releases the cart if one is waiting at the top.
	public void Activate()
	{
		if (_state != State.Waiting)
			return;
		_state = State.Warning;
		_timer = WarningTime;
		Sfx.PlayAt(this, "World/Mining", "mine", 900f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		switch (_state)
		{
			case State.Waiting:
				if (Release == ReleaseMode.Timed)
				{
					_timer -= dt;
					if (_timer <= 0f)
						Activate();
				}
				break;

			case State.Warning:
				_timer -= dt;
				_cart.Position = MineRail.Sample(_railPoints, 0f, out _) + new Vector2((float)GD.RandRange(-1.5, 1.5), 0f);
				_lamp.Modulate = Mathf.PosMod(_timer, 0.16f) < 0.08f ? new Color(1f, 0.15f, 0.1f) : new Color(0.4f, 0.05f, 0.05f);
				if (_timer <= 0f)
				{
					_state = State.Rolling;
					_speed = StartSpeed;
					_hazard.Monitoring = true;
					_lamp.Modulate = new Color(1f, 0.2f, 0.12f);
				}
				break;

			case State.Rolling:
				MineRail.Sample(_railPoints, _distance, out Vector2 tangent);
				_speed += SlopeGravity * tangent.Y * dt - Friction * dt;
				_speed = Mathf.Min(_speed, MaxSpeed);
				_distance += _speed * dt;
				PlaceCart();
				HitEnemies();
				if (_distance >= _railLength || _speed < StallSpeed)
					Smash();
				break;

			case State.Gone:
				_timer -= dt;
				if (_timer <= 0f)
					Respawn();
				break;
		}
	}

	private void PlaceCart()
	{
		Vector2 p = MineRail.Sample(_railPoints, _distance, out Vector2 tangent);
		_cart.Position = p;
		_tilt.Rotation = tangent.Angle();
	}

	private void HitEnemies()
	{
		Vector2 center = _cart.GlobalPosition + new Vector2(0f, -H / 2f);
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || enemy.HasMeta("vagoneta_golpeado"))
				continue;
			Vector2 d = enemy.GlobalPosition - center;
			if (Mathf.Abs(d.X) < W / 2f + 14f && Mathf.Abs(d.Y) < H / 2f + 30f)
			{
				enemy.SetMeta("vagoneta_golpeado", true);
				ProjectileReflect.TryHitEnemy(enemy, EnemyDamage, new Vector2(1f, -0.6f).Normalized(), Knockback);
				GetTree().CreateTimer(0.6f).Timeout += () => { if (IsInstanceValid(enemy)) enemy.RemoveMeta("vagoneta_golpeado"); };
			}
		}
	}

	private void Smash()
	{
		_state = State.Gone;
		_timer = RespawnDelay;
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_cart.Visible = false;
		Vector2 at = _cart.GlobalPosition;
		GroundSlamFx.Impact(this, at, 0.9f);
		Sfx.PlayAt(this, "World/Mining", "mine", 1100f);
		SpawnDebris(at);
		// The smash throws sparks: any gas around the crash point goes up.
		GrisuPocket.IgniteAt(GetTree(), at + new Vector2(0f, -H / 2f));
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p && p.GlobalPosition.DistanceTo(at) < 500f)
			p.ShakeCamera(5f, 0.25f);
	}

	private void Respawn()
	{
		_distance = 0f;
		PlaceCart();
		_cart.Visible = true;
		_cart.Modulate = new Color(1f, 1f, 1f, 0f);
		CreateTween().TweenProperty(_cart, "modulate:a", 1f, 0.4f);
		_lamp.Modulate = new Color(0.4f, 0.05f, 0.05f);
		_state = State.Waiting;
		_timer = Interval;
		if (Release == ReleaseMode.Zone && _playerInZone)
			Activate();
	}

	// Planks and ore chunks flying off the smash point; plain physics-free tweens, gone in a second.
	private void SpawnDebris(Vector2 at)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		Node parent = GetParent();
		for (int i = 0; i < 9; i++)
		{
			bool plank = i % 3 != 0;
			var piece = new Polygon2D
			{
				Polygon = plank
					? new[] { new Vector2(-7f, -2f), new Vector2(7f, -2f), new Vector2(7f, 2f), new Vector2(-7f, 2f) }
					: new[] { new Vector2(-3f, -3f), new Vector2(3f, -2f), new Vector2(2f, 3f), new Vector2(-3f, 2f) },
				Color = plank ? new Color(0.42f, 0.28f, 0.16f) : new Color(0.5f, 0.42f, 0.32f),
				GlobalPosition = at + new Vector2(0f, -H / 2f),
				ZIndex = 3,
			};
			parent.AddChild(piece);
			piece.GlobalPosition = at + new Vector2(0f, -H / 2f);
			Vector2 fly = new Vector2(rng.RandfRange(-140f, 140f), rng.RandfRange(-150f, -40f));
			var tween = piece.CreateTween().SetParallel();
			tween.TweenProperty(piece, "position", piece.Position + fly, 0.7f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			tween.TweenProperty(piece, "rotation", rng.RandfRange(-6f, 6f), 0.7f);
			tween.TweenProperty(piece, "modulate:a", 0f, 0.7f).SetDelay(0.3f);
			tween.Chain().TweenCallback(Callable.From(piece.QueueFree));
		}
	}

	private void BuildCart()
	{
		_cart?.QueueFree();
		_cart = new Node2D { Name = "Vagoneta" };
		AddChild(_cart);
		_tilt = new Node2D();
		_cart.AddChild(_tilt);
		var iron = new Color(0.25f, 0.23f, 0.25f);
		float w = W / 2f;
		_tilt.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-w, -H), new Vector2(w, -H), new Vector2(w - 6f, -7f), new Vector2(-w + 6f, -7f) },
			Color = iron,
		});
		// Ore heaped over the rim.
		_tilt.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-w + 3f, -H), new Vector2(-w + 12f, -H - 7f), new Vector2(-2f, -H - 10f), new Vector2(10f, -H - 6f), new Vector2(w - 3f, -H) },
			Color = new Color(0.45f, 0.4f, 0.36f),
		});
		_tilt.AddChild(new Line2D { Points = new[] { new Vector2(-w - 2f, -H), new Vector2(w + 2f, -H) }, Width = 3f, DefaultColor = new Color(0.5f, 0.3f, 0.18f) });
		foreach (float x in new[] { -w + 11f, w - 11f })
			_tilt.AddChild(new Polygon2D { Polygon = Circle(5.5f, 10, new Vector2(x, -5f)), Color = new Color(0.13f, 0.13f, 0.15f) });
		_lamp = new Sprite2D { Texture = Dot, Material = Additive, Position = new Vector2(-w + 4f, -H + 6f), Scale = Vector2.One * 0.1f, Modulate = new Color(0.4f, 0.05f, 0.05f) };
		_tilt.AddChild(_lamp);
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint() || _railPoints.Length < 2)
			return;
		DrawPolyline(_railPoints, new Color(1f, 0.5f, 0.3f, 0.8f), 2f);
	}

	private static Vector2[] Circle(float r, int steps, Vector2 c)
	{
		var pts = new Vector2[steps];
		for (int i = 0; i < steps; i++)
			pts[i] = c + new Vector2(r, 0f).Rotated(Mathf.Tau * i / steps);
		return pts;
	}
}
