using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bolsa de grisú: firedamp seeping out of a seam and pooling in a low gallery. A timing trap with
// a clear read and a twist:
//   Clear (ClearTime)   → safe, only a faint seam glow.
//   Filling (FillTime)  → green haze rises and thickens inside Size (still harmless).
//   Primed (PrimeTime)  → the broken lamp at SparkOffset starts spitting sparks, the haze pulses.
//   Blast               → the whole pocket goes up: damage + knockback to the player AND enemies
//                         inside (Size grown by BlastMargin), screen shake, and every MineRockWall
//                         within BlastRadius collapses.
// The twist is that last line: the gas is also the mine's dynamite. A pocket in Valve mode stays
// clear until something calls Activate() (a Lever "opening the valve"), fills once, blows, and
// clears (Loop = keep cycling afterwards) — so a lever next to a rubble plug is a puzzle, not a
// decoration. Everything is procedural (additive dots, particles, a light), no VFX sheets.
[Tool]
public partial class GrisuPocket : Node2D
{
	public enum TriggerMode { Timed, Valve }

	private Vector2 _size = new(200f, 120f);

	// Gas volume, centred on the node.
	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = value; QueueRedraw(); }
	}

	[Export] public TriggerMode Mode = TriggerMode.Timed;
	// Valve mode: keep cycling after the first blast.
	[Export] public bool Loop;
	// Valve mode: the gallery is already full of gas and has NO spark of its own — it only goes up
	// when something else lights it (IgniteAt: a runaway cart smashing in, a fire). Clears after.
	[Export] public bool StartsFull;
	[Export] public float ClearTime = 2.2f;
	[Export] public float FillTime = 1.8f;
	[Export] public float PrimeTime = 0.7f;
	[Export] public float StartOffset;
	[Export] public float BlastMargin = 16f;
	[Export] public float BlastRadius = 140f;
	[Export] public int PlayerDamage = 30;
	[Export] public int EnemyDamage = 60;
	[Export] public float Knockback = 420f;
	// Where the sparking lamp hangs, relative to the node (top corner by default).
	[Export] public Vector2 SparkOffset = new(-80f, -50f);
	[Export] public Color GasColor = new(0.45f, 1f, 0.35f, 1f);

	private enum Phase { Clear, Filling, Primed, Idle, Full }

	private Phase _phase = Phase.Clear;
	private float _timer;
	private Node2D _haze;
	private CpuParticles2D _motes;
	private CpuParticles2D _sparks;
	private Sprite2D _sparkGlow;
	private PointLight2D _gasLight;
	private float _density;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public bool IsArmed => _phase is Phase.Filling or Phase.Primed or Phase.Full;

	// Any outside spark at `point` (inside a pocket grown by `margin`) sets off every pocket that
	// currently holds gas.
	public static void IgniteAt(SceneTree tree, Vector2 point, float margin = 24f)
	{
		foreach (Node node in tree.GetNodesInGroup(Group))
		{
			if (node is not GrisuPocket pocket || !pocket.IsArmed)
				continue;
			var rect = new Rect2(pocket.GlobalPosition - pocket._size / 2f, pocket._size).Grow(margin);
			if (rect.HasPoint(point))
				pocket.Blast();
		}
	}

	public const string Group = "grisu_pocket";

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		AddToGroup(Group);
		BuildVisual();
		if (Mode == TriggerMode.Valve)
		{
			_phase = Phase.Idle;
			if (StartsFull)
			{
				_phase = Phase.Full;
				_density = 0.8f;
			}
		}
		else
		{
			_phase = Phase.Clear;
			_timer = ClearTime + StartOffset;
		}
	}

	// Valve: start a fill → blast cycle now (ignored while one is already running).
	public void Activate()
	{
		if (_phase is Phase.Filling or Phase.Primed)
			return;
		_phase = Phase.Filling;
		_timer = FillTime;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		_timer -= dt;
		switch (_phase)
		{
			case Phase.Clear:
				_density = Mathf.MoveToward(_density, 0f, dt * 2f);
				if (_timer <= 0f)
				{
					_phase = Phase.Filling;
					_timer = FillTime;
				}
				break;
			case Phase.Idle:
				_density = Mathf.MoveToward(_density, 0f, dt * 2f);
				break;
			case Phase.Full:
				_density = 0.75f + 0.05f * Mathf.Sin(_timer * 2f);
				break;
			case Phase.Filling:
				_density = Mathf.Clamp(1f - _timer / FillTime, 0f, 1f) * 0.8f;
				if (_timer <= 0f)
				{
					_phase = Phase.Primed;
					_timer = PrimeTime;
				}
				break;
			case Phase.Primed:
				_density = 0.8f + 0.2f * Mathf.Sin(_timer * 40f);
				if (_timer <= 0f)
					Blast();
				break;
		}
		UpdateVisual();
	}

	private void Blast()
	{
		bool cycle = Mode == TriggerMode.Timed || Loop;
		_phase = cycle ? Phase.Clear : Phase.Idle;
		_timer = ClearTime;
		_density = 0f;

		Vector2 center = GlobalPosition;
		var hurt = new Rect2(center - _size / 2f - Vector2.One * BlastMargin, _size + Vector2.One * BlastMargin * 2f);

		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is not Node2D player || !hurt.HasPoint(player.GlobalPosition))
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable && PlayerDamage > 0)
				stats.TakeDamage(PlayerDamage);
			if (player.HasMethod("ApplyKnockback"))
				player.Call("ApplyKnockback", Away(player, center), Knockback);
		}
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Node2D enemy && hurt.HasPoint(enemy.GlobalPosition))
				ProjectileReflect.TryHitEnemy(enemy, EnemyDamage, Away(enemy, center), Knockback);
		}
		MineRockWall.BlastAt(GetTree(), center, BlastRadius);

		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p && p.GlobalPosition.DistanceTo(center) < 700f)
			p.ShakeCamera(8f, 0.35f);
		Sfx.PlayAt(this, "Magic", "Fireball", 1100f);
		SpawnFlash(center);
	}

	private static Vector2 Away(Node2D body, Vector2 center)
	{
		float side = Mathf.Sign(body.GlobalPosition.X - center.X);
		return new Vector2(side == 0f ? 1f : side, -0.6f).Normalized();
	}

	// ── Visual ─────────────────────────────────────────────────────────────────────────────────

	private void BuildVisual()
	{
		_haze = new Node2D { Name = "Neblina", Modulate = new Color(1f, 1f, 1f, 0f) };
		AddChild(_haze);
		var rng = new RandomNumberGenerator { Seed = (ulong)(GetInstanceId() % 9973) };
		int blobs = Mathf.Max(3, (int)(_size.X * _size.Y / 5000f));
		for (int i = 0; i < blobs; i++)
		{
			var blob = new Sprite2D
			{
				Texture = Dot,
				Material = Additive,
				Position = new Vector2(rng.RandfRange(-_size.X / 2.4f, _size.X / 2.4f), rng.RandfRange(-_size.Y / 2.6f, _size.Y / 2.6f)),
				Scale = Vector2.One * rng.RandfRange(0.9f, 1.5f) * Mathf.Min(_size.X, _size.Y) / 180f,
				Modulate = GasColor with { A = 0.22f },
			};
			_haze.AddChild(blob);
			// Lazy drift so the gas looks alive.
			var tween = blob.CreateTween().SetLoops();
			Vector2 drift = new(rng.RandfRange(-18f, 18f), rng.RandfRange(-10f, 10f));
			float t = rng.RandfRange(1.6f, 2.8f);
			tween.TweenProperty(blob, "position", blob.Position + drift, t).SetTrans(Tween.TransitionType.Sine);
			tween.TweenProperty(blob, "position", blob.Position, t).SetTrans(Tween.TransitionType.Sine);
		}
		_motes = new CpuParticles2D
		{
			Texture = Dot,
			Material = Additive,
			Amount = 18,
			Lifetime = 2f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = _size / 2f,
			Gravity = new Vector2(0f, -12f),
			InitialVelocityMin = 2f,
			InitialVelocityMax = 10f,
			Spread = 180f,
			ScaleAmountMin = 0.02f,
			ScaleAmountMax = 0.04f,
			Color = GasColor with { A = 0.7f },
			Emitting = false,
		};
		AddChild(_motes);
		_gasLight = new PointLight2D { Texture = Dot, TextureScale = Mathf.Max(_size.X, _size.Y) / 60f, Energy = 0f, Color = GasColor };
		AddChild(_gasLight);

		// The broken lamp: a tiny cage on a hook.
		var lamp = new Node2D { Name = "LamparaRota", Position = SparkOffset };
		AddChild(lamp);
		lamp.AddChild(new Line2D { Points = new[] { new Vector2(0f, -14f), new Vector2(0f, -5f) }, Width = 1.5f, DefaultColor = new Color(0.3f, 0.3f, 0.32f) });
		lamp.AddChild(new Polygon2D { Polygon = new[] { new Vector2(-4f, -5f), new Vector2(4f, -5f), new Vector2(3f, 5f), new Vector2(-3f, 5f) }, Color = new Color(0.25f, 0.22f, 0.2f) });
		_sparkGlow = new Sprite2D { Texture = Dot, Material = Additive, Scale = Vector2.One * 0.08f, Modulate = new Color(1f, 0.7f, 0.3f, 0.3f) };
		lamp.AddChild(_sparkGlow);
		_sparks = new CpuParticles2D
		{
			Texture = Dot,
			Material = Additive,
			Amount = 14,
			Lifetime = 0.4f,
			Explosiveness = 0.6f,
			Direction = Vector2.Down,
			Spread = 70f,
			InitialVelocityMin = 40f,
			InitialVelocityMax = 120f,
			Gravity = new Vector2(0f, 400f),
			ScaleAmountMin = 0.02f,
			ScaleAmountMax = 0.045f,
			Color = new Color(1f, 0.8f, 0.4f),
			Emitting = false,
		};
		lamp.AddChild(_sparks);
	}

	private void UpdateVisual()
	{
		if (_haze == null)
			return;
		_haze.Modulate = new Color(1f, 1f, 1f, _density);
		_motes.Emitting = _density > 0.15f;
		_gasLight.Energy = _density * 0.6f;
		bool primed = _phase == Phase.Primed;
		_sparks.Emitting = primed;
		_sparkGlow.Modulate = primed ? new Color(1f, 0.8f, 0.4f, GD.Randf() > 0.5f ? 1f : 0.2f) : new Color(1f, 0.7f, 0.3f, 0.3f);
	}

	private void SpawnFlash(Vector2 center)
	{
		Node parent = GetParent();
		var flash = new Node2D { ZIndex = 5 };
		parent.AddChild(flash);
		flash.GlobalPosition = center;
		float s = Mathf.Max(_size.X, _size.Y) / 110f;
		var core = new Sprite2D { Texture = Dot, Material = Additive, Scale = Vector2.One * s * 0.6f, Modulate = new Color(1f, 0.95f, 0.8f) };
		var ring = new Sprite2D { Texture = Dot, Material = Additive, Scale = new Vector2(_size.X, _size.Y) / 70f, Modulate = new Color(1f, 0.5f, 0.15f) };
		var light = new PointLight2D { Texture = Dot, TextureScale = s * 4f, Energy = 2.5f, Color = new Color(1f, 0.6f, 0.25f) };
		flash.AddChild(ring);
		flash.AddChild(core);
		flash.AddChild(light);
		var embers = new CpuParticles2D
		{
			Texture = Dot,
			Material = Additive,
			Amount = 40,
			Lifetime = 0.7f,
			OneShot = true,
			Explosiveness = 1f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = _size / 2f,
			Spread = 180f,
			InitialVelocityMin = 60f,
			InitialVelocityMax = 220f,
			Gravity = new Vector2(0f, 250f),
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.07f,
			Color = new Color(1f, 0.65f, 0.25f),
		};
		flash.AddChild(embers);
		embers.Emitting = true;
		var tween = flash.CreateTween().SetParallel();
		tween.TweenProperty(core, "modulate:a", 0f, 0.35f);
		tween.TweenProperty(ring, "scale", ring.Scale * 1.4f, 0.45f);
		tween.TweenProperty(ring, "modulate:a", 0f, 0.45f);
		tween.TweenProperty(light, "energy", 0f, 0.5f);
		tween.Chain().TweenInterval(0.4f);
		tween.Chain().TweenCallback(Callable.From(flash.QueueFree));
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		DrawRect(new Rect2(-_size / 2f, _size), GasColor with { A = 0.18f });
		DrawRect(new Rect2(-_size / 2f, _size), GasColor with { A = 0.8f }, false, 1.5f);
		DrawCircle(SparkOffset, 4f, new Color(1f, 0.7f, 0.3f));
	}
}
