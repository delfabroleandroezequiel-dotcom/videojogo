using Godot;

namespace Metroidvania.World;

// One look for the game-wide finishing pass (LacquerLayer → Laqueado.gdshader). Tints are added
// in sRGB space: ShadowTint mostly to dark pixels, HighlightTint mostly to bright ones. Lives in
// resources/rendering/Laqueado/.
[GlobalClass]
public partial class LacquerPreset : Resource
{
	// In stops: -1 = half as bright.
	[Export] public float Exposure;
	[Export] public float Contrast = 1f;
	[Export] public float Saturation = 1f;
	[Export] public Vector3 ShadowTint;
	[Export] public Vector3 HighlightTint;
	[Export(PropertyHint.Range, "0,1,0.01")] public float Vignette;
	[Export(PropertyHint.Range, "0,0.2,0.005")] public float Grain;
	// Pixels of red/blue split at the screen edges (0 = off).
	[Export] public float Aberration;

	public void ApplyTo(ShaderMaterial material)
	{
		material.SetShaderParameter("exposure", Exposure);
		material.SetShaderParameter("contrast", Contrast);
		material.SetShaderParameter("saturation", Saturation);
		material.SetShaderParameter("shadow_tint", ShadowTint);
		material.SetShaderParameter("highlight_tint", HighlightTint);
		material.SetShaderParameter("vignette", Vignette);
		material.SetShaderParameter("grain", Grain);
		material.SetShaderParameter("aberration", Aberration);
	}
}
