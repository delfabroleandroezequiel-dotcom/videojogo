using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Hand-drawn greybox shapes (a "Visible" node full of Polygon2Ds, e.g. carried over from a Prueba
// map) that should guide painting in the editor but not show in the game — same rule as the
// MiniCorridor/MiniRampa fills: visible only in the editor or with GameConfig's runtime greybox on.
// Not [Tool], so the editor never runs this and always shows the shapes.
public partial class GreyboxVisual : Node2D
{
	public override void _Ready() => Visible = GameConfig.GreyboxFillVisible;
}
