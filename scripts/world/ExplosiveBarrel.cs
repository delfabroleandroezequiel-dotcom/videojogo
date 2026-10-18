using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandit-camp powder keg: a weapon the stage hands to whoever uses it first. Sits on the Enemies
// physics layer (Barril.tscn), not World: the player's AttackHitbox (mask World|Enemies) still
// finds its "Stats" child with no extra wiring (same contract as SecretWall), but nobody collides
// with it — a solid barrel parked next to a bandit pinned him in place (CanStepTo saw a wall). Any hit lights the fuse: FuseTime seconds of hissing sparks and a flashing band —
// enough to step back, or to kick it off and let it go next to the bandits — then it blows,
// hurting the player AND enemies inside Radius (knockback away from the barrel) and lighting every
// other barrel in reach a beat later, so a row of them goes off as a chain.
// Everything is drawn in code. [Tool] so the barrel previews in the editor.
[Tool]
public partial class ExplosiveBarrel : StaticBody2D
{
	[Signal] public delegate void IgnitedEventHandler();
	[Signal] public delegate void ExplodedEventHandler();

	[Export] public float Radius = 95f;
	[Export] public int PlayerDamage = 30;
	[Export] public int EnemyDamage = 60;
	[Export] public float Knockback = 520f;
	[Export] public float FuseTime = 0.5f;
	// Delay before a neighbour caught in the blast goes off itself.
	[Export] public float ChainDelay = 0.14f;
	// Settles onto whatever World geometry is under it and falls when that goes away (a burnt
	// plank, a broken floor), riding a sagging bridge. Off = stays exactly where it was placed.
	[Export] public bool Falls = true;
	[Export] public float Gravity = 1200f;

	private Color _woodColor = new(0.45f, 0.29f, 0.16f, 1f);
	[Export]
	public Color WoodColor { get => _woodColor; set { _woodColor = value; Rebuild(); } }

	private const float W = 30f;
	private const float H = 38f;
	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	private enum State { Idle, Lit, Gone }

	private State _state = State.Idle;
	private float _fallSpeed;
	private Node2D _visual;
	private Polygon2D _hazardBand;
	private Node2D _fuseSpark;
	private CpuParticles2D _fuseParticles;

	public override void _Ready()
	{
		Rebuild();
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { } collision)
		{
			collision.Shape = new RectangleShape2D { Size = new Vector2(W - 2f, H) };
			collision.Position = new Vector2(0f, -H / 2f);
		}
		if (Engine.IsEditorHint())
			return;
		AddToGroup("explosive_barrel");
		if (GetNodeOrNull<Stats>("Stats") is { } stats)
		{
			stats.MaxHealth = 1;
			stats.ResetToFull();
			stats.Died += () => Ignite();
		}
	}

	// Lights the fuse (a hit, or anything wired to it — e.g. a PressurePlate target).
	public void Ignite() => IgniteAfter(FuseTime);
	public void Activate() => IgniteAfter(FuseTime);

	public async void IgniteAfter(float fuse)
	{
		if (_state != State.Idle)
			return;
		_state = State.Lit;
		EmitSignal(SignalName.Ignited);
		Sfx.PlayAt(this, "Magic", "Spell Impact");
		_fuseSpark.Visible = true;
		_fuseParticles.Emitting = true;

		// Swells and flashes faster and faster until it goes.
		Tween flash = CreateTween().SetLoops();
		flash.TweenProperty(_hazardBand, "color", new Color(1f, 0.85f, 0.4f), 0.06f);
		flash.TweenProperty(_hazardBand, "color", new Color(0.75f, 0.12f, 0.08f), 0.06f);
		Tween swell = CreateTween();
		swell.TweenProperty(_visual, "scale", new Vector2(1.08f, 1.05f), fuse).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		Tween shake = CreateTween().SetLoops();
		shake.TweenProperty(_visual, "position:x", 1f, 0.03f);
		shake.TweenProperty(_visual, "position:x", -1f, 0.03f);

		await ToSignal(GetTree().CreateTimer(fuse), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		flash.Kill();
		shake.Kill();
		Explode();
	}

	// Hand-rolled gravity (it's a StaticBody2D on the Enemies layer so nothing collides with it):
	// a short ray down onto World each frame — snap onto the surface if it's within this frame's
	// fall, otherwise keep falling.
	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || !Falls || _state == State.Gone)
			return;
		float dt = (float)delta;
		float step = Mathf.Max(_fallSpeed * dt, 1f);
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition + new Vector2(0f, -6f), GlobalPosition + new Vector2(0f, step + 2f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			// Only move when the surface actually moved: re-setting a StaticBody2D's transform every
			// frame resets its area overlaps, and the boulder's crush area never saw a resting barrel.
			float surfaceY = ((Vector2)hit["position"]).Y;
			if (Mathf.Abs(surfaceY - GlobalPosition.Y) > 0.05f)
				GlobalPosition = new Vector2(GlobalPosition.X, surfaceY);
			_fallSpeed = 0f;
			return;
		}
		_fallSpeed += Gravity * dt;
		GlobalPosition += new Vector2(0f, _fallSpeed * dt);
	}

	private void Explode()
	{
		_state = State.Gone;
		EmitSignal(SignalName.Exploded);
		Vector2 center = GlobalPosition + new Vector2(0f, -H / 2f);
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { } collision)
			collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_visual.Visible = false;

		Sfx.PlayAt(this, "Magic", "Fireball");
		GroundSlamFx.Impact(this, GlobalPosition, Radius / 80f);
		SpawnFireball(center);
		SpawnStaves(center);

		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is not Node2D player || !InBlast(player, center))
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable && PlayerDamage > 0)
				stats.TakeDamage(PlayerDamage);
			if (player.HasMethod("ApplyKnockback"))
				player.Call("ApplyKnockback", BlastDirection(player, center), Knockback);
		}
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p && p.GlobalPosition.DistanceTo(center) < Radius * 4f)
			p.ShakeCamera(9f, 0.3f);

		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Node2D enemy && InBlast(enemy, center))
				ProjectileReflect.TryHitEnemy(enemy, EnemyDamage, BlastDirection(enemy, center), Knockback);
		}

		foreach (Node node in GetTree().GetNodesInGroup("explosive_barrel"))
		{
			if (node is ExplosiveBarrel other && other != this && other._state == State.Idle
				&& other.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
				other.IgniteAfter(ChainDelay);
		}

		// Wooden things in the blast (scaffolds, flammable bridges) catch fire and spread it from there.
		foreach (Node node in GetTree().GetNodesInGroup(PlankFire.FlammableGroup))
		{
			if (node is IFlammable flammable)
				flammable.IgniteAt(center, Radius * 0.8f);
		}

		GetTree().CreateTimer(1.5f).Timeout += QueueFree;
	}

	// Body centre (feet + ~half a character) inside the radius — measured to the body, not the feet,
	// so a blast at chest height doesn't miss someone standing right beside the barrel.
	private bool InBlast(Node2D body, Vector2 center) =>
		(body.GlobalPosition + new Vector2(0f, -20f)).DistanceTo(center) <= Radius;

	private static Vector2 BlastDirection(Node2D body, Vector2 center)
	{
		float side = Mathf.Sign(body.GlobalPosition.X - center.X);
		if (side == 0f)
			side = 1f;
		return new Vector2(side, -0.55f).Normalized();
	}

	// ── Visual ──────────────────────────────────────────────────────────────────────────────────

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;
		_visual?.QueueFree();
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);

		Color wood = _woodColor;
		Color dark = wood.Darkened(0.45f);
		Color light = wood.Lightened(0.22f);
		var iron = new Color(0.22f, 0.22f, 0.25f, 1f);

		// Body: a slightly bulging barrel silhouette.
		var outline = new System.Collections.Generic.List<Vector2>();
		const int steps = 10;
		for (int i = 0; i <= steps; i++)
		{
			float t = i / (float)steps;
			float bulge = Mathf.Sin(t * Mathf.Pi) * 3f;
			outline.Add(new Vector2(-W / 2f + 2f - bulge, -t * H));
		}
		for (int i = steps; i >= 0; i--)
		{
			float t = i / (float)steps;
			float bulge = Mathf.Sin(t * Mathf.Pi) * 3f;
			outline.Add(new Vector2(W / 2f - 2f + bulge, -t * H));
		}
		_visual.AddChild(new Polygon2D { Polygon = outline.ToArray(), Color = wood });

		// Staves: vertical seams following the bulge, outer ones shaded.
		for (int s = -2; s <= 2; s++)
		{
			float x = s * 5.2f;
			var seam = new Vector2[steps + 1];
			for (int i = 0; i <= steps; i++)
			{
				float t = i / (float)steps;
				seam[i] = new Vector2(x * (1f + Mathf.Sin(t * Mathf.Pi) * 0.18f), -t * H);
			}
			_visual.AddChild(new Line2D { Points = seam, Width = 1f, DefaultColor = dark with { A = 0.8f } });
		}
		_visual.AddChild(new Polygon2D { Polygon = new[] { new Vector2(-W / 2f - 1f, 0f), new Vector2(-W / 2f + 5f, 0f), new Vector2(-W / 2f + 4f, -H), new Vector2(-W / 2f + 2f, -H) }, Color = dark with { A = 0.55f } });
		_visual.AddChild(new Line2D { Points = new[] { new Vector2(W / 2f - 6f, -4f), new Vector2(W / 2f - 5f, -H / 2f), new Vector2(W / 2f - 6f, -H + 4f) }, Width = 2f, DefaultColor = light with { A = 0.55f } });

		// Iron hoops top and bottom, and the red hazard band in the middle (flashes when lit).
		foreach (float y in new[] { -4f, -H + 4f })
			_visual.AddChild(new Line2D { Points = new[] { new Vector2(-W / 2f + 1f, y), new Vector2(W / 2f - 1f, y) }, Width = 3f, DefaultColor = iron });
		float bandY = -H / 2f;
		_hazardBand = new Polygon2D
		{
			Polygon = new[] { new Vector2(-W / 2f - 0.5f, bandY - 4f), new Vector2(W / 2f + 0.5f, bandY - 4f), new Vector2(W / 2f + 0.5f, bandY + 4f), new Vector2(-W / 2f - 0.5f, bandY + 4f) },
			Color = new Color(0.75f, 0.12f, 0.08f),
		};
		_visual.AddChild(_hazardBand);
		// Painted skull-ish warning mark: a pale diamond on the band.
		_visual.AddChild(new Polygon2D { Polygon = new[] { new Vector2(0f, bandY - 3.5f), new Vector2(3.5f, bandY), new Vector2(0f, bandY + 3.5f), new Vector2(-3.5f, bandY) }, Color = new Color(0.95f, 0.88f, 0.7f) });

		// Lid and the fuse sticking out of the bung.
		_visual.AddChild(new Polygon2D { Polygon = Ellipse(W / 2f - 2f, 2.5f, new Vector2(0f, -H), 12), Color = light.Darkened(0.1f) });
		var fusePoints = new[] { new Vector2(4f, -H - 1f), new Vector2(6f, -H - 6f), new Vector2(9f, -H - 9f) };
		_visual.AddChild(new Line2D { Points = fusePoints, Width = 1.5f, DefaultColor = new Color(0.2f, 0.18f, 0.15f) });

		_fuseSpark = new Node2D { Name = "FuseSpark", Position = fusePoints[^1], Visible = false };
		_fuseSpark.AddChild(new Sprite2D { Texture = Dot, Material = Additive, Scale = Vector2.One * 0.1f, Modulate = new Color(1f, 0.75f, 0.3f) });
		_fuseSpark.AddChild(new PointLight2D { Texture = Dot, TextureScale = 0.9f, Energy = 0.9f, Color = new Color(1f, 0.6f, 0.25f) });
		_fuseParticles = new CpuParticles2D
		{
			Texture = Dot,
			Amount = 14,
			Lifetime = 0.35f,
			Emitting = false,
			Direction = Vector2.Up,
			Spread = 80f,
			Gravity = new Vector2(0f, 300f),
			InitialVelocityMin = 40f,
			InitialVelocityMax = 110f,
			ScaleAmountMin = 0.02f,
			ScaleAmountMax = 0.045f,
			Color = new Color(1f, 0.8f, 0.4f),
			Material = Additive,
		};
		_fuseSpark.AddChild(_fuseParticles);
		_visual.AddChild(_fuseSpark);
	}

	// ── Explosion FX (on top of GroundSlamFx.Impact) ───────────────────────────────────────────

	private void SpawnFireball(Vector2 center)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var ball = new Node2D { GlobalPosition = center, ZIndex = 5 };
		scene.AddChild(ball);
		ball.GlobalPosition = center;
		float r = Radius / 95f;
		var core = new Sprite2D { Texture = Dot, Material = Additive, Scale = Vector2.One * 0.4f * r, Modulate = new Color(1f, 0.95f, 0.75f) };
		var fire = new Sprite2D { Texture = Dot, Material = Additive, Scale = Vector2.One * 0.7f * r, Modulate = new Color(1f, 0.45f, 0.1f) };
		ball.AddChild(fire);
		ball.AddChild(core);
		var light = new PointLight2D { Texture = Dot, TextureScale = 4f * r, Energy = 2.4f, Color = new Color(1f, 0.6f, 0.3f) };
		ball.AddChild(light);
		Tween tween = ball.CreateTween().SetParallel();
		tween.TweenProperty(fire, "scale", Vector2.One * 2.1f * r, 0.32f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(core, "scale", Vector2.One * 1.1f * r, 0.18f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fire, "modulate:a", 0f, 0.4f).SetDelay(0.08f);
		tween.TweenProperty(core, "modulate:a", 0f, 0.22f);
		tween.TweenProperty(light, "energy", 0f, 0.5f);
		tween.Chain().TweenCallback(Callable.From(ball.QueueFree));

		// Black smoke rolling up after the flash.
		var smoke = new CpuParticles2D
		{
			Texture = Dot,
			Amount = 18,
			Lifetime = 1.1f,
			OneShot = true,
			Explosiveness = 0.85f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 18f * r,
			Direction = Vector2.Up,
			Spread = 60f,
			Gravity = new Vector2(0f, -60f),
			InitialVelocityMin = 30f,
			InitialVelocityMax = 90f,
			DampingMin = 30f,
			DampingMax = 60f,
			ScaleAmountMin = 0.25f * r,
			ScaleAmountMax = 0.5f * r,
			Color = new Color(0.12f, 0.1f, 0.1f, 0.55f),
			ColorRamp = new Gradient { Colors = new[] { new Color(0.15f, 0.12f, 0.1f, 0.6f), new Color(0.1f, 0.1f, 0.1f, 0f) }, Offsets = new[] { 0f, 1f } },
			LocalCoords = false,
		};
		scene.AddChild(smoke);
		smoke.GlobalPosition = center;
		smoke.Emitting = true;
		smoke.GetTree().CreateTimer(1.6f).Timeout += smoke.QueueFree;
	}

	// Planks flung out spinning, falling with gravity, fading out.
	private void SpawnStaves(Vector2 center)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < 8; i++)
		{
			var plank = new Polygon2D
			{
				Polygon = new[] { new Vector2(-2f, -7f), new Vector2(2f, -7f), new Vector2(2.5f, 7f), new Vector2(-2.5f, 7f) },
				Color = _woodColor.Darkened(rng.RandfRange(0f, 0.35f)),
				ZIndex = 4,
			};
			scene.AddChild(plank);
			plank.GlobalPosition = center;
			Vector2 velocity = Vector2.Up.Rotated(rng.RandfRange(-1.3f, 1.3f)) * rng.RandfRange(220f, 420f);
			float spin = rng.RandfRange(-14f, 14f);
			float life = rng.RandfRange(0.7f, 1.0f);
			var tween = plank.CreateTween();
			tween.TweenMethod(Callable.From<float>(t =>
			{
				if (!IsInstanceValid(plank))
					return;
				plank.GlobalPosition = center + velocity * t + new Vector2(0f, 900f) * t * t * 0.5f;
				plank.Rotation = spin * t;
				plank.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - life * 0.6f) / (life * 0.4f), 0f, 1f));
			}), 0f, life, life);
			tween.TweenCallback(Callable.From(plank.QueueFree));
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
}
