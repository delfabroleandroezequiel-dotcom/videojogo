using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Delfrad thinks out loud a moment after arriving in a map (the smoke smell when he first walks into
// the cave): voice + floating subtitle through Player.SayThought. Once per save by default, so coming
// back through the same map doesn't repeat it.
[GlobalClass]
public partial class PensamientoAlEntrar : Node
{
	[Export] public string[] ThoughtKeys = { };
	[Export] public float Delay = 1.4f;
	[Export] public bool OncePerSave = true;
	// Save key; empty = this node's path.
	[Export] public string PersistenceId = "";

	private string Key => "pensamiento:" + (string.IsNullOrEmpty(PersistenceId) ? GetPath().ToString() : PersistenceId);

	public override async void _Ready()
	{
		if (ThoughtKeys.Length == 0 || (OncePerSave && SaveManager.Instance.IsPickupCollected(Key)))
			return;

		await ToSignal(GetTree().CreateTimer(Delay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree() || GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player)
			return;

		player.SayThought(ThoughtKeys);
		if (OncePerSave)
			SaveManager.Instance.MarkPickupCollected(Key);
	}
}
