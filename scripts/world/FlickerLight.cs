using Godot;

namespace Metroidvania.World;

// Standalone flickering PointLight2D — no sprite, no flame art, just the glow. Meant to drop
// directly on top of a torch/lantern/lit-window that's already baked into a background or
// tileset (like Torch.tscn's own Flame does with its AnimatedSprite2D), instead of covering it
// with a second sprite of its own.
// Energy wobbles from three sine layers at different speeds/scales instead of one — same
// "combine a few layers so it doesn't look like one repeating pulse" idea ProceduralWater.gdshader
// uses for its noise. StartOffset (seconds added to the internal clock at _Ready) staggers
// multiple instances placed near each other so they don't all pulse in lockstep — same idea as
// SpikeTrap/MovingPlatform's own StartOffset.
public partial class FlickerLight : PointLight2D
{
	[Export] public float BaseEnergy = 1.5f;
	[Export] public float EnergyFlickerAmount = 0.35f;

	// Subtle glow-radius breathing on top of the energy flicker — 0 disables it, keeping only the
	// brightness wobble.
	[Export] public float ScaleFlickerAmount = 0.04f;

	[Export] public float FlickerSpeed = 1f;
	[Export] public float StartOffset;

	private float _time;
	private float _baseTextureScale;

	public override void _Ready()
	{
		_time = StartOffset;
		_baseTextureScale = TextureScale;
	}

	public override void _Process(double delta)
	{
		_time += (float)delta * FlickerSpeed;

		float n = Mathf.Sin(_time) * 0.6f
			+ Mathf.Sin(_time * 2.7f + 1.3f) * 0.3f
			+ Mathf.Sin(_time * 5.1f + 2.7f) * 0.1f;

		Energy = BaseEnergy + n * EnergyFlickerAmount;
		TextureScale = _baseTextureScale + n * ScaleFlickerAmount;
	}
}
