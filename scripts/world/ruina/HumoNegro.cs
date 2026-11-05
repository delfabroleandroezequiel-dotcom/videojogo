using Godot;

namespace Metroidvania.World;

// A column of thick black smoke rising from the ground: "the way is closed". Procedural (two particle
// layers on a soft radial puff), so it needs no art. Its origin sits on the ground; with BlockPassage
// it also stands up an invisible wall so the player can't walk through it.
[GlobalClass]
public partial class HumoNegro : Node2D
{
	[Export] public float Width = 150f;
	[Export] public float Height = 520f;
	[Export] public Color SmokeColor = new(0.05f, 0.04f, 0.04f, 1f);
	[Export] public bool BlockPassage = true;

	public override void _Ready()
	{
		ZIndex = 5;
		Texture2D puff = PuffTexture();
		// Dense, slow base hugging the ground, then the tall plume that reads from afar.
		AddChild(Layer(puff, amount: 26, lifetime: 3.5f, rise: 0.25f, scaleMin: 2.2f, scaleMax: 3.6f, alpha: 0.95f));
		AddChild(Layer(puff, amount: 44, lifetime: 7f, rise: 1f, scaleMin: 1.6f, scaleMax: 3.2f, alpha: 0.8f));

		if (BlockPassage)
		{
			var wall = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
			wall.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = new Vector2(Width * 0.4f, Height * 2f) },
				Position = new Vector2(0f, -Height),
			});
			AddChild(wall);
		}
	}

	private CpuParticles2D Layer(Texture2D puff, int amount, float lifetime, float rise, float scaleMin, float scaleMax, float alpha)
	{
		float speed = Height * rise / lifetime;
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(SmokeColor, 0f));
		ramp.SetColor(1, new Color(SmokeColor, 0f));
		ramp.AddPoint(0.15f, new Color(SmokeColor, alpha));
		ramp.AddPoint(0.7f, new Color(SmokeColor, alpha * 0.7f));
		var grow = new Curve();
		grow.AddPoint(new Vector2(0f, 0.5f));
		grow.AddPoint(new Vector2(1f, 1f));
		return new CpuParticles2D
		{
			Texture = puff,
			Amount = amount,
			Lifetime = lifetime,
			Preprocess = lifetime,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(Width * 0.5f, 12f),
			Direction = Vector2.Up,
			Spread = 12f,
			Gravity = new Vector2(6f, 0f),
			InitialVelocityMin = speed * 0.7f,
			InitialVelocityMax = speed * 1.2f,
			AngleMin = 0f,
			AngleMax = 360f,
			AngularVelocityMin = -12f,
			AngularVelocityMax = 12f,
			ScaleAmountMin = scaleMin,
			ScaleAmountMax = scaleMax,
			ScaleAmountCurve = grow,
			ColorRamp = ramp,
		};
	}

	private static Texture2D PuffTexture()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
		gradient.AddPoint(0.5f, new Color(1f, 1f, 1f, 0.6f));
		return new GradientTexture2D
		{
			Gradient = gradient,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
			Width = 64,
			Height = 64,
		};
	}
}
