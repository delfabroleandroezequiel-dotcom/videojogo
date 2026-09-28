using Godot;

namespace Metroidvania.World;

// Scattered patches of drifting fog (not a full-screen layer): Count patches placed at random inside
// Area, each a FogWisp-shader quad with its own size, noise offset and slow horizontal drift that
// wraps around the area, plus a gentle alpha "breathing". Put it in front of the gameplay layer
// (z_index above the player) for foreground fog, or behind it for background banks.
// [Tool] so the patches preview (and drift) in the editor; they're rebuilt, never saved.
[Tool]
public partial class FogWisps : Node2D
{
	private const string ShaderPath = "res://resources/shaders/FogWisp.gdshader";

	private Vector2 _area = new(2000f, 300f);

	// Rect (centered on this node) the patches are scattered and drift inside.
	[Export]
	public Vector2 Area
	{
		get => _area;
		set { _area = value; Rebuild(); }
	}

	private int _count = 8;

	[Export(PropertyHint.Range, "1,64,1")]
	public int Count
	{
		get => _count;
		set { _count = value; Rebuild(); }
	}

	private Vector2 _wispSize = new(640f, 170f);

	[Export]
	public Vector2 WispSize
	{
		get => _wispSize;
		set { _wispSize = value; Rebuild(); }
	}

	// Each patch's size is WispSize × a random factor in this range.
	[Export] public Vector2 SizeVariance = new(0.7f, 1.4f);

	private Color _fogColor = new(0.85f, 0.9f, 1f, 1f);

	[Export]
	public Color FogColor
	{
		get => _fogColor;
		set { _fogColor = value; Rebuild(); }
	}

	private float _alpha = 0.35f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Alpha
	{
		get => _alpha;
		set { _alpha = value; Rebuild(); }
	}

	// Horizontal drift in px/s (each patch gets 60-140% of it); the noise inside also scrolls.
	[Export] public float DriftSpeed = 10f;
	// Alpha breathing: ± this fraction, over BreathePeriod seconds.
	[Export] public float BreatheAmount = 0.3f;
	[Export] public float BreathePeriod = 7f;
	// Same seed → same layout every load.
	[Export] public int Seed = 1;

	private readonly System.Collections.Generic.List<(Sprite2D sprite, float speed, float phase, float baseAlpha)> _wisps = new();
	private float _time;

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (var wisp in _wisps)
			if (IsInstanceValid(wisp.sprite))
				wisp.sprite.QueueFree();
		_wisps.Clear();

		var shader = GD.Load<Shader>(ShaderPath);
		var noise = new NoiseTexture2D
		{
			Width = 256,
			Height = 256,
			Seamless = true,
			Noise = new FastNoiseLite { Seed = Seed, Frequency = 0.012f, FractalOctaves = 4 },
		};
		var image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
		image.Fill(Colors.White);
		var quad = ImageTexture.CreateFromImage(image);

		var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
		for (int i = 0; i < _count; i++)
		{
			float factor = rng.RandfRange(SizeVariance.X, SizeVariance.Y);
			Vector2 size = _wispSize * factor;
			var material = new ShaderMaterial { Shader = shader };
			material.SetShaderParameter("noise_tex", noise);
			material.SetShaderParameter("fog_color", _fogColor);
			material.SetShaderParameter("alpha", _alpha);
			material.SetShaderParameter("noise_offset", new Vector2(rng.Randf(), rng.Randf()));
			material.SetShaderParameter("scroll", new Vector2(rng.RandfRange(0.006f, 0.014f), rng.RandfRange(-0.003f, 0.003f)));

			var sprite = new Sprite2D
			{
				Texture = quad,
				Material = material,
				Scale = size / 4f,
				Position = new Vector2(rng.RandfRange(-_area.X, _area.X), rng.RandfRange(-_area.Y, _area.Y)) / 2f,
			};
			AddChild(sprite);
			_wisps.Add((sprite, DriftSpeed * rng.RandfRange(0.6f, 1.4f), rng.RandfRange(0f, Mathf.Tau), _alpha));
		}
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		float halfWidth = _area.X / 2f;
		foreach (var (sprite, speed, phase, baseAlpha) in _wisps)
		{
			if (!IsInstanceValid(sprite))
				continue;
			Vector2 position = sprite.Position;
			position.X += speed * (float)delta;
			// Wrap once fully past the right edge, re-entering from the left.
			float margin = sprite.Scale.X * 2f;
			if (position.X > halfWidth + margin)
				position.X = -halfWidth - margin;
			sprite.Position = position;

			if (BreathePeriod > 0f && sprite.Material is ShaderMaterial material)
			{
				float breathe = 1f + BreatheAmount * Mathf.Sin(_time * Mathf.Tau / BreathePeriod + phase);
				material.SetShaderParameter("alpha", baseAlpha * breathe);
			}
		}
	}
}
