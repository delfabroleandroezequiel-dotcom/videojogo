using System.Threading.Tasks;
using Godot;
using Metroidvania.Items;
using Metroidvania.Quests;

namespace Metroidvania.World;

// Starts a quest from inside a cinematic (Arthur's morning errand) and optionally hands the player
// an item for it. Does nothing if the quest was already started or finished, so a replayed
// cinematic never gives the item twice.
[GlobalClass]
public partial class StepStartQuest : CinematicStep
{
	[Export] public Quest Quest;
	[Export] public Item GivesItem;

	public override Task Run(Cinematic cinematic)
	{
		if (Quest is null || QuestManager.Instance.IsActive(Quest.Id) || QuestManager.Instance.IsCompleted(Quest.Id))
			return Task.CompletedTask;

		QuestManager.Instance.StartQuest(Quest);
		if (GivesItem is not null
			&& cinematic.GetTree().GetFirstNodeInGroup("player") is Node player
			&& player.GetNodeOrNull<PlayerInventory>("Inventory") is PlayerInventory inventory)
			inventory.CollectItem(GivesItem);
		return Task.CompletedTask;
	}
}
