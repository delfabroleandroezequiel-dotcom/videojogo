using System.Linq;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

public partial class InventoryUI : CanvasLayer
{
	private Control _panel;
	private VBoxContainer _itemList;
	private Label _equippedLabel;
	private Label _closeHint;
	private bool _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_panel = GetNode<Control>("Panel");
		_itemList = GetNode<VBoxContainer>("Panel/VBox/ItemList");
		_equippedLabel = GetNode<Label>("Panel/VBox/EquippedLabel");
		_closeHint = GetNode<Label>("Panel/VBox/CloseHint");

		// Jekyria window: vine frame, gold title, and the item list in a scroll so it stays inside it.
		var box = GetNode<VBoxContainer>("Panel/VBox");
		JekyriaFrame.Dress(_panel, box, GetNode<Label>("Panel/VBox/Title"), new Vector2(640f, 460f));
		JekyriaFrame.WrapInScroll(box, _itemList.GetIndex(), _itemList);

		_panel.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Opens only during free gameplay: never on top of the pause menu/settings (e.g. while rebinding
		// keys), a dialogue or another overlay — they all pause the tree — nor outside a level. Closing
		// is always allowed.
		if (!_isOpen && (GetTree().Paused || GetTree().CurrentScene is not Metroidvania.Save.LevelBootstrap))
			return;
		// Escape also closes it (the pause menu stays out of the way while it's open).
		if (_isOpen && @event.IsActionPressed("ui_cancel"))
		{
			Toggle();
			GetViewport().SetInputAsHandled();
			return;
		}
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

		// The hint shows the key actually bound to the inventory (it can be remapped).
		_closeHint.Text = string.Format(TranslationServer.Translate("UI_INVENTORY_CLOSE_HINT"),
			OS.GetKeycodeString(GameConfig.Instance.GetBinding("inventory")));

		PlayerInventory inventory = GetPlayerInventory();
		if (inventory is null)
			return;

		int equippedCount = SaveManager.Instance.GetEquippedRings().Count;
		_equippedLabel.Text = string.Format(TranslationServer.Translate("UI_INVENTORY_EQUIPPED"), equippedCount, SaveManager.MaxEquippedRings);

		bool anyItem = false;
		foreach (string itemId in SaveManager.Instance.GetCollectedItems())
		{
			Item item = ItemDatabase.Instance.Get(itemId);
			if (item is null)
				continue;

			_itemList.AddChild(BuildItemRow(item, inventory));
			anyItem = true;
		}
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in SaveManager.Instance.GetItemCounts())
		{
			Item item = ItemDatabase.Instance.Get(entry.Key);
			if (item is null || entry.Value <= 0)
				continue;

			_itemList.AddChild(BuildItemRow(item, inventory, entry.Value));
			anyItem = true;
		}
		if (!anyItem)
			_itemList.AddChild(new Label { Text = "UI_INVENTORY_EMPTY", Modulate = new Color(0.7f, 0.7f, 0.7f) });
	}

	private Control BuildItemRow(Item item, PlayerInventory inventory, int count = 0)
	{
		HBoxContainer row = new();

		string typeTag = TranslationServer.Translate(item.Type switch
		{
			ItemType.Ability => "UI_INVENTORY_TAG_ABILITY",
			ItemType.Ring => "UI_INVENTORY_TAG_RING",
			ItemType.Quest => "UI_INVENTORY_TAG_QUEST",
			ItemType.Key => "UI_INVENTORY_TAG_KEY",
			ItemType.Consumable => "UI_INVENTORY_TAG_CONSUMABLE",
			_ => "UI_INVENTORY_TAG_LORE",
		});
		string itemName = TranslationServer.Translate(item.ItemName);
		if (count > 0)
			itemName += $" x{count}";
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
			Button button = new()
			{
				Text = isEquipped ? "UI_INVENTORY_UNEQUIP" : "UI_INVENTORY_EQUIP",
				ThemeTypeVariation = "SmallButton",
				CustomMinimumSize = new Vector2(120, 0),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			};
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

		if (item.Type == ItemType.Consumable)
		{
			Button use = SmallButton("UI_INVENTORY_USE");
			use.Pressed += () =>
			{
				if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player && player.UseConsumable(item))
					Refresh();
			};
			row.AddChild(use);

			bool inBar = SaveManager.Instance.FindQuickSlot(item.Id) >= 0;
			Button bar = SmallButton(inBar ? "UI_INVENTORY_UNASSIGN" : "UI_INVENTORY_ASSIGN");
			bar.Pressed += () =>
			{
				if (inBar)
					SaveManager.Instance.ClearQuickSlot(item.Id);
				else
					SaveManager.Instance.AssignQuickSlot(item.Id);
				Refresh();
			};
			row.AddChild(bar);
		}

		return row;
	}

	private static Button SmallButton(string textKey) => new()
	{
		Text = textKey,
		ThemeTypeVariation = "SmallButton",
		CustomMinimumSize = new Vector2(120, 0),
		SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
	};
}
