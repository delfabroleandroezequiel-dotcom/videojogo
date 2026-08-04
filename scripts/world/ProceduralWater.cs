using Godot;

namespace Metroidvania.World;

// Fully procedural water hazard (layered scrolling noise + foam threshold via
// ProceduralWater.gdshader) instead of hand-painted sprite frames from a texture sheet — no
// crop/seam-matching at all since nothing is sampled from a texture, at the cost of a
// smoother/less "painted" look unless deliberately quantized later.
// Sized freely (Width/Height in pixels) instead of stacking fixed-size tiles, since there's no
// source art size to respect — a wide/short rectangle plus HasWavyTop (see below) doubles as a
// sea/pool surface, same technique either way.
// A single Hazard (InstantKill by default) covers the whole rectangle, unless IsSwimmable is on
// (see below), in which case it's a Water zone the player can actually swim through instead.
// Plus the same ambient ElementGlow LavaFall/LavaFloor use, tinted via Element same as those —
// flip it to Lava for the glow and a baked fire palette/flow speed (see the Lava* consts near
// Rebuild), same "one flag, no manual retuning" deal LavaFall gets from FlowingLiquid.gdshader's
// is_lava. ShallowColor/DeepColor/etc. stay in the Inspector but are ignored while Element is
// Lava, same as WaveSpeed/WaveFrequency.
// [Tool] so it previews live in the editor (the shader's TIME-driven flow animates there too).
[Tool]
public partial class ProceduralWater : Node2D
{
	private const string ShaderPath = "res://resources/shaders/ProceduralWater.gdshader";

	private int _width = 64;

	[Export(PropertyHint.Range, "8,2000,1")]
	public int Width
	{
		get => _width;
		set { _width = Mathf.Max(8, value); Rebuild(); }
	}

	private int _height = 200;

	[Export(PropertyHint.Range, "8,2000,1")]
	public int Height
	{
		get => _height;
		set { _height = Mathf.Max(8, value); Rebuild(); }
	}

	private float _waveSpeed = 1f;

	[Export]
	public float WaveSpeed
	{
		get => _waveSpeed;
		set { _waveSpeed = value; Rebuild(); }
	}

	private float _waveFrequency = 8f;

	// How many noise cells fit across the shape — higher reads as choppier/smaller ripples.
	[Export]
	public float WaveFrequency
	{
		get => _waveFrequency;
		set { _waveFrequency = value; Rebuild(); }
	}

	private float _opacity = 0.85f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Opacity
	{
		get => _opacity;
		set { _opacity = Mathf.Clamp(value, 0f, 1f); Rebuild(); }
	}

	private float _pixelBlockSize = 3f;

	// Real pixels per pixelation block — snaps the noise to a coarse grid instead of a smooth
	// blur, so it reads closer to the rest of the game's pixel art.
	[Export(PropertyHint.Range, "1,16,1")]
	public float PixelBlockSize
	{
		get => _pixelBlockSize;
		set { _pixelBlockSize = Mathf.Max(1f, value); Rebuild(); }
	}

	private int _colorSteps = 5;

	// Posterizes the flow into this many discrete color bands instead of a continuous gradient —
	// the other half of reading as pixel art instead of a shader gradient.
	[Export(PropertyHint.Range, "2,16,1")]
	public int ColorSteps
	{
		get => _colorSteps;
		set { _colorSteps = Mathf.Max(2, value); Rebuild(); }
	}

	private Color _shallowColor = new(0.55f, 0.88f, 0.95f);
	private Color _deepColor = new(0.05f, 0.28f, 0.48f);

	[Export]
	public Color ShallowColor
	{
		get => _shallowColor;
		set { _shallowColor = value; Rebuild(); }
	}

	[Export]
	public Color DeepColor
	{
		get => _deepColor;
		set { _deepColor = value; Rebuild(); }
	}

	private LavaElement _element = LavaElement.Water;

	// Swaps the ambient ElementGlow tint (blue vs orange) and, in Rebuild(), the shader's
	// wave/color parameters to a baked fire preset — same Element idea LavaFall uses, just
	// implemented here in C# instead of shader-side since this shader has no built-in water/lava
	// gradient of its own.
	[Export]
	public LavaElement Element
	{
		get => _element;
		set { _element = value; Rebuild(); }
	}

	private bool _hasWavyTop;

	// For a sea/pool surface instead of a hard rectangle — an animated sine offset per column
	// (quantized to PixelBlockSize steps, same as the flow noise) discards everything above the
	// resulting wave line, with a bright crest band riding right at the surface.
	[Export]
	public bool HasWavyTop
	{
		get => _hasWavyTop;
		set { _hasWavyTop = value; Rebuild(); }
	}

	private float _waveEdgeAmplitude = 6f;

	// How many pixels the wavy top bobs up/down from its resting line.
	[Export]
	public float WaveEdgeAmplitude
	{
		get => _waveEdgeAmplitude;
		set { _waveEdgeAmplitude = Mathf.Max(0f, value); Rebuild(); }
	}

	private float _waveEdgeFrequency = 2f;

	// How many waves fit across Width.
	[Export]
	public float WaveEdgeFrequency
	{
		get => _waveEdgeFrequency;
		set { _waveEdgeFrequency = value; Rebuild(); }
	}

	private float _waveEdgeSpeed = 1f;

	[Export]
	public float WaveEdgeSpeed
	{
		get => _waveEdgeSpeed;
		set { _waveEdgeSpeed = value; Rebuild(); }
	}

	private float _crestThickness = 3f;

	[Export]
	public float CrestThickness
	{
		get => _crestThickness;
		set { _crestThickness = Mathf.Max(0f, value); Rebuild(); }
	}

	private Color _crestColor = Colors.White;

	[Export]
	public Color CrestColor
	{
		get => _crestColor;
		set { _crestColor = value; Rebuild(); }
	}

	private bool _hasBaseFoam;

	// For a vertical fall's tip landing on a pool below (e.g. sitting right above a swimmable
	// ProceduralWater) — a jagged, animated foam band at the bottom edge, same idea as
	// HasWavyTop's crest but turbulent instead of a clean wave line, since it's meant to read as
	// impact spray, not a calm surface.
	[Export]
	public bool HasBaseFoam
	{
		get => _hasBaseFoam;
		set { _hasBaseFoam = value; Rebuild(); }
	}

	private float _baseFoamThickness = 5f;

	[Export]
	public float BaseFoamThickness
	{
		get => _baseFoamThickness;
		set { _baseFoamThickness = Mathf.Max(0f, value); Rebuild(); }
	}

	private float _baseFoamTurbulence = 10f;

	// How far the foam band's edge jitters up/down from its resting thickness — higher reads as
	// choppier, more broken-up spray.
	[Export]
	public float BaseFoamTurbulence
	{
		get => _baseFoamTurbulence;
		set { _baseFoamTurbulence = Mathf.Max(0f, value); Rebuild(); }
	}

	private Color _baseFoamColor = Colors.White;

	[Export]
	public Color BaseFoamColor
	{
		get => _baseFoamColor;
		set { _baseFoamColor = value; Rebuild(); }
	}

	private bool _hasOrganicEdges;

	// Off by default (a flat pool/lava rectangle is the point of this class) — turn on per
	// instance for a body of water that shouldn't read as a hard-edged tank, same wobble Cascada
	// uses by default for its waterfalls.
	[Export]
	public bool HasOrganicEdges
	{
		get => _hasOrganicEdges;
		set { _hasOrganicEdges = value; Rebuild(); }
	}

	private float _edgeJitterAmplitude = 6f;

	// How many pixels the left/right edges can inset from Width at their most narrow.
	[Export]
	public float EdgeJitterAmplitude
	{
		get => _edgeJitterAmplitude;
		set { _edgeJitterAmplitude = Mathf.Max(0f, value); Rebuild(); }
	}

	private float _edgeJitterFrequency = 3f;

	// How many bulge/narrow bumps fit down Height.
	[Export]
	public float EdgeJitterFrequency
	{
		get => _edgeJitterFrequency;
		set { _edgeJitterFrequency = value; Rebuild(); }
	}

	private float _edgeJitterSpeed = 0.15f;

	[Export]
	public float EdgeJitterSpeed
	{
		get => _edgeJitterSpeed;
		set { _edgeJitterSpeed = value; Rebuild(); }
	}

	private bool _isSwimmable;

	// On: the player can swim through this instead of it hurting them — spawns a Water zone
	// (Player.EnterWater/ExitWater, gated by the Swim ability) instead of a Hazard.
	// InstantKill/Damage/KnockbackForce below are ignored while this is on.
	[Export]
	public bool IsSwimmable
	{
		get => _isSwimmable;
		set { _isSwimmable = value; Rebuild(); }
	}

	private bool _instantKill = true;

	[Export]
	public bool InstantKill
	{
		get => _instantKill;
		set { _instantKill = value; Rebuild(); }
	}

	private int _damage = 20;

	[Export]
	public int Damage
	{
		get => _damage;
		set { _damage = value; Rebuild(); }
	}

	private float _knockbackForce = 300f;

	[Export]
	public float KnockbackForce
	{
		get => _knockbackForce;
		set { _knockbackForce = value; Rebuild(); }
	}

	// Baked fire preset, applied instead of the exported wave/color fields whenever Element is
	// Lava — same "Element alone gives you the right look" deal LavaFall gets from
	// FlowingLiquid.gdshader's is_lava gradient, since this shader has no such built-in switch of
	// its own. Slower/lower-frequency than any sensible water setting so it reads as thick molten
	// flow instead of choppy water, on a shadow-to-white-hot gradient with a bright hot foam/crest
	// instead of white sea foam.
	private const float LavaWaveSpeed = 0.35f;
	private const float LavaWaveFrequency = 4f;
	// Deep is near-black cooled crust rather than a mid-tone, and shallow is pulled toward red and
	// off pure bright orange (first playtest read as flat orange soda — too much saturated area,
	// not enough dark rock for the bright bits to pop against) plus a tighter 3-step posterize so
	// it bands like rock layers instead of blending smoothly.
	private static readonly Color LavaShallowColor = new(0.85f, 0.35f, 0.05f);
	private static readonly Color LavaDeepColor = new(0.12f, 0.02f, 0f);
	private const float LavaColorSteps = 3f;
	private const float LavaFoamThreshold = 0.88f;
	private static readonly Color LavaFoamColor = new(1f, 0.8f, 0.2f);
	private static readonly Color LavaCrestColor = new(1f, 0.75f, 0.15f);
	private static readonly Color LavaBaseFoamColor = new(1f, 0.8f, 0.2f);

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		// Width/Height are the only intended way to size this — if this node's own Scale ever
		// got changed by accident (e.g. dragging its transform gizmo in the 2D viewport, which
		// Godot allows for any Node2D and would otherwise silently compound with Width/Height),
		// reset it here so the next property change self-corrects instead of the two fighting.
		Scale = Vector2.One;

		foreach (Node child in GetChildren())
			child.Free();

		// A 1x1 white pixel scaled up to Width x Height — the shader ignores the sampled texture
		// entirely and generates color from noise + UV, so the source texture's own resolution
		// never matters, only the sprite's on-screen size (driven by Scale here).
		var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		image.SetPixel(0, 0, Colors.White);
		var texture = ImageTexture.CreateFromImage(image);

		bool isLava = _element == LavaElement.Lava;

		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		material.SetShaderParameter("wave_speed", isLava ? LavaWaveSpeed : _waveSpeed);
		material.SetShaderParameter("wave_frequency", isLava ? LavaWaveFrequency : _waveFrequency);
		material.SetShaderParameter("shallow_color", isLava ? LavaShallowColor : _shallowColor);
		material.SetShaderParameter("deep_color", isLava ? LavaDeepColor : _deepColor);
		material.SetShaderParameter("foam_threshold", isLava ? LavaFoamThreshold : 0.82f);
		material.SetShaderParameter("foam_color", isLava ? LavaFoamColor : Colors.White);
		material.SetShaderParameter("opacity_multiplier", _opacity);
		material.SetShaderParameter("shape_size", new Vector2(_width, _height));
		material.SetShaderParameter("pixel_block_size", _pixelBlockSize);
		material.SetShaderParameter("color_steps", isLava ? LavaColorSteps : (float)_colorSteps);
		material.SetShaderParameter("has_wavy_top", _hasWavyTop);
		material.SetShaderParameter("has_organic_edges", _hasOrganicEdges);
		material.SetShaderParameter("edge_jitter_amplitude", _edgeJitterAmplitude);
		material.SetShaderParameter("edge_jitter_frequency", _edgeJitterFrequency);
		material.SetShaderParameter("edge_jitter_speed", _edgeJitterSpeed);
		material.SetShaderParameter("wave_edge_amplitude", _waveEdgeAmplitude);
		material.SetShaderParameter("wave_edge_frequency", _waveEdgeFrequency);
		material.SetShaderParameter("wave_edge_speed", _waveEdgeSpeed);
		material.SetShaderParameter("crest_thickness", _crestThickness);
		material.SetShaderParameter("crest_color", isLava ? LavaCrestColor : _crestColor);
		material.SetShaderParameter("has_base_foam", _hasBaseFoam);
		material.SetShaderParameter("base_foam_thickness", _baseFoamThickness);
		material.SetShaderParameter("base_foam_turbulence", _baseFoamTurbulence);
		material.SetShaderParameter("base_foam_color", isLava ? LavaBaseFoamColor : _baseFoamColor);

		var sprite = new Sprite2D
		{
			Name = "Water",
			Texture = texture,
			Centered = false,
			Scale = new Vector2(_width, _height),
			Material = material,
		};
		AddChild(sprite);
		sprite.Owner = this;

		if (_isSwimmable)
		{
			Water.CreateArea(this, new Vector2(_width, _height));
		}
		else
		{
			Hazard hazard = Hazard.CreateArea(this, _instantKill, _damage, _knockbackForce);
			var hazardShape = new CollisionShape2D
			{
				Name = "CollisionShape2D",
				Position = new Vector2(_width / 2f, _height / 2f),
				Shape = new RectangleShape2D { Size = new Vector2(_width, _height) },
			};
			hazard.AddChild(hazardShape);
			hazardShape.Owner = this;
		}

		ElementGlow.AddTo(this, new Vector2(_width / 2f, _height / 2f), _element);
	}
}
