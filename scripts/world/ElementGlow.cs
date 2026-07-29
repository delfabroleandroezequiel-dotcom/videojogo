using Godot;

namespace Metroidvania.World;

// Runtime helper shared by LavaFall/LavaFloor/ProceduralWater — all spawn an ambient PointLight2D
// tinted by their Element, same style as Torch.tscn's Glow (same texture, similar scale): an
// accent near the hazard, not an attempt to cover its whole shape. Pulled out of LavaTileSheet
// (which is GandalfHardcore-specific) once a non-GandalfHardcore hazard needed it too.
public static class ElementGlow
{
	private const string GlowTexturePath = "res://resources/lighting/PointLightGradient.tres";

	public static Color GetColor(LavaElement element) =>
		element == LavaElement.Water ? new Color(0.4f, 0.75f, 1f) : new Color(1f, 0.55f, 0.15f);

	public static void AddTo(Node2D parent, Vector2 position, LavaElement element)
	{
		var glow = new PointLight2D
		{
			Name = "Glow",
			Position = position,
			Color = GetColor(element),
			Energy = 1.3f,
			Texture = GD.Load<Texture2D>(GlowTexturePath),
			TextureScale = 2.5f,
		};
		parent.AddChild(glow);
		glow.Owner = parent;
	}
}
