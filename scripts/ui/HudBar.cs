using Godot;

namespace Metroidvania.UI;

public partial class HudBar : Control
{
	[Export] public Color FillColor = new(0, 1, 0, 1);

	private ColorRect _fill;

	public override void _Ready()
	{
		_fill = GetNode<ColorRect>("Fill");
		_fill.Color = FillColor;
	}

	public void SetRatio(float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		_fill.AnchorRight = ratio;
	}
}
