using System.Linq;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;

namespace Metroidvania.UI;

public partial class InventoryUI : CanvasLayer
{
	private Control _panel;
	private VBoxContainer _itemList;
	private Label _equippedLabel;
	private bool _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_panel = GetNode<Control>("Panel");
		_itemList = GetNode<VBoxContainer>("Panel/VBox/ItemList");
		_equippedLabel = GetNode<Label>("Panel/VBox/EquippedLabel");

		_panel.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("inventory") && !DialogueBox.Instance.IsOpen)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Toggle()
	{
		_isOpen = !_isOpen;
		_panel.Visible = _isOpen;
		GetTree().Paused = _isOpen;

		if (_isOpen)
			Refresh();
	}

	private PlayerInventory GetPlayerInventory()
	{
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		return player?.GetNodeOrNull<PlayerInventory>("Inventory");
	}

	private void Refresh()
	{
		foreach (Node child in _itemList.GetChildren())
			child.QueueFree();

		PlayerInventory inventory = GetPlayerInventory();
		if (inventory is null)
			return;

		int equippedCount = SaveManager.Instance.GetEquippedRings().Count;
		_equippedLabel.Text = string.Format(TranslationServer.Translate("UI_INVENTORY_EQUIPPED"), equippedCount, SaveManager.MaxEquippedRings);

		foreach (string itemId in SaveManager.Instance.GetCollectedItems())
		{
			Item item = ItemDatabase.Instance.Get(itemId);
			if (item is null)
				continue;

			_itemList.AddChild(BuildItemRow(item, inventory));
		}
	}

	private Control BuildItemRow(Item item, PlayerInventory inventory)
	{
		HBoxContainer row = new();

		string typeTag = TranslationServer.Translate(item.Type switch
		{
			ItemType.Ability => "UI_INVENTORY_TAG_ABILITY",
			ItemType.Ring => "UI_INVENTORY_TAG_RING",
			ItemType.Quest => "UI_INVENTORY_TAG_QUEST",
			_ => "UI_INVENTORY_TAG_LORE",
		});
		string itemName = TranslationServer.Translate(item.ItemName);
		string description = TranslationServer.Translate(item.Description);

		Label label = new()
		{
			Text = $"{typeTag} {itemName}\n  {description}",
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(380, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		row.AddChild(label);

		if (item.Type == ItemType.Ring)
		{
			bool isEquipped = SaveManager.Instance.GetEquippedRings().Contains(item.Id);
			Button button = new() { Text = isEquipped ? "UI_INVENTORY_UNEQUIP" : "UI_INVENTORY_EQUIP" };
			button.Pressed += () =>
			{
				if (isEquipped)
					inventory.UnequipRing(item.Id);
				else
					inventory.EquipRing(item.Id);

				Refresh();
			};
			row.AddChild(button);
		}

		return row;
	}
}
