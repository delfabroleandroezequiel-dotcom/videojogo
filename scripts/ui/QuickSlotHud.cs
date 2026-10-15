using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

// The quick bar in the bottom-right corner: SaveManager.QuickSlotCount squares, the selected one
// lit, each showing its item (icon, or a dot in the resin's element colour when it has none) and
// how many are held, with the selected item's name above. Redraws on QuickSlotsChanged and when
// counts change (Refresh is cheap, so it also runs a few times a second).
public partial class QuickSlotHud : Control
{
	private const string DotPath = "res://resources/particles/SoftDotGradient.tres";
	private static readonly Color SlotBg = new(0.07f, 0.06f, 0.05f, 0.85f);
	private static readonly Color SlotBorder = new(0.45f, 0.36f, 0.2f);
	private static readonly Color SlotBorderHot = new(1f, 0.82f, 0.35f);

	[Export] public float SlotSize = 46f;
	[Export] public Vector2 Margin = new(18f, 18f);

	private readonly Panel[] _slots = new Panel[SaveManager.QuickSlotCount];
	private readonly TextureRect[] _icons = new TextureRect[SaveManager.QuickSlotCount];
	private readonly Label[] _counts = new Label[SaveManager.QuickSlotCount];
	private Label _name;
	private float _refreshTimer;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// Bottom-right corner (the bottom-left one belongs to the Bastón's shadow slots).
		float width = SaveManager.QuickSlotCount * (SlotSize + 6f);
		AnchorLeft = 1f;
		AnchorRight = 1f;
		AnchorTop = 1f;
		AnchorBottom = 1f;
		OffsetLeft = -Margin.X - width;
		OffsetRight = -Margin.X;
		OffsetTop = -Margin.Y - SlotSize - 24f;
		OffsetBottom = -Margin.Y;

		_name = new Label
		{
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Right,
			Position = new Vector2(-120f, 0f),
			Size = new Vector2(width + 120f, 22f),
		};
		_name.AddThemeFontSizeOverride("font_size", 16);
		_name.AddThemeColorOverride("font_color", SlotBorderHot);
		AddChild(_name);

		for (int i = 0; i < SaveManager.QuickSlotCount; i++)
		{
			var slot = new Panel
			{
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(i * (SlotSize + 6f), 24f),
				Size = new Vector2(SlotSize, SlotSize),
			};
			AddChild(slot);
			var icon = new TextureRect
			{
				MouseFilter = MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Position = new Vector2(6f, 6f),
				Size = new Vector2(SlotSize - 12f, SlotSize - 12f),
			};
			slot.AddChild(icon);
			var count = new Label
			{
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Right,
				Position = new Vector2(0f, SlotSize - 20f),
				Size = new Vector2(SlotSize - 4f, 18f),
			};
			count.AddThemeFontSizeOverride("font_size", 14);
			count.AddThemeColorOverride("font_outline_color", Colors.Black);
			count.AddThemeConstantOverride("outline_size", 4);
			slot.AddChild(count);
			_slots[i] = slot;
			_icons[i] = icon;
			_counts[i] = count;
		}

		SaveManager.Instance.QuickSlotsChanged += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (SaveManager.Instance is not null)
			SaveManager.Instance.QuickSlotsChanged -= Refresh;
	}

	public override void _Process(double delta)
	{
		_refreshTimer -= (float)delta;
		if (_refreshTimer > 0f)
			return;
		_refreshTimer = 0.25f;
		Refresh();
	}

	private void Refresh()
	{
		SaveManager save = SaveManager.Instance;
		for (int i = 0; i < SaveManager.QuickSlotCount; i++)
		{
			bool selected = i == save.SelectedQuickSlot;
			var style = new StyleBoxFlat { BgColor = SlotBg, BorderColor = selected ? SlotBorderHot : SlotBorder };
			style.SetBorderWidthAll(selected ? 3 : 1);
			style.SetCornerRadiusAll(4);
			_slots[i].AddThemeStyleboxOverride("panel", style);

			Item item = ItemDatabase.Instance.Get(save.GetQuickSlot(i));
			if (item is null)
			{
				_icons[i].Texture = null;
				_counts[i].Text = "";
				continue;
			}
			int held = save.GetItemCount(item.Id);
			_icons[i].Texture = item.Icon ?? GD.Load<Texture2D>(DotPath);
			_icons[i].Modulate = (item.Icon is null ? ElementColor(item.ImbueElement) : Colors.White) with { A = held > 0 ? 1f : 0.3f };
			_counts[i].Text = held.ToString();
		}
		Item current = ItemDatabase.Instance.Get(save.SelectedQuickItem);
		_name.Text = current is null ? "" : TranslationServer.Translate(current.ItemName);
	}

	public static Color ElementColor(DamageElement element) => element switch
	{
		DamageElement.Fire => new Color(1f, 0.5f, 0.15f),
		DamageElement.Ice => new Color(0.62f, 0.93f, 1f),
		DamageElement.Poison => new Color(0.45f, 1f, 0.35f),
		DamageElement.Lightning => new Color(1f, 0.9f, 0.2f),
		_ => Colors.White,
	};
}
