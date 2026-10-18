using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandit bear trap hidden in the grass. Place the node on the floor surface. Stepping on it (walking —
// a dash or a jump carries you over) snaps the toothed jaws shut on the player's feet: a little
// damage, SnareDuration seconds pinned in place (Player.Snare: no walking/jumping/dashing/rolling,
// attacking and parrying still work) and the clang alerts every enemy within AlertRadius (Enemy.Alert),
// so the camp comes running at a stuck target. Then it lets go, lies sprung and re-arms itself.
// The tell is small on purpose: grass tufts drawn over it and, now and then, a glint off a tooth.
// Everything is drawn in code. [Tool] so the trap and its grass preview in the editor.
[Tool]
public partial class BearTrap : Node2D
{
	[Signal] public delegate void SprungEventHandler();

	[Export] public int Damage = 10;
	[Export] public float SnareDuration = 1.1f;
	[Export] public float AlertRadius = 650f;
	[Export] public float AlertDuration = 6f;
	// Seconds lying sprung (closed, harmless) before it opens again. 0 = single use.
	[Export] public float ReArmDelay = 5f;
	[Export] public float Size = 1f;

	private bool _grassCover = true;
	[Export]
	public bool GrassCover { get => _grassCover; set { _grassCover = value; Rebuild(); } }
	private Color _grassColor = new(0.17f, 0.27f, 0.15f, 1f);
	[Export]
	public Color GrassColor { get => _grassColor; set { _grassColor = value; Rebuild(); } }
	[Export] public Vector2 GlintInterval = new(2.2f, 4f);

	// Openness 1 = armed: both half-rings flattened onto the ground (a ring lying in the grass seen
	// from the side, teeth poking up); 0 = snapped shut, the two halves standing up as a dome.
	private const float Open = 1f;
	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

	private enum State { Armed, Clamped, Sprung }

	private bool _initialized;
	private State _state = State.Armed;
	private Node2D _visual;
	private Node2D _leftJaw;
	private Node2D _rightJaw;
	private Node2D _glint;
	private Node2D _grass;
	private Area2D _trigger;
	private float _glintTimer;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_initialized = true;
		_rng.Randomize();
		Rebuild();
		if (Engine.IsEditorHint())
			return;
		_trigger = new Area2D { Name = "Trigger", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_trigger.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(26f, 14f) * Size }, Position = new Vector2(0f, -7f * Size) });
		AddChild(_trigger);
		_glintTimer = _rng.RandfRange(GlintInterval.X, GlintInterval.Y);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _state != State.Armed)
			return;
		// Checked every frame (not just on entry): landing from a jump onto a trap you were already
		// above still springs it, while dashing or jumping across never does.
		foreach (Node2D body in _trigger.GetOverlappingBodies())
		{
			if (body is Metroidvania.Player.Player player && player.IsOnFloor() && !player.IsDashing)
			{
				Spring(player);
				return;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _state != State.Armed)
			return;
		_glintTimer -= (float)delta;
		if (_glintTimer > 0f)
			return;
		_glintTimer = _rng.RandfRange(GlintInterval.X, GlintInterval.Y);
		Twinkle();
	}

	private async void Spring(Metroidvania.Player.Player player)
	{
		_state = State.Clamped;
		EmitSignal(SignalName.Sprung);
		SnapJaws(0f, 0.06f);
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable && Damage > 0)
			stats.TakeDamage(Damage);
		player.Snare(SnareDuration);
		if (player.HasMethod("ShakeCamera"))
			player.ShakeCamera(3f, 0.12f);
		SpawnSparks();
		RustleGrass(1f);
		AlertNearby();

		await ToSignal(GetTree().CreateTimer(SnareDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		// Lets go: the jaws ease off a little and stay down.
		_state = State.Sprung;
		SnapJaws(0.15f, 0.2f);
		if (ReArmDelay <= 0f)
			return;
		await ToSignal(GetTree().CreateTimer(ReArmDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		// Don't re-arm under someone standing in it.
		while (IsInstanceValid(this) && HasPlayerInside())
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		SnapJaws(Open, 0.5f, Tween.TransitionType.Sine);
		RustleGrass(0.4f);
		_state = State.Armed;
	}

	private bool HasPlayerInside()
	{
		foreach (Node2D body in _trigger.GetOverlappingBodies())
			if (body.IsInGroup("player"))
				return true;
		return false;
	}

	private void AlertNearby()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Enemy enemy && enemy.GlobalPosition.DistanceTo(GlobalPosition) <= AlertRadius)
				enemy.Alert(AlertDuration);
		}
	}

	// ── Visual ──────────────────────────────────────────────────────────────────────────────────

	private void Rebuild()
	{
		if (!_initialized)
			return;
		_visual?.QueueFree();
		_visual = new Node2D { Name = "Visual", Scale = Vector2.One * Size };
		AddChild(_visual);

		var iron = new Color(0.3f, 0.31f, 0.34f, 1f);
		var rust = new Color(0.38f, 0.24f, 0.16f, 1f);
		var bright = new Color(0.66f, 0.68f, 0.72f, 1f);

		// Base plate and the trigger pan in the middle.
		_visual.AddChild(new Polygon2D { Polygon = Ellipse(16f, 2.6f, new Vector2(0f, -1.5f), 14), Color = iron.Darkened(0.35f) });
		_visual.AddChild(new Polygon2D { Polygon = Ellipse(5.5f, 1.6f, new Vector2(0f, -2.6f), 10), Color = rust });
		_visual.AddChild(new Line2D { Points = new[] { new Vector2(-15f, -1.5f), new Vector2(15f, -1.5f) }, Width = 1f, DefaultColor = iron.Lightened(0.2f) });

		_leftJaw = BuildJaw(-1f, iron, bright);
		_rightJaw = BuildJaw(1f, iron, bright);
		bool open = Engine.IsEditorHint() || _state == State.Armed;
		_leftJaw.Scale = JawScale(open ? Open : 0f);
		_rightJaw.Scale = JawScale(open ? Open : 0f);
		_visual.AddChild(_leftJaw);
		_visual.AddChild(_rightJaw);

		_glint = new Node2D { Name = "Glint", Position = new Vector2(-11f, -3f), Modulate = new Color(1f, 1f, 1f, 0f), ZIndex = 3 };
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_glint.AddChild(new Sprite2D { Texture = Dot, Material = additive, Scale = Vector2.One * 0.06f, Modulate = new Color(1f, 0.97f, 0.85f) });
		_glint.AddChild(new Line2D { Points = new[] { new Vector2(-4f, 0f), new Vector2(4f, 0f) }, Width = 0.8f, DefaultColor = new Color(1f, 0.97f, 0.85f), Material = additive });
		_glint.AddChild(new Line2D { Points = new[] { new Vector2(0f, -3f), new Vector2(0f, 3f) }, Width = 0.8f, DefaultColor = new Color(1f, 0.97f, 0.85f), Material = additive });
		_visual.AddChild(_glint);

		_grass = null;
		if (_grassCover)
		{
			_grass = new Node2D { Name = "Grass", ZIndex = 1 };
			var rng = new RandomNumberGenerator { Seed = 4242 };
			for (int i = 0; i < 11; i++)
			{
				float x = Mathf.Lerp(-19f, 19f, i / 10f) + rng.RandfRange(-1.5f, 1.5f);
				float h = rng.RandfRange(5f, 11f) * (1f - Mathf.Abs(x) / 30f);
				float lean = rng.RandfRange(-3f, 3f);
				var blade = new Line2D
				{
					Points = new[] { new Vector2(x, 0f), new Vector2(x + lean * 0.5f, -h * 0.55f), new Vector2(x + lean, -h) },
					Width = rng.RandfRange(1.2f, 2f),
					DefaultColor = _grassColor.Lightened(rng.RandfRange(-0.1f, 0.18f)),
				};
				blade.WidthCurve = BladeCurve();
				_grass.AddChild(blade);
			}
			_visual.AddChild(_grass);
		}
	}

	// One serrated half-ring hinged at the centre: upright (rotation 0) it's half of the closed
	// trap; flattened by JawScale it lies in the grass.
	private static Node2D BuildJaw(float side, Color iron, Color bright)
	{
		var jaw = new Node2D();
		const int segments = 8;
		const float radius = 14f;
		var outer = new Vector2[segments + 1];
		var teeth = new System.Collections.Generic.List<Vector2>();
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Lerp(Mathf.Pi, Mathf.Pi * 1.5f, i / (float)segments);
			var p = new Vector2(Mathf.Cos(a) * radius * -side * -1f, Mathf.Sin(a) * radius);
			p.X = side < 0 ? -Mathf.Abs(p.X) : Mathf.Abs(p.X);
			outer[i] = p;
		}
		// Teeth: small triangles pointing inward (towards the other jaw) along the arc.
		for (int i = 1; i < segments; i++)
		{
			Vector2 p = outer[i];
			Vector2 inward = (Vector2.Zero - p).Normalized();
			Vector2 tangent = new(-inward.Y, inward.X);
			teeth.Add(p + tangent * 1.6f);
			teeth.Add(p + inward * 3.4f);
			teeth.Add(p - tangent * 1.6f);
		}
		jaw.AddChild(new Line2D { Points = outer, Width = 2.6f, DefaultColor = iron, JointMode = Line2D.LineJointMode.Round });
		for (int t = 0; t + 2 < teeth.Count; t += 3)
			jaw.AddChild(new Polygon2D { Polygon = new[] { teeth[t], teeth[t + 1], teeth[t + 2] }, Color = bright });
		jaw.AddChild(new Line2D { Points = outer, Width = 0.8f, DefaultColor = bright with { A = 0.6f } });
		return jaw;
	}

	private void SnapJaws(float openness, float time, Tween.TransitionType trans = Tween.TransitionType.Back)
	{
		Tween tween = CreateTween();
		tween.TweenProperty(_leftJaw, "scale", JawScale(openness), time).SetTrans(trans).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(_rightJaw, "scale", JawScale(openness), time).SetTrans(trans).SetEase(Tween.EaseType.Out);
	}

	private static Vector2 JawScale(float openness) => new(1f + 0.15f * openness, 1f - 0.8f * openness);

	private void Twinkle()
	{
		_glint.Rotation = _rng.RandfRange(-0.3f, 0.3f);
		Tween tween = CreateTween();
		tween.TweenProperty(_glint, "modulate:a", 0.9f, 0.12f);
		tween.TweenProperty(_glint, "modulate:a", 0f, 0.35f);
	}

	private void RustleGrass(float strength)
	{
		if (_grass is null)
			return;
		Tween tween = CreateTween();
		tween.TweenProperty(_grass, "skew", 0.35f * strength, 0.06f);
		tween.TweenProperty(_grass, "skew", -0.2f * strength, 0.1f);
		tween.TweenProperty(_grass, "skew", 0f, 0.25f);
	}

	private void SpawnSparks()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var sparks = new CpuParticles2D
		{
			Texture = Dot,
			Amount = 10,
			Lifetime = 0.3f,
			OneShot = true,
			Explosiveness = 1f,
			Direction = Vector2.Up,
			Spread = 70f,
			Gravity = new Vector2(0f, 600f),
			InitialVelocityMin = 80f,
			InitialVelocityMax = 180f,
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.06f,
			Color = new Color(1f, 0.85f, 0.55f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			LocalCoords = false,
		};
		scene.AddChild(sparks);
		sparks.GlobalPosition = GlobalPosition + new Vector2(0f, -10f * Size);
		sparks.Emitting = true;
		sparks.GetTree().CreateTimer(0.6f).Timeout += sparks.QueueFree;
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

	private static Curve BladeCurve()
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0.1f));
		return curve;
	}
}
