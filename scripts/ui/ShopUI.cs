using System.Collections.Generic;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;

namespace Metroidvania.UI;

// A merchant's shop window: the Jekyria frame with the stock laid out as a grid of item slots.
// Pointing at a slot (mouse hover or keyboard/gamepad focus) floats the item's name above it and
// fills the details row (description, price, how many you already hold); clicking/accepting buys
// one. Not an autoload (see CLAUDE.md): MerchantNpc creates one through Open() and it frees itself
// on close. Pauses the tree while open, like InventoryUI.
public partial class ShopUI : CanvasLayer
{
	private const int Columns = 6;
	private const float SlotSize = 72f;

	private static readonly Color SlotBg = new(0.07f, 0.06f, 0.05f, 0.92f);
	private static readonly Color SlotBorder = new(0.45f, 0.36f, 0.2f);
	private static readonly Color SlotBorderHot = new(1f, 0.82f, 0.35f);
	private static readonly Color GoldText = new(1f, 0.84f, 0.35f);
	private static readonly Color BadText = new(1f, 0.35f, 0.3f);
	private static readonly Color GoodText = new(0.5f, 1f, 0.55f);

	private string _titleKey;
	private string _shopId;
	private ShopEntry[] _entries;

	private Control _root;
	private Label _goldLabel;
	private Label _nameLabel;
	private Label _descLabel;
	private Label _priceLabel;
	private Label _statusLabel;
	private PanelContainer _nameTag;
	private Label _nameTagLabel;
	private readonly List<Button> _slots = new();
	private int _selected = -1;
	private ulong _openedAtMs;

	public static void Open(Node context, string titleKey, string shopId, ShopEntry[] entries)
	{
		var ui = new ShopUI { _titleKey = titleKey, _shopId = shopId, _entries = entries ?? System.Array.Empty<ShopEntry>() };
		context.GetTree().Root.AddChild(ui);
	}

	public override void _Ready()
	{
		Layer = 50;
		ProcessMode = ProcessModeEnum.Always;
		_openedAtMs = Time.GetTicksMsec();
		GetTree().Paused = true;
		Build();
		Refresh();
		if (_slots.Count > 0)
			_slots[0].GrabFocus();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// The press that opened it (e.g. the one closing the greeting dialogue) must not close it too.
		if (Time.GetTicksMsec() - _openedAtMs < 150)
			return;
		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void Close()
	{
		GetTree().Paused = false;
		QueueFree();
	}

	private int Remaining(ShopEntry entry) =>
		entry.Stock <= 0 ? int.MaxValue : Mathf.Max(0, entry.Stock - SaveManager.Instance.GetShopPurchased(_shopId, entry.Item.Id));

	private static int Held(Item item) =>
		item.Stackable ? SaveManager.Instance.GetItemCount(item.Id) : (SaveManager.Instance.HasItem(item.Id) ? 1 : 0);

	private void Build()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		_root.AddChild(box);

		var header = new HBoxContainer();
		var title = new Label { Text = _titleKey, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddChild(title);
		_goldLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		JekyriaFrame.StyleHeader(_goldLabel, 22);
		header.AddChild(_goldLabel);
		box.AddChild(header);

		var gridCenter = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		var grid = new GridContainer { Columns = Columns };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		gridCenter.AddChild(grid);
		box.AddChild(gridCenter);

		for (int i = 0; i < _entries.Length; i++)
		{
			if (_entries[i]?.Item is null)
				continue;
			Button slot = BuildSlot(_entries[i], _slots.Count);
			grid.AddChild(slot);
			_slots.Add(slot);
		}
		// Empty placeholder slots fill out the last row so the grid always reads as a full shelf.
		int filled = _slots.Count;
		int total = Mathf.Max(Columns, Mathf.CeilToInt(filled / (float)Columns) * Columns);
		for (int i = filled; i < total; i++)
			grid.AddChild(BuildEmptySlot());

		box.AddChild(new HSeparator());

		var detailsRow = new HBoxContainer();
		detailsRow.AddThemeConstantOverride("separation", 16);
		var detailsText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_nameLabel = new Label();
		JekyriaFrame.StyleHeader(_nameLabel, 22);
		_descLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(0f, 44f) };
		_descLabel.AddThemeFontSizeOverride("font_size", 16);
		detailsText.AddChild(_nameLabel);
		detailsText.AddChild(_descLabel);
		detailsRow.AddChild(detailsText);
		_priceLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
		_priceLabel.AddThemeFontSizeOverride("font_size", 18);
		detailsRow.AddChild(_priceLabel);
		box.AddChild(detailsRow);

		_statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeFontSizeOverride("font_size", 16);
		box.AddChild(_statusLabel);

		var hint = new Label { Text = "UI_SHOP_HINT", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(0.75f, 0.75f, 0.75f) };
		hint.AddThemeFontSizeOverride("font_size", 14);
		box.AddChild(hint);

		JekyriaFrame.Dress(_root, box, title, new Vector2(640f, 470f));

		// Floating name tag shown over the slot being pointed at.
		_nameTag = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 10 };
		var tagStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.035f, 0.03f, 0.95f),
			BorderColor = SlotBorderHot,
			ContentMarginLeft = 8f,
			ContentMarginRight = 8f,
			ContentMarginTop = 3f,
			ContentMarginBottom = 3f,
		};
		tagStyle.SetBorderWidthAll(1);
		tagStyle.SetCornerRadiusAll(3);
		_nameTag.AddThemeStyleboxOverride("panel", tagStyle);
		_nameTagLabel = new Label();
		_nameTagLabel.AddThemeFontSizeOverride("font_size", 16);
		_nameTagLabel.AddThemeColorOverride("font_color", GoldText);
		_nameTag.AddChild(_nameTagLabel);
		_root.AddChild(_nameTag);
	}

	private static StyleBoxFlat SlotStyle(Color border, int borderWidth)
	{
		var style = new StyleBoxFlat { BgColor = SlotBg, BorderColor = border };
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(4);
		style.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
		style.ShadowSize = 3;
		return style;
	}

	private Control BuildEmptySlot()
	{
		var slot = new Panel { CustomMinimumSize = new Vector2(SlotSize, SlotSize), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = SlotStyle(SlotBorder.Darkened(0.5f), 1);
		style.BgColor = SlotBg.Darkened(0.3f);
		slot.AddThemeStyleboxOverride("panel", style);
		return slot;
	}

	private Button BuildSlot(ShopEntry entry, int index)
	{
		var slot = new Button { CustomMinimumSize = new Vector2(SlotSize, SlotSize), FocusMode = Control.FocusModeEnum.All };
		slot.AddThemeStyleboxOverride("normal", SlotStyle(SlotBorder, 2));
		slot.AddThemeStyleboxOverride("hover", SlotStyle(SlotBorderHot, 2));
		slot.AddThemeStyleboxOverride("focus", SlotStyle(SlotBorderHot, 3));
		slot.AddThemeStyleboxOverride("pressed", SlotStyle(Colors.White, 3));
		slot.AddThemeStyleboxOverride("disabled", SlotStyle(SlotBorder.Darkened(0.4f), 2));

		Item item = entry.Item;
		if (item.Icon is not null)
		{
			var icon = new TextureRect
			{
				Name = "Icon",
				Texture = item.Icon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			icon.OffsetLeft = 12f;
			icon.OffsetTop = 10f;
			icon.OffsetRight = -12f;
			icon.OffsetBottom = -14f;
			slot.AddChild(icon);
		}
		else
		{
			// No icon yet: the item's initial, so the slot still reads as "something".
			string initial = TranslationServer.Translate(item.ItemName).ToString();
			slot.Text = initial.Length > 0 ? initial.Substring(0, 1) : "?";
		}

		var count = new Label { Name = "Count", HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
		count.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		count.OffsetLeft = -40f;
		count.OffsetRight = -5f;
		count.OffsetTop = 2f;
		count.AddThemeFontSizeOverride("font_size", 13);
		count.AddThemeConstantOverride("outline_size", 4);
		count.AddThemeColorOverride("font_outline_color", Colors.Black);
		slot.AddChild(count);

		var price = new Label { Name = "Price", Text = entry.Price.ToString(), MouseFilter = Control.MouseFilterEnum.Ignore };
		price.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		price.OffsetLeft = 5f;
		price.OffsetTop = -19f;
		price.OffsetBottom = -2f;
		price.AddThemeFontSizeOverride("font_size", 13);
		price.AddThemeColorOverride("font_color", GoldText);
		price.AddThemeConstantOverride("outline_size", 4);
		price.AddThemeColorOverride("font_outline_color", Colors.Black);
		slot.AddChild(price);

		slot.MouseEntered += () => slot.GrabFocus();
		slot.FocusEntered += () => Select(index);
		slot.Pressed += () => TryBuy(index);
		return slot;
	}

	private ShopEntry EntryAt(int slotIndex)
	{
		int seen = 0;
		foreach (ShopEntry entry in _entries)
		{
			if (entry?.Item is null)
				continue;
			if (seen++ == slotIndex)
				return entry;
		}
		return null;
	}

	private void Select(int index)
	{
		_selected = index;
		ShopEntry entry = EntryAt(index);
		if (entry is null)
			return;

		Item item = entry.Item;
		string name = TranslationServer.Translate(item.ItemName);
		_nameLabel.Text = name;
		_descLabel.Text = TranslationServer.Translate(item.Description);
		_statusLabel.Text = "";
		RefreshPrice(entry);

		_nameTagLabel.Text = name;
		_nameTag.Visible = true;
		_nameTag.ResetSize();
	}

	// Re-placed every frame rather than once on select: the grid only lays its slots out after the
	// first frame, so a one-shot position taken at open time lands in the window's corner.
	public override void _Process(double delta)
	{
		if (!_nameTag.Visible || _selected < 0 || _selected >= _slots.Count)
			return;

		Rect2 slotRect = _slots[_selected].GetGlobalRect();
		Vector2 tagSize = _nameTag.Size;
		_nameTag.GlobalPosition = new Vector2(slotRect.GetCenter().X - tagSize.X / 2f, slotRect.Position.Y - tagSize.Y - 6f);
	}

	private void RefreshPrice(ShopEntry entry)
	{
		int held = Held(entry.Item);
		string heldText = held > 0 ? string.Format(TranslationServer.Translate("UI_SHOP_HELD"), held) + "\n" : "";
		_priceLabel.Text = heldText + string.Format(TranslationServer.Translate("UI_SHOP_PRICE"), entry.Price);
		_priceLabel.AddThemeColorOverride("font_color", SaveManager.Instance.Gold >= entry.Price ? GoldText : BadText);
	}

	private void TryBuy(int index)
	{
		ShopEntry entry = EntryAt(index);
		if (entry is null)
			return;

		Button slot = _slots[index];
		Item item = entry.Item;
		if (Remaining(entry) <= 0)
		{
			ShowStatus("UI_SHOP_SOLD_OUT", BadText);
			Shake(slot);
			return;
		}
		if (!item.Stackable && SaveManager.Instance.HasItem(item.Id))
		{
			ShowStatus("UI_SHOP_ALREADY_OWNED", BadText);
			Shake(slot);
			return;
		}
		if (!SaveManager.Instance.SpendGold(entry.Price))
		{
			ShowStatus("UI_SHOP_NO_GOLD", BadText);
			Shake(slot);
			return;
		}

		PlayerInventory inventory = (GetTree().GetFirstNodeInGroup("player") as Node)?.GetNodeOrNull<PlayerInventory>("Inventory");
		if (inventory is not null)
			inventory.CollectItem(item);
		else if (item.Stackable)
			SaveManager.Instance.AddItemCount(item.Id, 1);
		else
			SaveManager.Instance.CollectItem(item.Id);
		if (entry.Stock > 0)
			SaveManager.Instance.AddShopPurchase(_shopId, item.Id);

		ShowStatus(string.Format(TranslationServer.Translate("UI_SHOP_BOUGHT"), TranslationServer.Translate(item.ItemName)), GoodText);
		Tween flash = CreateTween();
		slot.Modulate = new Color(1.8f, 1.6f, 1.2f);
		flash.TweenProperty(slot, "modulate", Colors.White, 0.3f);
		Refresh();
	}

	private void ShowStatus(string text, Color color)
	{
		_statusLabel.Text = text;
		_statusLabel.AddThemeColorOverride("font_color", color);
	}

	// Refusal feedback: a quick red flash (not a position shake — the grid owns slot positions).
	private void Shake(Control slot)
	{
		slot.Modulate = new Color(1.7f, 0.45f, 0.4f);
		CreateTween().TweenProperty(slot, "modulate", Colors.White, 0.35f);
	}

	private void Refresh()
	{
		_goldLabel.Text = string.Format(TranslationServer.Translate("UI_SHOP_GOLD"), SaveManager.Instance.Gold);

		for (int i = 0; i < _slots.Count; i++)
		{
			ShopEntry entry = EntryAt(i);
			Button slot = _slots[i];
			int remaining = Remaining(entry);
			bool soldOut = remaining <= 0 || (!entry.Item.Stackable && SaveManager.Instance.HasItem(entry.Item.Id));

			var count = slot.GetNode<Label>("Count");
			count.Text = soldOut ? "" : entry.Stock > 0 ? $"x{remaining}" : "";
			var price = slot.GetNode<Label>("Price");
			price.Text = soldOut ? TranslationServer.Translate("UI_SHOP_SOLD_OUT_TAG") : entry.Price.ToString();
			price.AddThemeColorOverride("font_color", soldOut ? BadText : GoldText);
			if (slot.GetNodeOrNull<TextureRect>("Icon") is TextureRect icon)
				icon.Modulate = soldOut ? new Color(0.35f, 0.35f, 0.35f) : Colors.White;
		}

		if (_selected >= 0 && EntryAt(_selected) is ShopEntry selected)
			RefreshPrice(selected);
	}
}
