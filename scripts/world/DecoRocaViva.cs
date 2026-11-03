using Godot;

namespace Metroidvania.World;

// Brings every placed decoRoca image to life when a map loads: wind on the foliage (DecoRocaViva
// shader) plus neon spores drifting off it. Matched by texture file name, so a rock dropped into any
// map gets this with nothing to set up. A sprite that already has its own material is left alone.
public static class DecoRocaViva
{
	private const string TexturePrefix = "decoroca";
	private const string MaterialPath = "res://resources/shaders/DecoRocaViva.tres";
	private const string SporeTexturePath = "res://resources/particles/SoftDotGradient.tres";
	private const float SporesPerMegapixel = 90f;
	private const int MinSpores = 8;
	private const int MaxSpores = 40;

	public static void Apply(Node root)
	{
		var material = GD.Load<Material>(MaterialPath);
		var stack = new System.Collections.Generic.Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			foreach (Node child in node.GetChildren())
				stack.Push(child);

			if (node is Sprite2D sprite && sprite.Material is null && IsDecoRoca(sprite))
			{
				sprite.Material = material;
				AddSpores(sprite);
			}
		}
	}

	private static bool IsDecoRoca(Sprite2D sprite) =>
		sprite.Texture is not null
		&& sprite.Texture.ResourcePath.GetFile().ToLowerInvariant().StartsWith(TexturePrefix);

	// Child of the sprite so it follows parallax and tint, with its scale undone so spores keep the
	// same on-screen size whatever size the rock was placed at.
	private static void AddSpores(Sprite2D sprite)
	{
		Vector2 worldSize = sprite.Texture.GetSize() * sprite.Scale.Abs();
		int amount = Mathf.Clamp(Mathf.RoundToInt(worldSize.X * worldSize.Y / 1_000_000f * SporesPerMegapixel), MinSpores, MaxSpores);

		var fade = new Gradient
		{
			Offsets = new[] { 0f, 0.2f, 0.75f, 1f },
			Colors = new[] { new Color(1f, 1f, 1f, 0f), Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) },
		};
		var spores = new CpuParticles2D
		{
			Name = "Esporas",
			Scale = new Vector2(1f / Mathf.Max(Mathf.Abs(sprite.Scale.X), 0.001f), 1f / Mathf.Max(Mathf.Abs(sprite.Scale.Y), 0.001f)),
			Texture = GD.Load<Texture2D>(SporeTexturePath),
			Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
				LightMode = CanvasItemMaterial.LightModeEnum.Unshaded,
			},
			Amount = amount,
			Lifetime = 6f,
			Preprocess = 6f,
			Randomness = 0.5f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = worldSize * 0.42f,
			Direction = Vector2.Up,
			Spread = 70f,
			Gravity = new Vector2(3f, -5f),
			InitialVelocityMin = 3f,
			InitialVelocityMax = 11f,
			ScaleAmountMin = 0.05f,
			ScaleAmountMax = 0.13f,
			Color = new Color(0.6f, 1f, 0.35f, 0.9f),
			ColorRamp = fade,
		};
		sprite.AddChild(spores);
	}
}
