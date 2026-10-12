using Godot;

namespace Metroidvania.World;

// Procedural neon look for an Elevator (AscensorNeon.tscn) — same kit as EnergyBarrier, no sprite
// art and nothing solid-drawn: the car is a slab of living energy (EnergyBarrier.gdshader laid
// sideways, beams flowing along its width), hung from a glowing orb at the shaft's anchor by an
// energy tether (same shader, upright) that is re-stretched every frame exactly like Elevator's
// RopeExtend. Everything brightens and crackles while the car is travelling.
// Must be a direct child of the Elevator; purely visual — collision and travel stay in Elevator.cs
// and the scene's own CollisionShape2D/TriggerZone. [Tool] so it previews live in the editor.
[Tool]
public partial class AscensorNeonVisual : Node2D
{
	private const string ShaderPath = "res://resources/shaders/EnergyBarrier.gdshader";
	private const string SparkProcessPath = "res://resources/particles/LightningSparkProcess.tres";
	private const string SoftDotPath = "res://resources/particles/SoftDotGradient.tres";
	private const string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";

	// Must match the shader's pad_px default — each field rect is this much wider on each side.
	private const float FieldPad = 14f;
	private const float SoftDotSize = 64f;

	private float _width = 96f;

	// Visual width of the car. Keep it in step with the scene's CollisionShape2D (96 by default).
	[Export(PropertyHint.Range, "32,400,1")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); Rebuild(); }
	}

	private float _carHeight = 12f;

	[Export(PropertyHint.Range, "6,40,1")]
	public float CarHeight
	{
		get => _carHeight;
		set { _carHeight = Mathf.Max(6f, value); Rebuild(); }
	}

	private Color _neonColor = new(0.42f, 0.25f, 0.13f);

	[Export]
	public Color NeonColor
	{
		get => _neonColor;
		set { _neonColor = value; Rebuild(); }
	}

	private float _intensity = 1.0f;

	[Export(PropertyHint.Range, "0.2,5,0.05")]
	public float Intensity
	{
		get => _intensity;
		set { _intensity = value; Rebuild(); }
	}

	private int _carBeams = 3;

	// Parallel beams running along the car's width — more reads as a denser slab.
	[Export(PropertyHint.Range, "1,8,1")]
	public int CarBeams
	{
		get => _carBeams;
		set { _carBeams = Mathf.Clamp(value, 1, 8); Rebuild(); }
	}

	private int _tetherBeams = 2;

	[Export(PropertyHint.Range, "1,4,1")]
	public int TetherBeams
	{
		get => _tetherBeams;
		set { _tetherBeams = Mathf.Clamp(value, 1, 4); Rebuild(); }
	}

	private float _tetherThickness = 10f;

	[Export(PropertyHint.Range, "4,40,1")]
	public float TetherThickness
	{
		get => _tetherThickness;
		set { _tetherThickness = Mathf.Max(4f, value); Rebuild(); }
	}

	[Export] public float LightEnergy = 0.7f;

	// How much brighter (and more flickery) everything gets while the car is travelling (0 = no change).
	[Export(PropertyHint.Range, "0,3,0.05")] public float MovingBoost = 0.6f;

	private Elevator _elevator;
	private Node2D _anchor;
	private ColorRect _tether;
	private ShaderMaterial _tetherMaterial;
	private ShaderMaterial _carMaterial;
	private Node2D _car;
	private PointLight2D _carLight;
	private float _lastElevatorY;
	private bool _hasLastY;
	private float _moving;

	public override void _Ready()
	{
		_elevator = GetParentOrNull<Elevator>();
		Rebuild();
	}

	public override void _Process(double delta) => UpdateTether((float)delta);

	private void UpdateTether(float delta)
	{
		if (_elevator is null || _tether is null)
			return;

		// Anchor in this node's local space (Elevator.AnchorY shares the elevator's parent space).
		float anchorLocal = _elevator.AnchorY - _elevator.Position.Y - Position.Y;
		float bottom = -_carHeight / 2f;
		float length = bottom - anchorLocal;
		bool show = length > 1f;

		_tether.Visible = show;
		_anchor.Visible = show;
		_anchor.Position = new Vector2(0f, anchorLocal);
		if (show)
		{
			_tether.Position = new Vector2(-_tetherThickness / 2f - FieldPad, anchorLocal);
			_tether.Size = new Vector2(_tetherThickness + FieldPad * 2f, length);
			_tetherMaterial.SetShaderParameter("length_px", length);
		}

		float y = _elevator.Position.Y;
		bool travelling = _hasLastY && delta > 0f && Mathf.Abs(y - _lastElevatorY) / delta > 1f;
		_lastElevatorY = y;
		_hasLastY = true;
		_moving = Mathf.MoveToward(_moving, travelling ? 1f : 0f, delta * 4f);

		float boost = 1f + MovingBoost * _moving;
		float surge = 0.35f * MovingBoost * _moving;
		_tetherMaterial.SetShaderParameter("intensity", _intensity * boost);
		_tetherMaterial.SetShaderParameter("surge", surge);
		_carMaterial.SetShaderParameter("intensity", _intensity * boost);
		_carMaterial.SetShaderParameter("surge", surge);
		_car.Modulate = new Color(boost, boost, boost);
		_carLight.Energy = LightEnergy * boost;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (string name in new[] { "Tether", "Anchor", "Car" })
		{
			if (GetNodeOrNull(name) is Node existing)
			{
				RemoveChild(existing);
				existing.QueueFree();
			}
		}

		Color color = _neonColor;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var softDot = GD.Load<Texture2D>(SoftDotPath);
		var lightTexture = GD.Load<Texture2D>(LightTexturePath);
		var sparkProcess = (ParticleProcessMaterial)GD.Load<ParticleProcessMaterial>(SparkProcessPath).Duplicate();

		_tetherMaterial = NewFieldMaterial(color, _tetherBeams, _tetherThickness, 0f);
		_tether = new ColorRect
		{
			Name = "Tether",
			Material = _tetherMaterial,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		AddChild(_tether);

		_anchor = BuildAnchor(color, additive, softDot, lightTexture, sparkProcess);
		AddChild(_anchor);

		_car = BuildCar(color, additive, softDot, lightTexture, sparkProcess);
		AddChild(_car);

		UpdateTether(0f);
	}

	private ShaderMaterial NewFieldMaterial(Color color, int beams, float thickness, float seedOffset)
	{
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		material.SetShaderParameter("glow_color", color);
		material.SetShaderParameter("intensity", _intensity);
		material.SetShaderParameter("beam_count", beams);
		material.SetShaderParameter("thickness_px", thickness);
		material.SetShaderParameter("seed", Mathf.PosMod(GlobalPosition.X * 0.0137f + GlobalPosition.Y * 0.0071f + seedOffset, 1f));
		material.SetShaderParameter("dissolve", 0f);
		return material;
	}

	// Fixed glowing orb at the shaft's anchor where the tether hangs from: hot core, soft halo,
	// a few sparks and a small light — no solid housing.
	private Node2D BuildAnchor(Color color, Material additive, Texture2D softDot, Texture2D lightTexture, ParticleProcessMaterial sparkProcess)
	{
		var anchor = new Node2D { Name = "Anchor", Visible = false };
		float size = _tetherThickness + 10f;

		anchor.AddChild(new Sprite2D
		{
			Material = additive,
			Texture = softDot,
			Modulate = new Color(color, 0.6f),
			Scale = Vector2.One * size * 2.2f / SoftDotSize,
		});
		anchor.AddChild(new Sprite2D
		{
			Material = additive,
			Texture = softDot,
			Modulate = color.Lightened(0.5f) * 1.8f,
			Scale = Vector2.One * size * 0.6f / SoftDotSize,
		});
		anchor.AddChild(new GpuParticles2D
		{
			Material = additive,
			Amount = 4,
			Lifetime = 0.45,
			Randomness = 0.7f,
			Texture = softDot,
			ProcessMaterial = sparkProcess,
			Scale = new Vector2(0.2f, 0.2f),
			Modulate = color.Lightened(0.3f),
		});
		anchor.AddChild(new PointLight2D
		{
			Texture = lightTexture,
			Color = color,
			Energy = LightEnergy * 0.6f,
			TextureScale = 90f / lightTexture.GetWidth(),
		});
		return anchor;
	}

	// The car, centered on the elevator's origin (matches the scene's CollisionShape2D): the field
	// shader authored upright and turned sideways so its beams flow along the width, glowing nodes
	// at both ends, sparks where the tether joins and dripping off the underside, plus a halo and a
	// light that ride along.
	private Node2D BuildCar(Color color, Material additive, Texture2D softDot, Texture2D lightTexture, ParticleProcessMaterial sparkProcess)
	{
		var car = new Node2D { Name = "Car" };
		float hw = _width / 2f;
		float hh = _carHeight / 2f;

		car.AddChild(new Sprite2D
		{
			Material = additive,
			Texture = softDot,
			Modulate = new Color(color, 0.35f),
			Scale = new Vector2(_width / SoftDotSize * 1.4f, _carHeight / SoftDotSize * 3.5f),
		});

		var slab = new Node2D { Rotation = -Mathf.Pi / 2f };
		_carMaterial = NewFieldMaterial(color, _carBeams, _carHeight, 0.37f);
		_carMaterial.SetShaderParameter("length_px", _width);
		slab.AddChild(new ColorRect
		{
			Material = _carMaterial,
			Position = new Vector2(-hh - FieldPad, -hw),
			Size = new Vector2(_carHeight + FieldPad * 2f, _width),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		car.AddChild(slab);

		foreach (float x in new[] { -hw, hw })
		{
			car.AddChild(new Sprite2D
			{
				Material = additive,
				Texture = softDot,
				Modulate = color.Lightened(0.45f) * 1.6f,
				Scale = Vector2.One * _carHeight * 1.1f / SoftDotSize,
				Position = new Vector2(x, 0f),
			});
		}

		car.AddChild(new GpuParticles2D
		{
			Material = additive,
			Amount = 4,
			Lifetime = 0.4,
			Randomness = 0.7f,
			Texture = softDot,
			ProcessMaterial = sparkProcess,
			Scale = new Vector2(0.2f, 0.2f),
			Position = new Vector2(0f, -hh),
			Modulate = color.Lightened(0.3f),
		});

		var dripProcess = (ParticleProcessMaterial)sparkProcess.Duplicate();
		dripProcess.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		// Particles are scaled 0.2 below, so the box is 5x the car's half-width.
		dripProcess.EmissionBoxExtents = new Vector3(hw * 5f * 0.85f, 2f, 0f);
		dripProcess.Direction = new Vector3(0f, 1f, 0f);
		dripProcess.Spread = 25f;
		dripProcess.InitialVelocityMin = 20f;
		dripProcess.InitialVelocityMax = 80f;
		car.AddChild(new GpuParticles2D
		{
			Material = additive,
			Amount = 8,
			Lifetime = 0.5,
			Randomness = 0.8f,
			Texture = softDot,
			ProcessMaterial = dripProcess,
			Scale = new Vector2(0.2f, 0.2f),
			Position = new Vector2(0f, hh),
			Modulate = new Color(color.Lightened(0.2f), 0.8f),
		});

		_carLight = new PointLight2D
		{
			Texture = lightTexture,
			Color = color,
			Energy = LightEnergy,
			TextureScale = _width * 2f / lightTexture.GetWidth(),
		};
		car.AddChild(_carLight);
		return car;
	}
}
