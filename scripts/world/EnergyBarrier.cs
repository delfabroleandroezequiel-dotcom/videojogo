using System.Collections.Generic;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// A procedural neon force-field barrier — a Door that needs no tileset art. Parallel energy beams
// (EnergyBarrier.gdshader) run between two emitter caps, with sparks at the emitters and a light
// cast on the surroundings. Open() surges, then burns the beams away bottom-up with sparks riding
// the cut; Close() brings it back. Same Open() entry point as Door, so a Lever's Activated signal
// wires straight to it (Node > Signals). Width sets the span it blocks; Horizontal turns it into
// a hatch across a vertical shaft. Everything is built in code from the exports — the .tscn is
// just this node. [Tool] so size/color/state preview live in the editor.
// Key-locked variant: set RequiredKeyItemId and a lock plate with a neon keyhole appears in the
// middle of the field. Interacting with the key in the inventory unlocks it (permanently, same
// GetPath()-keyed persistence Chest uses); without it, the keyhole flashes red. Same prompt
// strings and unlock sound as a locked Chest.
[Tool]
public partial class EnergyBarrier : Node2D
{
	public enum NeonColorPreset { Cyan, Green, Red, Violet, Gold, White, Custom }

	private static readonly Dictionary<NeonColorPreset, Color> PresetColors = new()
	{
		{ NeonColorPreset.Cyan, new Color(0.1f, 0.85f, 1f) },
		{ NeonColorPreset.Green, new Color(0.2f, 1f, 0.35f) },
		{ NeonColorPreset.Red, new Color(1f, 0.12f, 0.15f) },
		{ NeonColorPreset.Violet, new Color(0.7f, 0.25f, 1f) },
		{ NeonColorPreset.Gold, new Color(1f, 0.72f, 0.15f) },
		{ NeonColorPreset.White, new Color(0.85f, 0.9f, 1f) },
	};

	private const string ShaderPath = "res://resources/shaders/EnergyBarrier.gdshader";
	private const string SparkProcessPath = "res://resources/particles/LightningSparkProcess.tres";
	private const string SoftDotPath = "res://resources/particles/SoftDotGradient.tres";
	private const string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";

	// Collision thickness, same as Door's body.
	private const float BodyThickness = 32f;
	private const float EmitterHeight = 10f;
	private const float LightSpacing = 140f;
	// Must match the shader's pad_px default — the field rect is this much wider on each side.
	private const float FieldPad = 14f;

	[Signal] public delegate void OpenedEventHandler();
	[Signal] public delegate void ClosedEventHandler();

	private float _width = 200f;

	// The span across the passage the barrier blocks — along Y, or along X when Horizontal.
	[Export(PropertyHint.Range, "32,2000,1")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); Rebuild(); }
	}

	private bool _horizontal;

	[Export]
	public bool Horizontal
	{
		get => _horizontal;
		set { _horizontal = value; Rebuild(); }
	}

	private NeonColorPreset _neonColor = NeonColorPreset.Cyan;

	[Export]
	public NeonColorPreset NeonColor
	{
		get => _neonColor;
		set { _neonColor = value; Rebuild(); }
	}

	private Color _customColor = Colors.White;

	// Only used when NeonColor is Custom.
	[Export]
	public Color CustomColor
	{
		get => _customColor;
		set { _customColor = value; Rebuild(); }
	}

	private int _beamCount = 3;

	[Export(PropertyHint.Range, "1,8,1")]
	public int BeamCount
	{
		get => _beamCount;
		set { _beamCount = Mathf.Clamp(value, 1, 8); Rebuild(); }
	}

	private float _fieldThickness = 36f;

	// Visual thickness of the field; the beams spread evenly across it.
	[Export(PropertyHint.Range, "8,120,1")]
	public float FieldThickness
	{
		get => _fieldThickness;
		set { _fieldThickness = Mathf.Max(8f, value); Rebuild(); }
	}

	private float _intensity = 1.6f;

	[Export(PropertyHint.Range, "0.2,5,0.05")]
	public float Intensity
	{
		get => _intensity;
		set { _intensity = value; Rebuild(); }
	}

	private bool _showEmitters = true;

	// The dark caps the beams run between. Turn off when the barrier sits flush in a doorway whose
	// own art already reads as the frame.
	[Export]
	public bool ShowEmitters
	{
		get => _showEmitters;
		set { _showEmitters = value; Rebuild(); }
	}

	private string _requiredKeyItemId = "";

	// Empty: no lock (opened by a Lever or any other Open() call). Set to a Key-type Item's Id
	// (see ItemDatabase/ItemType.Key) to show the keyhole and open it with that key instead.
	[Export]
	public string RequiredKeyItemId
	{
		get => _requiredKeyItemId;
		set { _requiredKeyItemId = value ?? ""; Rebuild(); }
	}

	// Stackable keys (Item.Stackable) are used up on unlock; a non-stackable key item is kept either way.
	[Export] public bool ConsumesKey = true;

	[Export] public bool StartsOpen;
	[Export] public float OpenDuration = 0.9f;
	[Export] public float CloseDuration = 0.6f;
	[Export] public float LightEnergy = 0.8f;
	// Optional audio — a looping hum while closed, and one-shots for opening/closing.
	[Export] public AudioStream HumSound;
	[Export] public AudioStream OpenSound;
	[Export] public AudioStream CloseSound;

	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Node2D _rig;
	private ShaderMaterial _fieldMaterial;
	private ColorRect _field;
	private readonly List<GpuParticles2D> _emitterSparks = new();
	private readonly List<PointLight2D> _lights = new();
	private readonly List<CanvasItem> _emitterGlows = new();
	private GpuParticles2D _burnSparks;
	private AudioStreamPlayer2D _hum;
	private bool _isOpen;
	private Tween _tween;

	private Node2D _lock;
	private Polygon2D _keyhole;
	private Label _interactPrompt;
	private Tween _lockTween;
	private bool _playerInRange;
	private PlayerInventory _playerInventory;
	private string _persistenceId;

	public bool IsOpen => _isOpen;

	private bool IsKeyLocked => !string.IsNullOrEmpty(_requiredKeyItemId);

	private Color GlowColor => _neonColor == NeonColorPreset.Custom ? _customColor : PresetColors[_neonColor];

	public override void _Ready()
	{
		_isOpen = StartsOpen && !Engine.IsEditorHint();
		if (!Engine.IsEditorHint() && IsKeyLocked)
		{
			_persistenceId = GetPath().ToString();
			_isOpen |= SaveManager.Instance.IsPickupCollected(_persistenceId);
		}
		Rebuild();

		if (Engine.IsEditorHint())
			return;

		if (HumSound is not null)
		{
			_hum = new AudioStreamPlayer2D { Stream = HumSound };
			AddChild(_hum);
			if (!_isOpen)
				_hum.Play();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange || _isOpen || !IsKeyLocked || DialogueBox.Instance.IsOpen)
			return;

		if (@event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			TryUnlock();
		}
	}

	private bool PlayerHasKey => _playerInventory is not null && _playerInventory.HasItem(_requiredKeyItemId);

	private void TryUnlock()
	{
		if (!PlayerHasKey)
		{
			DenyUnlock();
			return;
		}

		SaveManager.Instance.MarkPickupCollected(_persistenceId);
		if (ConsumesKey)
			SaveManager.Instance.ConsumeItem(_requiredKeyItemId);
		Sfx.PlayAt(this, "World/Doors", "Lock Unlock");
		_interactPrompt.Visible = false;

		// Keyhole flares white, the plate pops and fades, then the field itself opens.
		_lockTween?.Kill();
		_lockTween = CreateTween();
		_lockTween.TweenProperty(_keyhole, "color", new Color(3f, 3f, 3f), 0.12f);
		_lockTween.TweenProperty(_lock, "scale", new Vector2(1.25f, 1.25f), 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_lockTween.Parallel().TweenProperty(_lock, "modulate:a", 0f, 0.25f).SetDelay(0.05f);
		_lockTween.TweenCallback(Callable.From(Open));
	}

	// No key: the keyhole flashes red and the plate shakes.
	private void DenyUnlock()
	{
		Color keyholeColor = KeyholeColor;
		_lockTween?.Kill();
		_lock.Position = Vector2.Zero;
		_lockTween = CreateTween();
		_lockTween.TweenProperty(_keyhole, "color", new Color(2.2f, 0.15f, 0.12f), 0.05f);
		foreach (float x in new[] { -3f, 3f, -2f, 2f, 0f })
			_lockTween.TweenProperty(_lock, "position:x", x, 0.04f);
		_lockTween.TweenProperty(_keyhole, "color", keyholeColor, 0.25f);
	}

	private Color KeyholeColor => GlowColor.Lightened(0.25f) * 1.8f;

	private void OnKeyDetectorBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInventory = body.GetNodeOrNull<PlayerInventory>("Inventory");
		_playerInRange = true;
		UpdatePromptText();
	}

	private void OnKeyDetectorBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_playerInventory = null;
		UpdatePromptText();
	}

	private void UpdatePromptText()
	{
		if (_interactPrompt is null)
			return;

		_interactPrompt.Visible = _playerInRange && !_isOpen;
		_interactPrompt.Text = PlayerHasKey ? "UI_INTERACT_PROMPT" : "UI_CHEST_LOCKED_PROMPT";
	}

	public void Toggle()
	{
		if (_isOpen)
			Close();
		else
			Open();
	}

	public void Open()
	{
		if (_isOpen)
			return;

		_isOpen = true;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_hum?.Stop();
		UpdatePromptText();
		// Opened some other way (a Lever) while still locked: the plate just fades out.
		if (_lock is not null && _lock.Modulate.A > 0f && _lockTween?.IsRunning() != true)
		{
			_lockTween = CreateTween();
			_lockTween.TweenProperty(_lock, "modulate:a", 0f, 0.3f);
		}
		PlayOneShot(OpenSound);

		_tween?.Kill();
		_tween = CreateTween();
		// Brief power surge (beams flare and stutter) before the field burns away.
		_tween.TweenMethod(Callable.From<float>(SetSurge), 0f, 1f, 0.12f);
		_tween.TweenCallback(Callable.From(() => _burnSparks.Emitting = true));
		_tween.TweenMethod(Callable.From<float>(SetDissolve), 0f, 1f, OpenDuration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
		_tween.Parallel().TweenMethod(Callable.From<float>(SetSurge), 1f, 0f, OpenDuration * 0.5f);
		_tween.Parallel().TweenMethod(Callable.From<float>(SetLightFactor), 1f, 0f, OpenDuration);
		_tween.TweenCallback(Callable.From(() =>
		{
			_burnSparks.Emitting = false;
			ApplyOpenState(true);
			EmitSignal(SignalName.Opened);
		}));
	}

	public void Close()
	{
		if (!_isOpen)
			return;

		_isOpen = false;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		PlayOneShot(CloseSound);
		_hum?.Play();

		_tween?.Kill();
		ApplyOpenState(false);
		if (_lock is not null)
		{
			_lockTween?.Kill();
			_lock.Scale = Vector2.One;
			_lockTween = CreateTween();
			_lockTween.TweenProperty(_lock, "modulate:a", 1f, CloseDuration);
			_keyhole.Color = KeyholeColor;
		}
		UpdatePromptText();
		SetDissolve(1f);
		SetLightFactor(0f);
		_burnSparks.Emitting = true;
		_tween = CreateTween();
		_tween.TweenMethod(Callable.From<float>(SetDissolve), 1f, 0f, CloseDuration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		_tween.Parallel().TweenMethod(Callable.From<float>(SetLightFactor), 0f, 1f, CloseDuration);
		_tween.TweenCallback(Callable.From(() => _burnSparks.Emitting = false));
		_tween.TweenMethod(Callable.From<float>(SetSurge), 1f, 0f, 0.25f);
		_tween.TweenCallback(Callable.From(() => EmitSignal(SignalName.Closed)));
	}

	private void PlayOneShot(AudioStream stream)
	{
		if (stream is null)
			return;
		var player = new AudioStreamPlayer2D { Stream = stream };
		AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	private void SetSurge(float value) => _fieldMaterial.SetShaderParameter("surge", value);

	private void SetDissolve(float value)
	{
		_fieldMaterial.SetShaderParameter("dissolve", value);
		// Keep the burn sparks on the cut line — mirrors the shader's average beam cut position.
		float cut = 1.06f - value * 1.39f;
		_burnSparks.Position = new Vector2(0f, (Mathf.Clamp(cut, 0f, 1f) - 0.5f) * _width);
	}

	private void SetLightFactor(float factor)
	{
		foreach (PointLight2D light in _lights)
			light.Energy = LightEnergy * factor;
	}

	// Visible field/emitter sparks/lights only while closed (or animating) — a fully open barrier
	// costs nothing to draw.
	private void ApplyOpenState(bool open)
	{
		_field.Visible = !open;
		foreach (GpuParticles2D sparks in _emitterSparks)
			sparks.Emitting = !open;
		foreach (PointLight2D light in _lights)
			light.Visible = !open;
		foreach (CanvasItem glow in _emitterGlows)
			glow.Visible = !open;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		_tween?.Kill();
		foreach (string name in new[] { "Body", "Rig", "Lock", "KeyDetector" })
		{
			if (GetNodeOrNull(name) is Node existing)
			{
				RemoveChild(existing);
				existing.QueueFree();
			}
		}
		_emitterSparks.Clear();
		_lights.Clear();
		_emitterGlows.Clear();

		Color color = GlowColor;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		// Collision (unrotated; the shape itself swaps axes). Always a fresh shape resource.
		_body = new StaticBody2D { Name = "Body" };
		_collision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = _horizontal ? new Vector2(_width, BodyThickness) : new Vector2(BodyThickness, _width) },
			Disabled = _isOpen,
		};
		_body.AddChild(_collision);
		AddChild(_body);

		// Visuals are authored vertically (length along Y) and the rig is turned for Horizontal —
		// "bottom-up" dissolve becomes right-to-left.
		_rig = new Node2D { Name = "Rig", Rotation = _horizontal ? -Mathf.Pi / 2f : 0f };
		AddChild(_rig);

		_fieldMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		_fieldMaterial.SetShaderParameter("glow_color", color);
		_fieldMaterial.SetShaderParameter("intensity", _intensity);
		_fieldMaterial.SetShaderParameter("beam_count", _beamCount);
		_fieldMaterial.SetShaderParameter("length_px", _width);
		_fieldMaterial.SetShaderParameter("thickness_px", _fieldThickness);
		_fieldMaterial.SetShaderParameter("seed", Mathf.PosMod(GlobalPosition.X * 0.0137f + GlobalPosition.Y * 0.0071f, 1f));
		_fieldMaterial.SetShaderParameter("dissolve", _isOpen ? 1f : 0f);
		_field = new ColorRect
		{
			Material = _fieldMaterial,
			Position = new Vector2(-_fieldThickness / 2f - FieldPad, -_width / 2f),
			Size = new Vector2(_fieldThickness + FieldPad * 2f, _width),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_rig.AddChild(_field);

		var sparkProcess = (ParticleProcessMaterial)GD.Load<ParticleProcessMaterial>(SparkProcessPath).Duplicate();
		var softDot = GD.Load<Texture2D>(SoftDotPath);

		foreach (float sign in new[] { -1f, 1f })
		{
			float y = sign * _width / 2f;

			if (_showEmitters)
				_rig.AddChild(BuildEmitter(y, sign, color, additive));

			var sparks = new GpuParticles2D
			{
				Material = additive,
				Amount = 6,
				Lifetime = 0.45,
				Randomness = 0.7f,
				Texture = softDot,
				ProcessMaterial = sparkProcess,
				Scale = new Vector2(0.25f, 0.25f),
				Position = new Vector2(0f, y - sign * 3f),
				Modulate = color.Lightened(0.3f),
				Emitting = !_isOpen,
			};
			_rig.AddChild(sparks);
			_emitterSparks.Add(sparks);
		}

		var burnProcess = (ParticleProcessMaterial)sparkProcess.Duplicate();
		burnProcess.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		// Particles are scaled 0.3 below, so the box is ~3.3x the field half-thickness.
		burnProcess.EmissionBoxExtents = new Vector3(_fieldThickness / 0.6f, 6f, 0f);
		_burnSparks = new GpuParticles2D
		{
			Material = additive,
			Amount = 48,
			Lifetime = 0.5,
			Randomness = 0.6f,
			Texture = softDot,
			ProcessMaterial = burnProcess,
			Scale = new Vector2(0.3f, 0.3f),
			Modulate = color.Lightened(0.4f),
			Emitting = false,
		};
		_rig.AddChild(_burnSparks);

		var lightTexture = GD.Load<Texture2D>(LightTexturePath);
		int lightCount = Mathf.Max(1, Mathf.CeilToInt(_width / LightSpacing));
		for (int i = 0; i < lightCount; i++)
		{
			var light = new PointLight2D
			{
				Texture = lightTexture,
				Color = color,
				Energy = LightEnergy,
				TextureScale = Mathf.Min(_width / lightCount, 220f) * 1.6f / lightTexture.GetWidth(),
				Position = new Vector2(0f, ((i + 0.5f) / lightCount - 0.5f) * _width),
			};
			_rig.AddChild(light);
			_lights.Add(light);
		}

		BuildLock(color, additive);
		ApplyOpenState(_isOpen);
	}

	// Key-locked only: a dark beveled plate mounted in the middle of the field (always upright, so
	// it lives outside the rotated rig) with a glowing keyhole, plus the proximity zone + prompt.
	private void BuildLock(Color color, Material additive)
	{
		_lock = null;
		_keyhole = null;
		_interactPrompt = null;
		if (!IsKeyLocked)
			return;

		const float halfW = 13f;
		const float halfH = 17f;
		const float chamfer = 5f;
		Vector2[] Octagon(float inset) => new[]
		{
			new Vector2(-halfW + chamfer, -halfH + inset), new Vector2(halfW - chamfer, -halfH + inset),
			new Vector2(halfW - inset, -halfH + chamfer), new Vector2(halfW - inset, halfH - chamfer),
			new Vector2(halfW - chamfer, halfH - inset), new Vector2(-halfW + chamfer, halfH - inset),
			new Vector2(-halfW + inset, halfH - chamfer), new Vector2(-halfW + inset, -halfH + chamfer),
		};

		_lock = new Node2D { Name = "Lock" };
		_lock.AddChild(new Sprite2D
		{
			Material = additive,
			Texture = GD.Load<Texture2D>(SoftDotPath),
			Modulate = new Color(color, 0.7f),
			Scale = new Vector2(1.1f, 1.3f),
		});
		_lock.AddChild(new Polygon2D { Color = new Color(0.06f, 0.06f, 0.08f), Polygon = Octagon(0f) });
		_lock.AddChild(new Polygon2D { Color = new Color(0.12f, 0.12f, 0.15f), Polygon = Octagon(3f) });
		foreach (Vector2 corner in new[] { new Vector2(-8f, -12f), new Vector2(8f, -12f), new Vector2(-8f, 12f), new Vector2(8f, 12f) })
			_lock.AddChild(new Polygon2D { Color = new Color(0.25f, 0.25f, 0.3f), Polygon = Square(corner, 1f) });

		var rimPoints = new List<Vector2>(Octagon(0.75f)) { Octagon(0.75f)[0] };
		_lock.AddChild(new Line2D
		{
			Material = additive,
			Width = 1.5f,
			DefaultColor = color * 1.3f,
			Points = rimPoints.ToArray(),
			JointMode = Line2D.LineJointMode.Round,
		});

		// Keyhole: round top + flared slot, traced as one outline.
		const float radius = 4.5f;
		var center = new Vector2(0f, -4f);
		var keyholePoints = new List<Vector2> { new(-3.2f, 8f) };
		float startAngle = Mathf.Atan2(4.2f, -1.6f);
		float endAngle = Mathf.Atan2(4.2f, 1.6f) + Mathf.Tau;
		const int arcSteps = 20;
		for (int i = 0; i <= arcSteps; i++)
		{
			float a = Mathf.Lerp(startAngle, endAngle, i / (float)arcSteps);
			keyholePoints.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
		}
		keyholePoints.Add(new Vector2(3.2f, 8f));
		_keyhole = new Polygon2D { Material = additive, Color = KeyholeColor, Polygon = keyholePoints.ToArray() };
		_lock.AddChild(_keyhole);

		if (_isOpen)
			_lock.Modulate = new Color(1f, 1f, 1f, 0f);
		AddChild(_lock);

		_interactPrompt = new Label
		{
			Text = "UI_INTERACT_PROMPT",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-80f, -halfH - 22f),
			Size = new Vector2(160f, 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		_lock.AddChild(_interactPrompt);

		var detector = new Area2D { Name = "KeyDetector", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		const float reach = 110f;
		detector.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = _horizontal ? new Vector2(_width, reach) : new Vector2(reach, _width) },
		});
		AddChild(detector);
		if (!Engine.IsEditorHint())
		{
			detector.BodyEntered += OnKeyDetectorBodyEntered;
			detector.BodyExited += OnKeyDetectorBodyExited;
		}
	}

	private static Vector2[] Square(Vector2 center, float half) => new[]
	{
		center + new Vector2(-half, -half), center + new Vector2(half, -half),
		center + new Vector2(half, half), center + new Vector2(-half, half),
	};

	// A dark beveled cap with a neon slit on the face the beams leave from, plus a soft halo.
	private Node2D BuildEmitter(float y, float sign, Color color, Material additive)
	{
		var emitter = new Node2D { Position = new Vector2(0f, y) };
		float halfW = _fieldThickness / 2f + 6f;
		float bevel = 3f;
		// Local Y: 0 = the face toward the field, -sign*EmitterHeight = the back against the wall.
		float back = sign * EmitterHeight;

		emitter.AddChild(new Polygon2D
		{
			Color = new Color(0.07f, 0.07f, 0.09f),
			Polygon = new[]
			{
				new Vector2(-halfW, back), new Vector2(halfW, back),
				new Vector2(halfW, sign * bevel), new Vector2(halfW - bevel, 0f),
				new Vector2(-halfW + bevel, 0f), new Vector2(-halfW, sign * bevel),
			},
		});
		emitter.AddChild(new Polygon2D
		{
			Color = new Color(0.16f, 0.16f, 0.19f),
			Polygon = new[]
			{
				new Vector2(-halfW + 2f, back - sign * 2f), new Vector2(halfW - 2f, back - sign * 2f),
				new Vector2(halfW - 2f, back - sign * 4f), new Vector2(-halfW + 2f, back - sign * 4f),
			},
		});
		var slit = new Line2D
		{
			Material = additive,
			Width = 2f,
			DefaultColor = color.Lightened(0.35f) * 1.5f,
			Points = new[] { new Vector2(-halfW + bevel + 1f, -sign * 0.5f), new Vector2(halfW - bevel - 1f, -sign * 0.5f) },
		};
		var halo = new Sprite2D
		{
			Material = additive,
			Texture = GD.Load<Texture2D>(SoftDotPath),
			Modulate = new Color(color, 0.55f),
			Scale = new Vector2(_fieldThickness / 40f, 0.35f),
			Position = new Vector2(0f, -sign * 2f),
		};
		emitter.AddChild(slit);
		emitter.AddChild(halo);
		_emitterGlows.Add(slit);
		_emitterGlows.Add(halo);
		return emitter;
	}
}
