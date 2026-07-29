using Godot;

namespace Metroidvania.World;

public enum CascadaCollision
{
	None,
	Hazard,
	Swimmable,
}

// Reusable procedural waterfall — same noise-driven ProceduralWater.gdshader as ProceduralWater.cs,
// but with has_organic_edges/has_rock_flecks/has_base_foam turned on by default so the silhouette
// isn't a dead-straight rectangle: the sides bulge and narrow unevenly (like squeezed through a
// rock channel), occasional darker patches read as exposed rock behind the flow, and a jittering
// foam band lands at the bottom like it's hitting a pool. Use ProceduralWater directly instead for
// a flat pool/lava surface — this one is specifically for the vertical fall.
// CollisionMode picks None (pure background decoration — no Area2D at all), Hazard (InstantKill/
// Damage/KnockbackForce, same as ProceduralWater's default), or Swimmable (a Water zone the player
// can swim through, gated by the Swim ability).
// [Tool] so it previews live in the editor (the shader's TIME-driven flow/edges animate there too).
[Tool]
public partial class Cascada : Node2D
{
	private const string ShaderPath = "res://resources/shaders/ProceduralWater.gdshader";

	private int _width = 48;

	[Export(PropertyHint.Range, "8,2000,1")]
	public int Width
	{
		get => _width;
		set { _width = Mathf.Max(8, value); Rebuild(); }
	}

	private int _height = 280;

	[Export(PropertyHint.Range, "8,2000,1")]
	public int Height
	{
		get => _height;
		set { _height = Mathf.Max(8, value); Rebuild(); }
	}

	private float _waveSpeed = 1.4f;

	[Export]
	public float WaveSpeed
	{
		get => _waveSpeed;
		set { _waveSpeed = value; Rebuild(); }
	}

	private float _waveFrequency = 10f;

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

	[Export(PropertyHint.Range, "1,16,1")]
	public float PixelBlockSize
	{
		get => _pixelBlockSize;
		set { _pixelBlockSize = Mathf.Max(1f, value); Rebuild(); }
	}

	private int _colorSteps = 5;

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

	private bool _hasOrganicEdges = true;

	// The whole point of Cascada over plain ProceduralWater — off only lets you compare against a
	// straight-edged rectangle, or if a specific spot genuinely wants one.
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

	// Slow drift over time — 0 freezes the silhouette (a fixed rock channel); small values give it
	// a subtle living wobble without reading as an animated wave.
	[Export]
	public float EdgeJitterSpeed
	{
		get => _edgeJitterSpeed;
		set { _edgeJitterSpeed = value; Rebuild(); }
	}

	private bool _hasRockFlecks = true;

	[Export]
	public bool HasRockFlecks
	{
		get => _hasRockFlecks;
		set { _hasRockFlecks = value; Rebuild(); }
	}

	private float _rockFleckDensity = 0.12f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float RockFleckDensity
	{
		get => _rockFleckDensity;
		set { _rockFleckDensity = Mathf.Clamp(value, 0f, 1f); Rebuild(); }
	}

	private Color _rockFleckColor = new(0.15f, 0.17f, 0.2f);

	[Export]
	public Color RockFleckColor
	{
		get => _rockFleckColor;
		set { _rockFleckColor = value; Rebuild(); }
	}

	private bool _hasBaseFoam = true;

	// On by default — a fall reads wrong without a splash where it lands.
	[Export]
	public bool HasBaseFoam
	{
		get => _hasBaseFoam;
		set { _hasBaseFoam = value; Rebuild(); }
	}

	private float _baseFoamThickness = 8f;

	[Export]
	public float BaseFoamThickness
	{
		get => _baseFoamThickness;
		set { _baseFoamThickness = Mathf.Max(0f, value); Rebuild(); }
	}

	private float _baseFoamTurbulence = 12f;

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

	private CascadaCollision _collisionMode = CascadaCollision.Hazard;

	[Export]
	public CascadaCollision CollisionMode
	{
		get => _collisionMode;
		set { _collisionMode = value; Rebuild(); }
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

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		// Width/Height are the only intended way to size this — see ProceduralWater's identical
		// note on why Scale gets reset here instead of trusted.
		Scale = Vector2.One;

		foreach (Node child in GetChildren())
			child.Free();

		var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		image.SetPixel(0, 0, Colors.White);
		var texture = ImageTexture.CreateFromImage(image);

		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		material.SetShaderParameter("wave_speed", _waveSpeed);
		material.SetShaderParameter("wave_frequency", _waveFrequency);
		material.SetShaderParameter("shallow_color", _shallowColor);
		material.SetShaderParameter("deep_color", _deepColor);
		material.SetShaderParameter("foam_threshold", 0.82f);
		material.SetShaderParameter("foam_color", Colors.White);
		material.SetShaderParameter("opacity_multiplier", _opacity);
		material.SetShaderParameter("shape_size", new Vector2(_width, _height));
		material.SetShaderParameter("pixel_block_size", _pixelBlockSize);
		material.SetShaderParameter("color_steps", (float)_colorSteps);
		material.SetShaderParameter("has_organic_edges", _hasOrganicEdges);
		material.SetShaderParameter("edge_jitter_amplitude", _edgeJitterAmplitude);
		material.SetShaderParameter("edge_jitter_frequency", _edgeJitterFrequency);
		material.SetShaderParameter("edge_jitter_speed", _edgeJitterSpeed);
		material.SetShaderParameter("has_rock_flecks", _hasRockFlecks);
		material.SetShaderParameter("rock_fleck_density", _rockFleckDensity);
		material.SetShaderParameter("rock_fleck_color", _rockFleckColor);
		material.SetShaderParameter("has_base_foam", _hasBaseFoam);
		material.SetShaderParameter("base_foam_thickness", _baseFoamThickness);
		material.SetShaderParameter("base_foam_turbulence", _baseFoamTurbulence);
		material.SetShaderParameter("base_foam_color", _baseFoamColor);

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

		switch (_collisionMode)
		{
			case CascadaCollision.Swimmable:
				Water.CreateArea(this, new Vector2(_width, _height));
				break;
			case CascadaCollision.Hazard:
				Hazard hazard = Hazard.CreateArea(this, _instantKill, _damage, _knockbackForce);
				var hazardShape = new CollisionShape2D
				{
					Name = "CollisionShape2D",
					Position = new Vector2(_width / 2f, _height / 2f),
					Shape = new RectangleShape2D { Size = new Vector2(_width, _height) },
				};
				hazard.AddChild(hazardShape);
				hazardShape.Owner = this;
				break;
		}

		ElementGlow.AddTo(this, new Vector2(_width / 2f, _height / 2f), LavaElement.Water);
	}
}
