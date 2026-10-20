using Godot;

namespace Metroidvania.World;

// A depth gradient laid over a floor's tiles: clear along the walkable top edge, sinking into shadow
// further down, so the floor reads as a surface with thickness instead of one flat mass. Place its
// origin on the floor's top-left; the shade starts TopOffset below that. Unshaded, so torches and
// the map's CanvasModulate don't change how deep it looks. [Tool] so it shows in the editor.
[Tool]
public partial class FloorShade : Node2D
{
	private float _width = 400f;
	private float _depth = 100f;
	private float _topOffset = 8f;
	private float _maxAlpha = 0.7f;
	private Color _color = new(0.01f, 0.015f, 0.03f, 1f);

	[Export] public float Width { get => _width; set { _width = Mathf.Max(1f, value); QueueRedraw(); } }
	[Export] public float Depth { get => _depth; set { _depth = Mathf.Max(1f, value); QueueRedraw(); } }
	[Export] public float TopOffset { get => _topOffset; set { _topOffset = value; QueueRedraw(); } }
	[Export(PropertyHint.Range, "0,1")] public float MaxAlpha { get => _maxAlpha; set { _maxAlpha = value; QueueRedraw(); } }
	[Export] public Color ShadeColor { get => _color; set { _color = value; QueueRedraw(); } }

	public override void _Ready()
	{
		Material ??= new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
	}

	public override void _Draw()
	{
		Color clear = _color with { A = 0f };
		Color dark = _color with { A = _maxAlpha };
		DrawPolygon(
			new[] { new Vector2(0f, _topOffset), new Vector2(_width, _topOffset), new Vector2(_width, _topOffset + _depth), new Vector2(0f, _topOffset + _depth) },
			new[] { clear, clear, dark, dark });
	}
}
