using Godot;
using Metroidvania.Quests;

namespace Metroidvania.World;

// Put on the parent of a set piece that belongs to one stretch of a quest (the overturned cart,
// its bandits and Leomax in BosqueLobos1 during "El carro de Morlok"): the whole group only exists
// while that quest is in the chosen state, so the player can't stumble on it before being asked,
// and it doesn't stay in the map forever once the quest is done.
[GlobalClass]
public partial class QuestGate : Node2D
{
	public enum GateMode
	{
		WhileActive,      // started and not completed yet
		AfterCompleted,   // only once it's completed
		UntilCompleted,   // from the start of the game until it's completed (started or not)
	}

	[Export] public string QuestId = "";
	[Export] public GateMode Mode = GateMode.WhileActive;

	// _EnterTree runs before any child's _Ready, so a closed gate is gone before its enemies or
	// pickups do anything (spawn loot, register as attackers, start talking).
	public override void _EnterTree()
	{
		if (string.IsNullOrEmpty(QuestId) || QuestManager.Instance is not { } quests)
			return;

		bool completed = quests.IsCompleted(QuestId);
		bool open = Mode switch
		{
			GateMode.WhileActive => quests.IsActive(QuestId) && !completed,
			GateMode.AfterCompleted => completed,
			_ => !completed,
		};
		if (open)
			return;

		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
		QueueFree();
	}
}
