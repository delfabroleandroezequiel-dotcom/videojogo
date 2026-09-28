using Godot;

namespace Metroidvania.World;

// Aerial-perspective haze: a translucent vertical gradient in the ambient color, drawn between the
// far background and the gameplay layer, so the background recedes and what's in front pops.
// Densest at the horizon band (HorizonPosition, 0 = top of the rect, 1 = bottom), fading toward the
// top and bottom. Place it right above the backdrop in draw order and size it to cover the map.
// [Tool] so the rect and colors preview live in the editor.
[Tool]
public partial class AtmosphereHaze : Node2D
{
	private Vector2 _size = new(2000f, 1000f);

	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = value; QueueRedraw(); }
	}

	private Color _hazeColor = new(0.78f, 0.85f, 0.95f, 1f);

	[Export]
	public Color HazeColor
	{
		get => _hazeColor;
		set { _hazeColor = value; Rebuild(); }
	}

	private float _topAlpha = 0.05f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float TopAlpha
	{
		get => _topAlpha;
		set { _topAlpha = value; Rebuild(); }
	}

	private float _horizonAlpha = 0.35f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HorizonAlpha
	{
		get => _horizonAlpha;
		set { _horizonAlpha = value; Rebuild(); }
	}

	private float _bottomAlpha = 0.15f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float BottomAlpha
	{
		get => _bottomAlpha;
		set { _bottomAlpha = value; Rebuild(); }
	}

	private float _horizonPosition = 0.6f;

	[Export(PropertyHint.Range, "0.01,0.99,0.01")]
	public float HorizonPosition
	{
		get => _horizonPosition;
		set { _horizonPosition = value; Rebuild(); }
	}

	private GradientTexture2D _texture;

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		var gradient = new Gradient
		{
			Offsets = new[] { 0f, _horizonPosition, 1f },
			Colors = new[]
			{
				_hazeColor with { A = _topAlpha },
				_hazeColor with { A = _horizonAlpha },
				_hazeColor with { A = _bottomAlpha },
			},
		};
		_texture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 4,
			Height = 256,
			FillFrom = new Vector2(0f, 0f),
			FillTo = new Vector2(0f, 1f),
		};
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_texture is not null)
			DrawTextureRect(_texture, new Rect2(-_size / 2f, _size), false);
	}
}
