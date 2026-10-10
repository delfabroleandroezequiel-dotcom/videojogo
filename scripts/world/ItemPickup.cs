using Godot;
using Metroidvania.Items;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class ItemPickup : Area2D
{
	[Export] public Item Item;

	// Stackable items (keys) can be held, spent and found again, so "already have one" says
	// nothing about THIS pickup — those remember being taken per instance instead (GetPath()-keyed,
	// same as Chest).
	private bool AlreadyTaken => Item.Stackable
		? SaveManager.Instance.IsPickupCollected(GetPath().ToString())
		: SaveManager.Instance.HasItem(Item.Id);

	public override void _Ready()
	{
		if (Item is null || string.IsNullOrEmpty(Item.Id) || AlreadyTaken)
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
		if (Item.Stackable)
			SaveManager.Instance.MarkPickupCollected(GetPath().ToString());
		QueueFree();
	}
}
