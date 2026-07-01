using Godot;
using Metroidvania.Items;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class ItemPickup : Area2D
{
	[Export] public Item Item;

	public override void _Ready()
	{
		if (Item is null || string.IsNullOrEmpty(Item.Id) || SaveManager.Instance.HasItem(Item.Id))
		{
			QueueFree();
			return;
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		PlayerInventory inventory = body.GetNodeOrNull<PlayerInventory>("Inventory");
		if (inventory is null)
			return;

		inventory.CollectItem(Item);
		QueueFree();
	}
}
