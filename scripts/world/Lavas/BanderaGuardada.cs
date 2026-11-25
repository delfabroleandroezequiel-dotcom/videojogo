using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// A per-save flag another scene can read (Las Lavas: the Forja's lever opens the way back up the
// Caída). Wire a Lever's Activated signal to Marcar(). Saved with the collected pickups, so it's
// committed at the next hoguera like everything else.
public partial class BanderaGuardada : Node
{
	[Export] public string Id = "";

	public void Marcar()
	{
		if (!string.IsNullOrEmpty(Id))
			SaveManager.Instance.MarkPickupCollected(Id);
	}
}
