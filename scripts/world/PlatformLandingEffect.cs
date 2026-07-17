using Godot;

namespace Metroidvania.World;

// Plays a one-shot debris VFX (see VfxSpawner) the moment the player steps onto this platform.
// Re-arms when they step off, so it fires again next time they land — but not every physics
// frame while they're just standing there.
public partial class PlatformLandingEffect : Area2D
{
	[Export] public string VfxFramesPath = "res://resources/sprites/PedritasSpriteFrames.tres";
	[Export] public string VfxAnimation = "fall";
	[Export] public Vector2 VfxOffset = Vector2.Zero;
	[Export] public float VfxScale = 1f;

	private bool _triggered;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || !body.IsInGroup("player"))
			return;

		_triggered = true;
		VfxSpawner.SpawnAt(this, GlobalPosition + VfxOffset, VfxFramesPath, VfxAnimation, scale: VfxScale);
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player"))
			_triggered = false;
	}
}
