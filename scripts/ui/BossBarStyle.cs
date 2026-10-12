using Godot;

namespace Metroidvania.UI;

// One look for the boss health bar (resources/ui/BarrasJefe/*.tres): the ornate frame with an
// empty channel, and the red tube that fills that channel. A boss picks one through
// Enemy.BossBarStyle; BossHealthBar draws it.
[GlobalClass]
public partial class BossBarStyle : Resource
{
	// The whole frame, channel drawn empty (dark).
	[Export] public Texture2D Frame;
	// The full red tube, exactly the channel's size.
	[Export] public Texture2D Fill;
	// Where the channel starts inside Frame, in Frame pixels.
	[Export] public Vector2 FillOffset;
}
