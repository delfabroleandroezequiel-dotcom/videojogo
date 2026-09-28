using Godot;

namespace Metroidvania.World;

// Deadly thorn vine (pinchosenredadera.png) as a stretchable strip: the art tiles along a Line2D, so
// it can be any Width in a straight line, arched (Curvature), rotated (diagonal — just rotate the
// node), or follow any hand-drawn shape (ShapePoints). Touching it kills (Hazard, InstantKill by
// default). Tiny white glints keep twinkling on it as a "these are sharp" warning, and it writhes
// slightly (a slow travelling wave along the strip) so it reads as alive. The kill area follows the
// un-writhed shape and is thinner than the art (HitThickness) so grazing a leaf isn't a death.
// [Tool] so shape/size/glints/writhe preview live in the editor.
[Tool]
public partial class ThornVine : Node2D
{
	private const string TexturePath = "res://assets/sprites/deco/pinchosenredadera.png";
	private const string GlintTexturePath = "res://resources/particles/SoftDotGradient.tres";

	// Crop of the sheet, centered vertically on the vine's core band (rows 276-464, measured), so the
	// strip's centerline sits on the vine and not on the leaves below it.
	private static readonly Rect2I Crop = new(0, 98, 2172, 544);
	private static Texture2D _strip;

	private float _width = 300f;

	// Length of the straight strip (ignored when ShapePoints is set).
	[Export(PropertyHint.Range, "20,5000,1")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(20f, value); Rebuild(); }
	}

	private float _curvature;

	// Arches the straight strip: the middle rises (negative) or sags (positive) by this many pixels.
	[Export(PropertyHint.Range, "-400,400,1")]
	public float Curvature
	{
		get => _curvature;
		set { _curvature = value; Rebuild(); }
	}

	private Vector2[] _shapePoints = System.Array.Empty<Vector2>();

	// Optional free shape (local points, 2 or more): the strip follows this polyline instead of the
	// straight Width line — for zig-zags, L shapes, wrapping around a ledge, etc.
	[Export]
	public Vector2[] ShapePoints
	{
		get => _shapePoints;
		set { _shapePoints = value ?? System.Array.Empty<Vector2>(); Rebuild(); }
	}

	private float _vineScale = 0.12f;

	// Art scale: thickness of the strip and how long each repeat of the pattern is.
	[Export(PropertyHint.Range, "0.03,1,0.01")]
	public float VineScale
	{
		get => _vineScale;
		set { _vineScale = Mathf.Max(0.03f, value); Rebuild(); }
	}

	private Color _tint = Colors.White;

	[Export]
	public Color Tint
	{
		get => _tint;
		set { _tint = value; if (_line is not null) _line.DefaultColor = value; }
	}

	[ExportGroup("Damage")]
	[Export] public bool InstantKill = true;
	[Export] public int Damage = 30;
	[Export] public float KnockbackForce = 300f;
	// Thickness of the kill area around the strip's centerline (the art is ~65px tall at 0.12).
	[Export] public float HitThickness = 26f;

	[ExportGroup("Glints")]
	[Export] public bool Glints = true;
	// Glints per 100px of strip.
	[Export] public float GlintDensity = 4f;
	[Export] public float GlintLifetime = 0.7f;
	[Export] public float GlintSize = 0.18f;
	[Export] public Color GlintColor = new(1f, 1f, 1f, 0.95f);

	[ExportGroup("Writhe")]
	// Perpendicular wobble of the strip in pixels (0 = static).
	[Export] public float WritheAmplitude = 2f;
	// Wave speed and wavelength (px) of the wobble travelling along the strip.
	[Export] public float WritheSpeed = 1.5f;
	[Export] public float WritheWavelength = 180f;

	private const float SampleSpacing = 12f;

	private Line2D _line;
	private CpuParticles2D _glints;
	private Hazard _hazard;
	private Vector2[] _basePoints = System.Array.Empty<Vector2>();
	private Vector2[] _normals = System.Array.Empty<Vector2>();
	private float[] _distances = System.Array.Empty<float>();
	private float _time;

	public override void _Ready() => Rebuild();

	private static Texture2D StripTexture()
	{
		if (_strip is not null)
			return _strip;
		Image sheet = GD.Load<Texture2D>(TexturePath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		_strip = ImageTexture.CreateFromImage(sheet.GetRegion(Crop));
		return _strip;
	}

	// The path the strip follows, resampled every SampleSpacing px so the writhe bends smoothly.
	private Vector2[] BuildPath()
	{
		Vector2[] control;
		if (_shapePoints.Length >= 2)
		{
			control = _shapePoints;
		}
		else
		{
			// Straight (or arched) strip starting at this node and running right.
			int steps = Mathf.Max(2, Mathf.CeilToInt(_width / 40f));
			control = new Vector2[steps + 1];
			for (int i = 0; i <= steps; i++)
			{
				float t = (float)i / steps;
				control[i] = new Vector2(_width * t, _curvature * 4f * t * (1f - t));
			}
		}

		var points = new System.Collections.Generic.List<Vector2> { control[0] };
		for (int i = 1; i < control.Length; i++)
		{
			Vector2 from = control[i - 1];
			Vector2 to = control[i];
			int pieces = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / SampleSpacing));
			for (int k = 1; k <= pieces; k++)
				points.Add(from.Lerp(to, (float)k / pieces));
		}
		return points.ToArray();
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		_basePoints = BuildPath();
		int count = _basePoints.Length;
		_normals = new Vector2[count];
		_distances = new float[count];
		for (int i = 0; i < count; i++)
		{
			Vector2 tangent = (i < count - 1 ? _basePoints[i + 1] - _basePoints[i] : _basePoints[i] - _basePoints[i - 1]).Normalized();
			_normals[i] = new Vector2(-tangent.Y, tangent.X);
			_distances[i] = i == 0 ? 0f : _distances[i - 1] + _basePoints[i].DistanceTo(_basePoints[i - 1]);
		}

		Texture2D strip = StripTexture();
		if (_line is null)
		{
			_line = new Line2D
			{
				TextureMode = Line2D.LineTextureMode.Tile,
				TextureRepeat = TextureRepeatEnum.Enabled,
				JointMode = Line2D.LineJointMode.Round,
			};
			AddChild(_line);
		}
		_line.Texture = strip;
		_line.Width = strip.GetHeight() * _vineScale;
		_line.DefaultColor = _tint;
		ApplyWrithe();

		RebuildGlints();
		if (!Engine.IsEditorHint())
			RebuildHazard();
	}

	private void RebuildGlints()
	{
		_glints?.QueueFree();
		_glints = null;
		if (!Glints || _basePoints.Length < 2)
			return;

		float length = _distances[^1];
		// Emission points spread along the strip, jittered across its thickness (where the spikes are).
		var rng = new RandomNumberGenerator { Seed = 7 };
		int pointCount = Mathf.Max(8, Mathf.CeilToInt(length / 6f));
		var emission = new Vector2[pointCount];
		float halfSpread = StripHalfThickness();
		for (int i = 0; i < pointCount; i++)
		{
			int index = rng.RandiRange(0, _basePoints.Length - 1);
			emission[i] = _basePoints[index] + _normals[index] * rng.RandfRange(-halfSpread, halfSpread);
		}

		var fade = new Curve();
		fade.AddPoint(new Vector2(0f, 0f));
		fade.AddPoint(new Vector2(0.35f, 1f));
		fade.AddPoint(new Vector2(1f, 0f));

		_glints = new CpuParticles2D
		{
			Amount = Mathf.Max(2, Mathf.CeilToInt(length / 100f * GlintDensity)),
			Lifetime = GlintLifetime,
			Texture = GD.Load<Texture2D>(GlintTexturePath),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Points,
			EmissionPoints = emission,
			Direction = Vector2.Up,
			Spread = 0f,
			Gravity = Vector2.Zero,
			InitialVelocityMin = 0f,
			InitialVelocityMax = 0f,
			ScaleAmountMin = GlintSize * 0.6f,
			ScaleAmountMax = GlintSize,
			ScaleAmountCurve = fade,
			Color = GlintColor,
			Randomness = 1f,
			LocalCoords = true,
			ZIndex = 1,
		};
		AddChild(_glints);
	}

	// Glints sit on the spike band: roughly the core vine plus the spike tips around it.
	private float StripHalfThickness() => 250f * _vineScale;

	private void RebuildHazard()
	{
		_hazard?.QueueFree();
		_hazard = new Hazard
		{
			Name = "HazardArea",
			CollisionLayer = 0,
			CollisionMask = 2,
			InstantKill = InstantKill,
			Damage = Damage,
			KnockbackForce = KnockbackForce,
		};
		// One rotated box per ~3 samples of the (un-writhed) path.
		for (int i = 0; i < _basePoints.Length - 1; i += 3)
		{
			Vector2 from = _basePoints[i];
			Vector2 to = _basePoints[Mathf.Min(i + 3, _basePoints.Length - 1)];
			float length = from.DistanceTo(to);
			if (length < 0.5f)
				continue;
			_hazard.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = new Vector2(length + HitThickness * 0.5f, HitThickness) },
				Position = (from + to) / 2f,
				Rotation = (to - from).Angle(),
			});
		}
		AddChild(_hazard);
	}

	// Slow travelling wave, perpendicular to the strip; both ends stay attached.
	private void ApplyWrithe()
	{
		if (_line is null || _basePoints.Length == 0)
			return;
		float length = Mathf.Max(1f, _distances[^1]);
		var points = new Vector2[_basePoints.Length];
		for (int i = 0; i < points.Length; i++)
		{
			float d = _distances[i];
			float pin = Mathf.Sin(Mathf.Pi * d / length);
			float wave = Mathf.Sin(d / Mathf.Max(1f, WritheWavelength) * Mathf.Tau - _time * WritheSpeed);
			points[i] = _basePoints[i] + _normals[i] * (WritheAmplitude * wave * pin);
		}
		_line.Points = points;
	}

	public override void _Process(double delta)
	{
		if (WritheAmplitude <= 0f)
			return;
		_time += (float)delta;
		ApplyWrithe();
	}
}
