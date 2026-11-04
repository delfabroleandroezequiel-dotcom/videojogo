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
		ShowIcon();
	}

	// Items with an icon show it instead of the placeholder diamond, bobbing gently so it reads as loot.
	private void ShowIcon()
	{
		if (Item.Icon is null)
			return;
		if (GetNodeOrNull<CanvasItem>("Visual") is CanvasItem placeholder)
			placeholder.Visible = false;
		var sprite = new Sprite2D { Texture = Item.Icon, TextureFilter = TextureFilterEnum.Nearest, Position = new Vector2(0f, -6f) };
		AddChild(sprite);
		Tween bob = sprite.CreateTween().SetLoops();
		bob.TweenProperty(sprite, "position:y", -10f, 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		bob.TweenProperty(sprite, "position:y", -6f, 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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
