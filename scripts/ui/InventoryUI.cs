using System.Collections.Generic;
using System.Linq;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

// Inventory window: four tabs (key items, consumables, weapons, armour), each a grid of icon slots.
// Hovering or focusing a slot (mouse, keyboard or pad) fills the detail panel on the right with the
// item's name, description, numbers and its actions (use, quick bar, equip).
public partial class InventoryUI : CanvasLayer
{
	private enum Tab { Key, Consumables, Weapons, Armor }

	private static readonly string[] TabKeys =
		{ "UI_INV_TAB_KEY", "UI_INV_TAB_CONSUMABLES", "UI_INV_TAB_WEAPONS", "UI_INV_TAB_ARMOR" };
	private const int GridColumns = 6;
	private const int MinGridSlots = 24;
	private const float SlotSize = 66f;

	// Palette: dark leather wells with a thin gold edge, one accent colour per item type.
	private static readonly Color CardColor = new(0.12f, 0.09f, 0.07f, 0.85f);
	private static readonly Color CardBorder = new(0.55f, 0.4f, 0.2f, 0.55f);
	private static readonly Color WellColor = new(0.05f, 0.04f, 0.03f, 0.85f);
	private static readonly Color EmptyWellColor = new(0.05f, 0.04f, 0.03f, 0.45f);
	private static readonly Color HoverBorder = new(1f, 0.8f, 0.35f, 1f);
	private static readonly Color NameColor = new(0.97f, 0.92f, 0.8f);
	private static readonly Color DescColor = new(0.78f, 0.74f, 0.66f);
	private static readonly Color AttrColor = new(1f, 0.82f, 0.4f);

	private Control _panel;
	private Label _equippedLabel;
	private Label _closeHint;
	private bool _isOpen;

	private Tab _tab = Tab.Key;
	private readonly Button[] _tabButtons = new Button[4];
	private GridContainer _grid;
	private TextureRect _detailIcon;
	private PanelContainer _detailWell;
	private Label _detailName;
	private Label _detailTag;
	private Label _detailDesc;
	private Label _detailAttr;
	private VBoxContainer _detailButtons;
	private string _selectedId;
	private readonly Dictionary<string, Button> _slots = new();

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		// Above the player's HUD (layer 10) so its bars don't draw over the window; below dialogue (30).
		Layer = 15;

		_panel = GetNode<Control>("Panel");
		_equippedLabel = GetNode<Label>("Panel/VBox/EquippedLabel");
		_closeHint = GetNode<Label>("Panel/VBox/CloseHint");

		var box = GetNode<VBoxContainer>("Panel/VBox");
		JekyriaFrame.Dress(_panel, box, GetNode<Label>("Panel/VBox/Title"), new Vector2(860f, 560f));
		BuildLayout(box);

		_panel.Visible = false;
	}

	// Tabs row, then the grid (scrolls if it ever outgrows the window) beside the detail panel.
	private void BuildLayout(VBoxContainer box)
	{
		var oldList = GetNode<Control>("Panel/VBox/ItemList");
		int index = oldList.GetIndex();
		box.RemoveChild(oldList);
		oldList.QueueFree();

		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabs.AddThemeConstantOverride("separation", 8);
		for (int i = 0; i < TabKeys.Length; i++)
		{
			Tab tab = (Tab)i;
			var button = new Button
			{
				Text = TabKeys[i],
				ToggleMode = true,
				ThemeTypeVariation = "SmallButton",
				CustomMinimumSize = new Vector2(170, 36),
				FocusMode = Control.FocusModeEnum.None,
			};
			button.AddThemeFontSizeOverride("font_size", 15);
			button.Pressed += () => SetTab(tab);
			_tabButtons[i] = button;
			tabs.AddChild(button);
		}
		box.AddChild(tabs);
		box.MoveChild(tabs, index);

		var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 14);
		box.AddChild(body);
		box.MoveChild(body, index + 1);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(GridColumns * (SlotSize + 6f) + 14f, 0f),
		};
		_grid = new GridContainer { Columns = GridColumns };
		_grid.AddThemeConstantOverride("h_separation", 6);
		_grid.AddThemeConstantOverride("v_separation", 6);
		scroll.AddChild(_grid);
		body.AddChild(scroll);

		var detail = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		detail.AddThemeStyleboxOverride("panel", Box(CardColor, CardBorder, 1, 6, 12f));
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 6);
		detail.AddChild(column);

		_detailWell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(96, 96),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		_detailIcon = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
		};
		_detailWell.AddChild(_detailIcon);
		column.AddChild(_detailWell);

		_detailName = MakeLabel(NameColor, 20, wrap: true, center: true);
		_detailTag = MakeLabel(NameColor, 12, center: true);
		_detailDesc = MakeLabel(DescColor, 15, wrap: true);
		_detailAttr = MakeLabel(AttrColor, 15, wrap: true);
		column.AddChild(_detailName);
		column.AddChild(_detailTag);
		column.AddChild(new HSeparator());
		column.AddChild(_detailDesc);
		column.AddChild(_detailAttr);
		column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

		_detailButtons = new VBoxContainer();
		_detailButtons.AddThemeConstantOverride("separation", 4);
		column.AddChild(_detailButtons);
		body.AddChild(detail);
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
			return;
		}
		// Tabs: Q / E on the keyboard, LB / RB on a pad.
		if (_isOpen && @event.IsPressed() && !@event.IsEcho())
		{
			int step = @event switch
			{
				InputEventKey { PhysicalKeycode: Key.Q } => -1,
				InputEventKey { PhysicalKeycode: Key.E } => 1,
				InputEventJoypadButton { ButtonIndex: JoyButton.LeftShoulder } => -1,
				InputEventJoypadButton { ButtonIndex: JoyButton.RightShoulder } => 1,
				_ => 0,
			};
			if (step != 0)
			{
				SetTab((Tab)(((int)_tab + step + TabKeys.Length) % TabKeys.Length));
				GetViewport().SetInputAsHandled();
			}
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

	private void SetTab(Tab tab)
	{
		if (_tab != tab)
			_selectedId = null;
		_tab = tab;
		Refresh();
	}

	private PlayerInventory GetPlayerInventory()
	{
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		return player?.GetNodeOrNull<PlayerInventory>("Inventory");
	}

	private static Tab TabOf(ItemType type) => type switch
	{
		ItemType.Consumable => Tab.Consumables,
		ItemType.Weapon => Tab.Weapons,
		ItemType.Ring => Tab.Armor,
		_ => Tab.Key,
	};

	// Everything the player holds: one-off items, then stackable ones with their count.
	private static List<(Item item, int count)> OwnedItems()
	{
		var owned = new List<(Item, int)>();
		foreach (string itemId in SaveManager.Instance.GetCollectedItems())
			if (ItemDatabase.Instance.Get(itemId) is Item item)
				owned.Add((item, 0));
		foreach (KeyValuePair<string, int> entry in SaveManager.Instance.GetItemCounts())
			if (entry.Value > 0 && ItemDatabase.Instance.Get(entry.Key) is Item item)
				owned.Add((item, entry.Value));
		return owned;
	}

	private void Refresh()
	{
		// The hint shows the key actually bound to the inventory (it can be remapped).
		_closeHint.Text = string.Format(TranslationServer.Translate("UI_INVENTORY_CLOSE_HINT"),
			OS.GetKeycodeString(GameConfig.Instance.GetBinding("inventory"))) + "   ·   " + TranslationServer.Translate("UI_INV_TABS_HINT");

		// The open tab at full brightness, the rest dimmed.
		for (int i = 0; i < _tabButtons.Length; i++)
		{
			bool current = i == (int)_tab;
			_tabButtons[i].SetPressedNoSignal(current);
			_tabButtons[i].Modulate = current ? new Color(1.25f, 1.15f, 0.9f) : new Color(0.55f, 0.55f, 0.55f);
		}

		// Only meaningful on the armour tab, but the line keeps its space so tabs never jump.
		_equippedLabel.Text = _tab == Tab.Armor
			? string.Format(TranslationServer.Translate("UI_INVENTORY_EQUIPPED"),
				SaveManager.Instance.GetEquippedRings().Count, SaveManager.MaxEquippedRings)
			: " ";

		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}
		_slots.Clear();

		PlayerInventory inventory = GetPlayerInventory();
		if (inventory is null)
			return;

		List<(Item item, int count)> items = OwnedItems().Where(entry => TabOf(entry.item.Type) == _tab).ToList();
		foreach ((Item item, int count) in items)
			_grid.AddChild(BuildSlot(item, count, inventory));

		int total = Mathf.Max(MinGridSlots, Mathf.CeilToInt(items.Count / (float)GridColumns) * GridColumns);
		for (int i = items.Count; i < total; i++)
		{
			var empty = new Panel { CustomMinimumSize = new Vector2(SlotSize, SlotSize) };
			empty.AddThemeStyleboxOverride("panel", Box(EmptyWellColor, CardBorder with { A = 0.2f }, 1, 4, 0f));
			_grid.AddChild(empty);
		}

		if (items.Count == 0)
		{
			ShowEmpty();
			return;
		}
		(Item selected, int selectedCount) = items.FirstOrDefault(entry => entry.item.Id == _selectedId);
		if (selected is null)
			(selected, selectedCount) = items[0];
		ShowDetail(selected, selectedCount, inventory);
		// Pad / keyboard: the selected slot takes focus so the arrows move around the grid.
		Button focusSlot = _slots[selected.Id];
		Callable.From(() => { if (IsInstanceValid(focusSlot)) focusSlot.GrabFocus(); }).CallDeferred();
	}

	private Button BuildSlot(Item item, int count, PlayerInventory inventory)
	{
		bool active = (item.Type == ItemType.Ring && SaveManager.Instance.GetEquippedRings().Contains(item.Id))
			|| (item.Type == ItemType.Weapon && inventory.HeldSwordId == item.Id);
		Color edge = active ? HoverBorder with { A = 0.85f } : TypeColor(item.Type) with { A = 0.45f };

		var slot = new Button
		{
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			Icon = item.Icon,
			ExpandIcon = true,
			IconAlignment = HorizontalAlignment.Center,
			VerticalIconAlignment = VerticalAlignment.Center,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			FocusMode = Control.FocusModeEnum.All,
			Text = item.Icon is null ? TranslationServer.Translate(item.ItemName).ToString()[..1] : "",
		};
		StyleBoxFlat normal = Box(WellColor, edge, active ? 2 : 1, 4, 9f);
		StyleBoxFlat lit = Box(WellColor.Lightened(0.08f), HoverBorder, 2, 4, 9f);
		slot.AddThemeStyleboxOverride("normal", normal);
		slot.AddThemeStyleboxOverride("hover", lit);
		slot.AddThemeStyleboxOverride("pressed", lit);
		slot.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), HoverBorder, 2, 4, 9f));

		if (count > 1)
		{
			Label amount = MakeLabel(NameColor, 13);
			amount.Text = $"x{count}";
			amount.AddThemeColorOverride("font_outline_color", Colors.Black);
			amount.AddThemeConstantOverride("outline_size", 4);
			amount.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
			amount.GrowHorizontal = Control.GrowDirection.Begin;
			amount.GrowVertical = Control.GrowDirection.Begin;
			amount.OffsetRight = -4f;
			amount.OffsetBottom = -1f;
			slot.AddChild(amount);
		}

		slot.MouseEntered += () => ShowDetail(item, count, inventory);
		slot.FocusEntered += () => ShowDetail(item, count, inventory);
		_slots[item.Id] = slot;
		return slot;
	}

	private void ShowEmpty()
	{
		_selectedId = null;
		_detailWell.Visible = false;
		_detailName.Text = TranslationServer.Translate("UI_INV_EMPTY_TAB");
		_detailTag.Text = "";
		_detailDesc.Text = "";
		_detailAttr.Text = "";
		ClearButtons();
	}

	private void ShowDetail(Item item, int count, PlayerInventory inventory)
	{
		_selectedId = item.Id;
		_detailWell.Visible = true;
		_detailWell.AddThemeStyleboxOverride("panel", Box(WellColor, TypeColor(item.Type) with { A = 0.6f }, 1, 6, 10f));
		_detailIcon.Texture = item.Icon;

		string name = TranslationServer.Translate(item.ItemName);
		if (count > 1)
			name += $"  x{count}";
		_detailName.Text = name;

		string tag = ((string)TranslationServer.Translate(TagKey(item.Type))).Trim('[', ']', ' ').ToUpperInvariant();
		if (item.Type == ItemType.Weapon && inventory.HeldSwordId == item.Id)
			tag += "  ·  " + ((string)TranslationServer.Translate("UI_INVENTORY_IN_HAND")).Trim('(', ')').ToUpperInvariant();
		if (item.Type == ItemType.Ring && SaveManager.Instance.GetEquippedRings().Contains(item.Id))
			tag += "  ·  " + ((string)TranslationServer.Translate("UI_INV_EQUIPPED_TAG")).ToUpperInvariant();
		_detailTag.Text = tag;
		_detailTag.AddThemeColorOverride("font_color", TypeColor(item.Type));

		_detailDesc.Text = TranslationServer.Translate(item.Description);
		_detailAttr.Text = BuildAttributes(item, inventory);

		ClearButtons();
		if (item.Type == ItemType.Ring)
		{
			bool isEquipped = SaveManager.Instance.GetEquippedRings().Contains(item.Id);
			AddAction(isEquipped ? "UI_INVENTORY_UNEQUIP" : "UI_INVENTORY_EQUIP", () =>
			{
				if (isEquipped)
					inventory.UnequipRing(item.Id);
				else
					inventory.EquipRing(item.Id);
			});
		}
		if (item.Type == ItemType.Consumable)
		{
			AddAction("UI_INVENTORY_USE", () =>
			{
				if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player)
					player.UseConsumable(item);
			});
			bool inBar = SaveManager.Instance.FindQuickSlot(item.Id) >= 0;
			AddAction(inBar ? "UI_INVENTORY_UNASSIGN" : "UI_INVENTORY_ASSIGN", () =>
			{
				if (inBar)
					SaveManager.Instance.ClearQuickSlot(item.Id);
				else
					SaveManager.Instance.AssignQuickSlot(item.Id);
			});
		}
	}

	private void ClearButtons()
	{
		foreach (Node child in _detailButtons.GetChildren())
		{
			_detailButtons.RemoveChild(child);
			child.QueueFree();
		}
	}

	// Detail-panel action; afterwards the grid rebuilds and the same item stays selected.
	private void AddAction(string textKey, System.Action action)
	{
		var button = new Button
		{
			Text = textKey,
			ThemeTypeVariation = "SmallButton",
			CustomMinimumSize = new Vector2(0, 36),
			FocusMode = Control.FocusModeEnum.All,
		};
		button.AddThemeFontSizeOverride("font_size", 15);
		button.Pressed += () =>
		{
			action();
			Refresh();
		};
		_detailButtons.AddChild(button);
	}

	private static string TagKey(ItemType type) => type switch
	{
		ItemType.Ability => "UI_INVENTORY_TAG_ABILITY",
		ItemType.Ring => "UI_INVENTORY_TAG_RING",
		ItemType.Quest => "UI_INVENTORY_TAG_QUEST",
		ItemType.Key => "UI_INVENTORY_TAG_KEY",
		ItemType.Consumable => "UI_INVENTORY_TAG_CONSUMABLE",
		ItemType.Weapon => "UI_INVENTORY_TAG_WEAPON",
		_ => "UI_INVENTORY_TAG_LORE",
	};

	private static Color TypeColor(ItemType type) => type switch
	{
		ItemType.Weapon => new Color(0.95f, 0.55f, 0.35f),
		ItemType.Ring => new Color(0.95f, 0.8f, 0.35f),
		ItemType.Quest => new Color(0.55f, 0.8f, 1f),
		ItemType.Key => new Color(0.85f, 0.75f, 0.55f),
		ItemType.Consumable => new Color(0.6f, 0.95f, 0.55f),
		ItemType.Ability => new Color(0.8f, 0.6f, 1f),
		_ => new Color(0.75f, 0.72f, 0.65f),
	};

	private static StyleBoxFlat Box(Color bg, Color border, int borderWidth, int radius, float margin) => new()
	{
		BgColor = bg,
		BorderColor = border,
		BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth, BorderWidthTop = borderWidth, BorderWidthBottom = borderWidth,
		CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
		ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
	};

	private static Label MakeLabel(Color color, int size, bool wrap = false, bool center = false)
	{
		var label = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", size);
		if (wrap)
		{
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(1, 0);
		}
		if (center)
			label.HorizontalAlignment = HorizontalAlignment.Center;
		return label;
	}

	// One line of what the item does in numbers: a weapon's damage per hit and whether it breaks
	// poise, and any stat bonuses (rings and the like).
	private static string BuildAttributes(Item item, PlayerInventory inventory)
	{
		var parts = new List<string>();
		if (item.Type == ItemType.Weapon)
		{
			int damage = item.WeaponDamageCap > 0
				? item.WeaponDamageCap
				: inventory.GetParent().GetNode<Stats>("Stats").AttackPower;
			parts.Add(string.Format(TranslationServer.Translate("UI_ATTR_DAMAGE_PER_HIT"), damage));
			parts.Add(TranslationServer.Translate(item.WeaponBreaksPoise ? "UI_ATTR_BREAKS_POISE" : "UI_ATTR_NO_POISE"));
		}
		void Bonus(int value, string statKey)
		{
			if (value != 0)
				parts.Add($"{TranslationServer.Translate(statKey)} {value:+0;-0}");
		}
		Bonus(item.HealthBonus, "UI_STAT_VITALITY");
		Bonus(item.StaminaBonus, "UI_STAT_ENDURANCE");
		Bonus(item.AttackBonus, "UI_STAT_STRENGTH");
		Bonus(item.DefenseBonus, "UI_ATTR_DEFENSE");
		return string.Join("\n", parts);
	}
}
