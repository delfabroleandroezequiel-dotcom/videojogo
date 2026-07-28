using Godot;

namespace Metroidvania.World;

// One-shot splash burst for water entry, in the same "SpawnAt(context, globalPosition), then
// self-destroy" shape as ImpactEffect/VfxSpawner — but built from a procedural GpuParticles2D
// burst instead of a SpriteFrames animation, since there's no splash sheet in any owned pack yet.
// Same reasoning as ProceduralWater: a 1x1 white pixel texture tinted/scaled per droplet, so
// nothing needs cropping or seam-matching from source art. Easy to swap for a hand-painted burst
// later — only this file and its one call site in Water.cs would change.
public static class WaterSplash
{
	public static void SpawnAt(Node context, Vector2 globalPosition)
	{
		var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		image.SetPixel(0, 0, Colors.White);
		var texture = ImageTexture.CreateFromImage(image);

		var fadeCurve = new Curve();
		fadeCurve.AddPoint(new Vector2(0f, 1f));
		fadeCurve.AddPoint(new Vector2(1f, 0f));

		var material = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, -1f, 0f),
			Spread = 55f,
			InitialVelocityMin = 60f,
			InitialVelocityMax = 150f,
			Gravity = new Vector3(0f, 600f, 0f),
			ScaleMin = 2.5f,
			ScaleMax = 4.5f,
			Color = new Color(0.75f, 0.9f, 1f),
			AlphaCurve = new CurveTexture { Curve = fadeCurve },
		};

		var particles = new GpuParticles2D
		{
			Name = "WaterSplash",
			Texture = texture,
			ProcessMaterial = material,
			Amount = 14,
			Lifetime = 0.45,
			OneShot = true,
			Explosiveness = 1f,
			Emitting = true,
		};

		context.GetTree().CurrentScene.AddChild(particles);
		particles.GlobalPosition = globalPosition;
		particles.Finished += particles.QueueFree;
	}
}
