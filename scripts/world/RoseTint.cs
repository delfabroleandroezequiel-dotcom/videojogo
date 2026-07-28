using Godot;

namespace Metroidvania.World;

// Lets a single rose sprite be recolored to a named preset via the HueShift shader,
// so we don't need separate art per color.
[Tool]
public partial class RoseTint : Sprite2D
{
	public enum Preset { Red, Orange, Yellow, Green, Blue, Purple, Pink, White }

	private Preset _color = Preset.Red;

	[Export]
	public Preset Color
	{
		get => _color;
		set
		{
			_color = value;
			ApplyColor();
		}
	}

	public override void _Ready()
	{
		ApplyColor();
	}

	private void ApplyColor()
	{
		if (Material is not ShaderMaterial mat)
			return;

		var (hue, saturation) = Color switch
		{
			Preset.Red => (0.0f, 1.0f),
			Preset.Orange => (0.05f, 1.0f),
			Preset.Yellow => (0.15f, 1.0f),
			Preset.Green => (0.38f, 1.0f),
			Preset.Blue => (0.55f, 1.0f),
			Preset.Purple => (0.75f, 1.0f),
			Preset.Pink => (0.9f, 0.55f),
			Preset.White => (0.0f, 0.05f),
			_ => (0.0f, 1.0f),
		};

		mat.SetShaderParameter("hue_shift", hue);
		mat.SetShaderParameter("saturation", saturation);
	}
}
