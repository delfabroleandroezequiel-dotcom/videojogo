using System.Threading.Tasks;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;

namespace Metroidvania.World;

// Hands the player an item and/or takes one away, inside a cinematic: Arthur gives his sword and
// the wooden one leaves the inventory. Swords equip themselves (PlayerInventory.HeldSwordId takes
// the best owned one). Safe to run twice: owning it already or not having the other does nothing.
[GlobalClass]
public partial class StepGiveItem : CinematicStep
{
	[Export] public Item Gives;
	[Export] public Item Takes;

	public override Task Run(Cinematic cinematic)
	{
		if (Takes is not null && !string.IsNullOrEmpty(Takes.Id))
			SaveManager.Instance.RemoveItem(Takes.Id);
		if (Gives is not null
			&& cinematic.GetTree().GetFirstNodeInGroup("player") is Node player
			&& player.GetNodeOrNull<PlayerInventory>("Inventory") is PlayerInventory inventory)
			inventory.CollectItem(Gives);
		return Task.CompletedTask;
	}

	public override void Skip(Cinematic cinematic) => Run(cinematic);
}
