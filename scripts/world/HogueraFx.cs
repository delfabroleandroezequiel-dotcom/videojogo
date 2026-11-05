using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Lighting a hoguera, Dark Souls style: a green flash of light, a shockwave ring, a burst of green
// and blue sparks, a short camera shake and the torch-catching sound. All procedural, no VFX sheets
// (see Diseno/Combate.md: own code FX).
public static class HogueraFx
{
	private static readonly Color Green = new(0.35f, 1f, 0.45f);
	private static readonly Color Mint = new(0.75f, 1f, 0.85f);
	private static readonly Color Blue = new(0.35f, 0.65f, 1f);

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static Texture2D _glow;
	private static Texture2D Glow => _glow ??= GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public static void Ignite(Node2D at)
	{
		if (at.GetTree()?.CurrentScene is not Node scene)
			return;
		Vector2 center = at.GlobalPosition + new Vector2(0f, -10f);

		var root = new Node2D { GlobalPosition = center, ZIndex = 20 };
		scene.AddChild(root);

		// Flash: a big light that blooms and dies out.
		var light = new PointLight2D { Texture = Glow, Color = Green, Energy = 3f, TextureScale = 1f };
		root.AddChild(light);
		Tween lt = light.CreateTween();
		lt.TweenProperty(light, "texture_scale", 5f, 0.18f).SetEase(Tween.EaseType.Out);
		lt.TweenProperty(light, "energy", 0f, 1.1f).SetEase(Tween.EaseType.In);

		// Core: a hot white-green dot that pops and fades.
		var core = new Sprite2D { Texture = Dot, Modulate = Mint, Material = Additive, Scale = Vector2.One * 0.4f };
		root.AddChild(core);
		Tween ct = core.CreateTween().SetParallel();
		ct.TweenProperty(core, "scale", Vector2.One * 0.9f, 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		ct.TweenProperty(core, "modulate:a", 0f, 0.45f).SetDelay(0.08f);

		// Shockwave ring.
		var ring = new Line2D { Width = 3f, DefaultColor = Green, Closed = true, Material = Additive, Antialiased = true };
		for (int i = 0; i < 40; i++)
			ring.AddPoint(Vector2.Right.Rotated(Mathf.Tau * i / 40f) * 10f);
		root.AddChild(ring);
		Tween rt = ring.CreateTween().SetParallel();
		rt.TweenProperty(ring, "scale", new Vector2(9f, 5f), 0.45f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		rt.TweenProperty(ring, "width", 0.5f, 0.45f);
		rt.TweenProperty(ring, "modulate:a", 0f, 0.45f);

		// Sparks: a one-shot burst, mostly upward, green and blue.
		root.AddChild(Burst(Green, 70, 260f));
		root.AddChild(Burst(Blue, 40, 180f));

		TrapKit.Shake(at, 5f, 0.35f);
		Sfx.PlayAt(at, "World/Torch", "Light Torch");
		Sfx.PlayAt(at, "Magic", "Firebuff");

		Tween cleanup = root.CreateTween();
		cleanup.TweenInterval(2.5f);
		cleanup.TweenCallback(Callable.From(root.QueueFree));
	}

	private static CpuParticles2D Burst(Color color, int amount, float speed)
	{
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(1f, 1f, 1f, 1f));
		ramp.SetColor(1, new Color(color, 0f));
		ramp.AddPoint(0.25f, color);
		var p = new CpuParticles2D
		{
			Emitting = true,
			OneShot = true,
			Explosiveness = 0.95f,
			Amount = amount,
			Lifetime = 1.2f,
			Texture = Dot,
			Material = Additive,
			Direction = Vector2.Up,
			Spread = 75f,
			InitialVelocityMin = speed * 0.35f,
			InitialVelocityMax = speed,
			Gravity = new Vector2(0f, 220f),
			DampingMin = 40f,
			DampingMax = 90f,
			ScaleAmountMin = 0.02f,
			ScaleAmountMax = 0.05f,
			ColorRamp = ramp,
		};
		return p;
	}
}
