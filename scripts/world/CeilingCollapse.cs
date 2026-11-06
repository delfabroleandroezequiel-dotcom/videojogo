using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Derrumbe de techo: a stretch of rotten shoring that gives way when you walk under it. The node
// sits on the CEILING line; Width is the stretch, Drop is how far down the floor is.
//   1. The player enters the zone below → WarningTime of creaking: dust and pebbles trickle from
//      the whole width, the cracked beam glows, a small camera shake. Time to sprint through or
//      back off — the warning is the whole stretch, so you know exactly where it will land.
//   2. A slab of rubble (Width x RubbleHeight) drops with gravity; it hurts and knocks back anyone
//      under it (enemies too).
//   3. It lands and STAYS as solid ground — that's the point of the trap in a level: the pile
//      either becomes a step up to a ledge you couldn't reach before, or seals the way you came.
//      If the player is standing where it lands, they're shoved out to the nearer side (never stuck).
// PersistenceId (optional) remembers it collapsed for this save; empty = it resets every load.
[Tool]
public partial class CeilingCollapse : Node2D
{
	private float _width = 160f;
	private float _drop = 200f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); QueueRedraw(); }
	}

	// Ceiling to floor distance (where the rubble comes to rest).
	[Export]
	public float Drop
	{
		get => _drop;
		set { _drop = Mathf.Max(40f, value); QueueRedraw(); }
	}

	[Export] public float RubbleHeight = 56f;
	[Export] public float WarningTime = 0.9f;
	[Export] public float Gravity = 1400f;
	[Export] public int PlayerDamage = 35;
	[Export] public int EnemyDamage = 80;
	[Export] public float Knockback = 360f;
	[Export] public string PersistenceId = "";
	[Export] public int Seed = 3;

	private enum State { Armed, Warning, Falling, Landed }

	private State _state = State.Armed;
	private float _timer;
	private float _y;
	private float _speed;
	private Node2D _slab;
	private StaticBody2D _rest;
	private CollisionShape2D _restShape;
	private CpuParticles2D _dust;
	private Line2D _beamCrack;
	private bool _hitPlayer;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		BuildBeam();
		_slab = BuildSlab();
		AddChild(_slab);

		_rest = new StaticBody2D { Name = "Escombro", CollisionLayer = 1, CollisionMask = 0 };
		_restShape = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width, RubbleHeight) },
			Position = new Vector2(0f, _drop - RubbleHeight / 2f),
			Disabled = true,
		};
		_rest.AddChild(_restShape);
		AddChild(_rest);

		var zone = new Area2D { Name = "ZonaDerrumbe", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		zone.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width - 20f, _drop) }, Position = new Vector2(0f, _drop / 2f) });
		AddChild(zone);
		zone.BodyEntered += body =>
		{
			if (body.IsInGroup("player") && _state == State.Armed)
				StartWarning();
		};

		if (!string.IsNullOrEmpty(PersistenceId) && SaveManager.Instance != null && SaveManager.Instance.IsGateOpened(PersistenceId))
			Land(silent: true);
		else
			_slab.Position = new Vector2(0f, -RubbleHeight * 0.15f);
	}

	private void StartWarning()
	{
		_state = State.Warning;
		_timer = WarningTime;
		_dust.Emitting = true;
		Sfx.PlayAt(this, "World/Mining", "mine", 900f);
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p)
			p.ShakeCamera(2f, WarningTime);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		switch (_state)
		{
			case State.Warning:
				_timer -= dt;
				_slab.Position = new Vector2((float)GD.RandRange(-2.0, 2.0), -RubbleHeight * 0.15f + (float)GD.RandRange(0.0, 2.0));
				_beamCrack.DefaultColor = new Color(1f, 0.6f, 0.25f, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_timer * 18f)));
				if (_timer <= 0f)
				{
					_state = State.Falling;
					_y = 0f;
					_speed = 0f;
				}
				break;

			case State.Falling:
				_speed += Gravity * dt;
				_y += _speed * dt;
				float landY = _drop - RubbleHeight;
				if (_y >= landY)
				{
					_y = landY;
					Land(silent: false);
				}
				_slab.Position = new Vector2(0f, _y);
				HurtUnder();
				break;
		}
	}

	// Anyone overlapping the falling slab's rectangle takes the hit once.
	private void HurtUnder()
	{
		var rect = new Rect2(GlobalPosition + new Vector2(-_width / 2f, _y), new Vector2(_width, RubbleHeight + 32f));
		if (!_hitPlayer && GetTree().GetFirstNodeInGroup("player") is Node2D player && rect.HasPoint(player.GlobalPosition))
		{
			_hitPlayer = true;
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
				stats.TakeDamage(PlayerDamage);
			if (player.HasMethod("ApplyKnockback"))
				player.Call("ApplyKnockback", new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X) is var s && s != 0 ? s : 1f, -0.4f).Normalized(), Knockback);
		}
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Node2D enemy && rect.HasPoint(enemy.GlobalPosition) && !enemy.HasMeta("derrumbe_golpeado"))
			{
				enemy.SetMeta("derrumbe_golpeado", true);
				ProjectileReflect.TryHitEnemy(enemy, EnemyDamage, Vector2.Down, Knockback * 0.5f);
			}
		}
	}

	private void Land(bool silent)
	{
		_state = State.Landed;
		_y = _drop - RubbleHeight;
		_slab.Position = new Vector2(0f, _y);
		_dust.Emitting = false;
		_restShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		if (silent)
			return;
		if (!string.IsNullOrEmpty(PersistenceId))
			SaveManager.Instance?.MarkGateOpened(PersistenceId);

		Vector2 ground = GlobalPosition + new Vector2(0f, _drop);
		GroundSlamFx.Impact(this, ground, _width / 120f);
		Sfx.PlayAt(this, "World/Mining", "mine", 1100f);
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p)
		{
			p.ShakeCamera(7f, 0.3f);
			// Never bury the player inside the new block: shove them out to the nearer side.
			var rest = new Rect2(GlobalPosition + new Vector2(-_width / 2f, _drop - RubbleHeight - 32f), new Vector2(_width, RubbleHeight + 32f));
			if (rest.Grow(14f).HasPoint(p.GlobalPosition))
			{
				float left = rest.Position.X - 20f;
				float right = rest.End.X + 20f;
				float x = p.GlobalPosition.X - left < right - p.GlobalPosition.X ? left : right;
				p.GlobalPosition = new Vector2(x, GlobalPosition.Y + _drop - 33f);
			}
		}
	}

	// ── Visual ─────────────────────────────────────────────────────────────────────────────────

	private void BuildBeam()
	{
		var wood = new Color(0.38f, 0.25f, 0.14f);
		// The rotten crossbeam spanning the stretch, with a crack that glows during the warning.
		AddChild(new Line2D { Points = new[] { new Vector2(-_width / 2f - 10f, 6f), new Vector2(_width / 2f + 10f, 6f) }, Width = 10f, DefaultColor = wood });
		_beamCrack = new Line2D
		{
			Points = new[] { new Vector2(-14f, 2f), new Vector2(-4f, 8f), new Vector2(3f, 3f), new Vector2(12f, 9f) },
			Width = 1.5f,
			DefaultColor = new Color(1f, 0.6f, 0.25f, 0.35f),
		};
		AddChild(_beamCrack);
		_dust = new CpuParticles2D
		{
			Texture = Dot,
			Amount = 40,
			Lifetime = 0.9f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width / 2f, 2f),
			Position = new Vector2(0f, 10f),
			Direction = Vector2.Down,
			Spread = 10f,
			InitialVelocityMin = 10f,
			InitialVelocityMax = 40f,
			Gravity = new Vector2(0f, 500f),
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.07f,
			Color = new Color(0.75f, 0.68f, 0.58f, 0.8f),
			Emitting = false,
		};
		AddChild(_dust);
	}

	private Node2D BuildSlab()
	{
		var slab = new Node2D { Name = "Losa" };
		var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
		// Irregular top edge, flat bottom, filled with a darker base and a few chunk outlines.
		var pts = new System.Collections.Generic.List<Vector2> { new(-_width / 2f, RubbleHeight) };
		int steps = Mathf.Max(4, (int)(_width / 24f));
		for (int i = 0; i <= steps; i++)
		{
			float x = -_width / 2f + _width * i / steps;
			pts.Add(new Vector2(x, rng.RandfRange(0f, RubbleHeight * 0.35f)));
		}
		pts.Add(new Vector2(_width / 2f, RubbleHeight));
		slab.AddChild(new Polygon2D { Polygon = pts.ToArray(), Color = new Color(0.4f, 0.37f, 0.34f) });
		for (int i = 0; i < steps; i++)
		{
			float x = -_width / 2f + _width * (i + 0.5f) / steps;
			float r = rng.RandfRange(7f, 12f);
			var rock = new Vector2[6];
			for (int k = 0; k < 6; k++)
				rock[k] = new Vector2(x, RubbleHeight * rng.RandfRange(0.45f, 0.8f)) + new Vector2(r, 0f).Rotated(Mathf.Tau * k / 6f);
			slab.AddChild(new Polygon2D { Polygon = rock, Color = new Color(0.5f, 0.46f, 0.42f) });
		}
		return slab;
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		DrawRect(new Rect2(-_width / 2f, 0f, _width, _drop), new Color(1f, 0.6f, 0.2f, 0.12f));
		DrawRect(new Rect2(-_width / 2f, _drop - RubbleHeight, _width, RubbleHeight), new Color(0.6f, 0.55f, 0.5f, 0.6f));
		DrawLine(new Vector2(-_width / 2f, 6f), new Vector2(_width / 2f, 6f), new Color(0.5f, 0.32f, 0.18f), 6f);
	}
}
