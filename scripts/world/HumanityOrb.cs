using Godot;

namespace Metroidvania.World;

// A "humanity" (Midir's phase 2): a dark orb that rises out of the ground, hangs for a moment, then
// homes in on the player with a limited turn rate — so a dash or a sharp change of direction makes it
// overshoot. It bursts on the player (OnHitPlayer), on the floor/walls (world layer 1), or when its
// time runs out. Only draws and flies; the boss deals the damage through OnHitPlayer.
public partial class HumanityOrb : Node2D
{
	public Node2D Target;
	public System.Action<Vector2> OnHitPlayer;
	public float RiseTime = 0.5f;
	public float RiseSpeed = 90f;
	public float HangTime = 0.4f;
	public float Speed = 330f;
	public float TurnRate = 2.6f;
	public float HitRadius = 24f;
	public float MaxLife = 6f;
	public Color GlowColor = new(0.55f, 0.25f, 1f, 1f);

	private Vector2 _velocity;
	private float _age;
	private bool _done;

	// Look of a Dark Souls humanity: a small dark figure — round head, teardrop body trailing into two
	// wisps — filled black, with a glowing white wispy outline, sparkles and two white eyes.
	public Color OutlineColor = new(0.92f, 0.95f, 1f, 1f);
	public float FigureScale = 1f;

	private Node2D _figure;
	private Vector2[] _headShape;
	private readonly System.Collections.Generic.List<(Line2D line, Vector2[] shape, float amp, float width)> _strands = new();
	private float _frayTimer;
	private readonly RandomNumberGenerator _rng = new();
	// Set to fling it out first (e.g. blown out of an explosion): it flies with this, slows down over
	// HangTime, then homes in. Skips the rise.
	public Vector2 LaunchVelocity;

	private void AddStrand(Vector2[] shape, bool closed, float width, Color color, float amp, Material material)
	{
		var line = new Line2D { Width = width, DefaultColor = color, Material = material, Closed = closed, Points = shape, JointMode = Line2D.LineJointMode.Round };
		_figure.AddChild(line);
		_strands.Add((line, shape, amp, width));
	}

	private void Fray()
	{
		foreach (var (line, shape, amp, width) in _strands)
		{
			var points = new Vector2[shape.Length];
			for (int i = 0; i < shape.Length; i++)
				points[i] = shape[i] + new Vector2(_rng.RandfRange(-amp, amp), _rng.RandfRange(-amp, amp));
			line.Points = points;
			line.Width = width * _rng.RandfRange(0.6f, 1.4f);
		}
	}

	private static readonly Vector2[] BodyOutline =
	{
		new(-4f, -13f), new(-11f, -8f), new(-13f, 0f), new(-11f, 10f), new(-7f, 20f), new(-4f, 30f),
		new(-2f, 36f), new(0f, 28f), new(2f, 36f), new(4f, 30f), new(7f, 20f), new(11f, 10f),
		new(13f, 0f), new(11f, -8f), new(4f, -13f),
	};

	public override void _Ready()
	{
		ZIndex = 5;
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		_figure = new Node2D { Scale = Vector2.One * FigureScale };
		AddChild(_figure);
		// Dark body and head.
		_figure.AddChild(new Polygon2D { Polygon = BodyOutline, Color = new Color(0.02f, 0.02f, 0.04f, 0.92f) });
		var headPoints = new Vector2[20];
		for (int i = 0; i < headPoints.Length; i++)
			headPoints[i] = new Vector2(0f, -23f) + Vector2.Right.Rotated(Mathf.Tau * i / headPoints.Length) * 9f;
		_figure.AddChild(new Polygon2D { Polygon = headPoints, Color = new Color(0.02f, 0.02f, 0.04f, 0.95f) });
		// Wispy outline: several glow and core strands per shape, each re-jittered on its own every few
		// hundredths of a second (Fray) so the edge frays and crawls instead of being one clean line.
		_headShape = headPoints;
		foreach (Vector2[] shape in new[] { BodyOutline, headPoints })
		{
			bool closed = shape == headPoints;
			for (int s = 0; s < 3; s++)
				AddStrand(shape, closed, 4f + s * 2.5f, OutlineColor with { A = 0.18f }, 2.6f, additive);
			for (int s = 0; s < 2; s++)
				AddStrand(shape, closed, 1.4f, OutlineColor with { A = 0.85f }, 1.3f, additive);
		}
		// Eyes.
		foreach (float x in new[] { -3.5f, 3.5f })
			_figure.AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = OutlineColor, Scale = Vector2.One * 0.09f, Position = new Vector2(x, -24f) });
		// A faint halo so it reads against the dark cave.
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = OutlineColor with { A = 0.12f }, Scale = Vector2.One * 1.2f * FigureScale });

		var ramp = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { OutlineColor, OutlineColor with { A = 0f } } };
		var trail = new GpuParticles2D
		{
			Amount = 34,
			Lifetime = 0.6f,
			LocalCoords = false,
			Texture = dot,
			Material = additive,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(11f * FigureScale, 22f * FigureScale, 1f),
				Gravity = new Vector3(0f, -30f, 0f),
				Spread = 180f,
				InitialVelocityMin = 5f,
				InitialVelocityMax = 25f,
				ScaleMin = 0.03f,
				ScaleMax = 0.07f,
				ColorRamp = new GradientTexture1D { Gradient = ramp },
			},
		};
		AddChild(trail);
		trail.Emitting = true;
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = 0.7f,
			Color = OutlineColor,
			Energy = 0.7f,
		};
		AddChild(light);
		_rng.Randomize();
		if (LaunchVelocity != Vector2.Zero)
		{
			_velocity = LaunchVelocity;
			RiseTime = 0f;
		}
		else
		{
			_velocity = Vector2.Up * RiseSpeed;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_age += dt;
		// Flickering, swaying figure; leans into where it's flying.
		if (_figure is not null)
		{
			_figure.Modulate = new Color(1f, 1f, 1f, 0.8f + 0.2f * Mathf.Sin(_age * 21f));
			_figure.Rotation = Mathf.Clamp(_velocity.X / Mathf.Max(1f, Speed), -1f, 1f) * 0.35f + Mathf.Sin(_age * 6f) * 0.06f;
		}
		_frayTimer -= dt;
		if (_frayTimer <= 0f)
		{
			_frayTimer = 0.04f;
			Fray();
		}
		if (_age < RiseTime)
		{
			GlobalPosition += _velocity * dt;
			return;
		}
		if (_age < RiseTime + HangTime)
		{
			if (LaunchVelocity != Vector2.Zero)
			{
				// Flung out: carried by the blast, slowing down (stopped by the floor/walls, not burst).
				Vector2 to = GlobalPosition + _velocity * dt;
				if (GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(GlobalPosition, to, 1)).Count > 0)
					_velocity = Vector2.Zero;
				else
					GlobalPosition = to;
				_velocity = _velocity.MoveToward(Vector2.Zero, LaunchVelocity.Length() / Mathf.Max(0.05f, HangTime) * dt);
			}
			else
			{
				// Hovering: a nervous wobble before it goes.
				GlobalPosition += new Vector2(Mathf.Sin(_age * 30f), Mathf.Cos(_age * 23f)) * 0.6f;
			}
			if (LaunchVelocity == Vector2.Zero && Target is not null && IsInstanceValid(Target))
				_velocity = (Target.GlobalPosition - GlobalPosition).Normalized() * Speed;
			return;
		}

		if (Target is not null && IsInstanceValid(Target))
		{
			Vector2 want = (Target.GlobalPosition - GlobalPosition).Normalized() * Speed;
			if (_velocity.LengthSquared() < 100f)
				_velocity = want; // came to a stop after being flung: set off straight at the target
			float angle = _velocity.AngleTo(want);
			float maxTurn = TurnRate * dt;
			_velocity = _velocity.Rotated(Mathf.Clamp(angle, -maxTurn, maxTurn)).Normalized() * Speed;
			if (GlobalPosition.DistanceTo(Target.GlobalPosition) <= HitRadius)
			{
				OnHitPlayer?.Invoke(GlobalPosition);
				Burst();
				return;
			}
		}
		Vector2 next = GlobalPosition + _velocity * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 1);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0 || _age > MaxLife)
		{
			Burst();
			return;
		}
		GlobalPosition = next;
	}

	// Pops in a violet flash and frees itself.
	public void Burst()
	{
		if (_done)
			return;
		_done = true;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector2.One * 2.2f, 0.18f);
		tween.Parallel().TweenProperty(this, "modulate:a", 0f, 0.22f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
