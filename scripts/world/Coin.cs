using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class Coin : Area2D
{
	[Export] public int Value = 1;
	[Export] public string CustomPersistenceId = "";

	private string _persistenceId;

	public override void _Ready()
	{
		_persistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;

		if (SaveManager.Instance.IsPickupCollected(_persistenceId))
		{
			QueueFree();
			return;
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		SaveManager.Instance.AddGold(Value);
		SaveManager.Instance.MarkPickupCollected(_persistenceId);
		QueueFree();
	}
}
