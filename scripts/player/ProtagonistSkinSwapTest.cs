using Godot;

namespace Metroidvania.Player;

// Test-only weapon-skin toggle for the protagonistnuevo preview scenes — swaps this
// AnimatedSprite2D's SpriteFrames between the unarmed and axe-equipped sheets on
// debug_cycle_prota_skin (F6). Not a real equip system, just lets us eyeball the "equipped"
// look without wiring PlayerInventory/PlayerAbilities into the preview scene.
public partial class ProtagonistSkinSwapTest : AnimatedSprite2D
{
	[Export] public SpriteFrames UnarmedFrames;
	[Export] public SpriteFrames HachaFrames;

	private bool _usingHacha;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_cycle_prota_skin"))
			return;

		_usingHacha = !_usingHacha;
		string currentAnim = Animation;

		SpriteFrames = _usingHacha ? HachaFrames : UnarmedFrames;
		// Falls back to idle if the sheet we're switching to doesn't have whatever was playing
		// (e.g. mid-"attack1" when swapping back to the unarmed sheet, which has no attack yet).
		Animation = SpriteFrames.HasAnimation(currentAnim) ? currentAnim : "idle";
		Play();
	}
}
