using Godot;

namespace Metroidvania.World;

// The dark beam's look (LugiaBoss/Midir): a violet neon glow line with a white-hot core, flickering in
// width, plus a light and a spray of sparks where it ends. Only draws — the boss moves it with Set()
// every frame and calls FadeOut() when the beam stops. Lives in the scene root (world space).
public partial class DarkBeamVisual : Node2D
{
	private readonly Color _glowColor;
	private readonly Color _coreColor;
	private Line2D _glow;
	private Line2D _core;
	private PointLight2D _endLight;
	private GpuParticles2D _sparks;
	private readonly RandomNumberGenerator _rng = new();

	public DarkBeamVisual() : this(new Color(0.62f, 0.3f, 1f), Colors.White) { }

	public DarkBeamVisual(Color glow, Color core)
	{
		_glowColor = glow;
		_coreColor = core;
	}

	public override void _Ready()
	{
		_rng.Randomize();
		ZIndex = 6;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_glow = new Line2D { Width = 30f, DefaultColor = _glowColor, Material = additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		_core = new Line2D { Width = 9f, DefaultColor = _coreColor, Material = additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		_glow.AddPoint(Vector2.Zero);
		_glow.AddPoint(Vector2.Zero);
		_core.AddPoint(Vector2.Zero);
		_core.AddPoint(Vector2.Zero);
		AddChild(_glow);
		AddChild(_core);

		_endLight = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = 1.6f,
			Color = _glowColor,
			Energy = 1.8f,
		};
		AddChild(_endLight);

		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.4f, 1f },
			Colors = new[] { _coreColor, _glowColor, _glowColor with { A = 0f } },
		};
		_sparks = new GpuParticles2D
		{
			Amount = 40,
			Lifetime = 0.45f,
			LocalCoords = false,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 70f,
				InitialVelocityMin = 120f,
				InitialVelocityMax = 320f,
				Gravity = new Vector3(0f, 400f, 0f),
				ScaleMin = 0.05f,
				ScaleMax = 0.13f,
				ColorRamp = new GradientTexture1D { Gradient = ramp },
			},
		};
		AddChild(_sparks);
		_sparks.Emitting = true;
	}

	public void Set(Vector2 from, Vector2 to)
	{
		if (_glow is null)
			return;
		_glow.SetPointPosition(0, from);
		_glow.SetPointPosition(1, to);
		_core.SetPointPosition(0, from);
		_core.SetPointPosition(1, to);
		_glow.Width = 30f * _rng.RandfRange(0.75f, 1.25f);
		_core.Width = 9f * _rng.RandfRange(0.8f, 1.2f);
		_endLight.Position = to;
		_endLight.Energy = 1.8f * _rng.RandfRange(0.8f, 1.2f);
		_sparks.Position = to;
	}

	public void FadeOut()
	{
		if (_glow is null)
		{
			QueueFree();
			return;
		}
		_sparks.Emitting = false;
		Tween tween = CreateTween();
		tween.TweenProperty(_glow, "width", 0f, 0.18f);
		tween.Parallel().TweenProperty(_core, "width", 0f, 0.18f);
		tween.Parallel().TweenProperty(_endLight, "energy", 0f, 0.25f);
		tween.TweenInterval(0.4f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
