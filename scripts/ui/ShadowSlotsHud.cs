using Godot;
using Metroidvania.Save;

namespace Metroidvania.UI;

// Bastón mini HUD: 5 squares in the bottom-left corner, one per summon slot (stable order), drawn with
// the square frames of the Jekyria HUD kit (HUd2.png, measured alpha bounds: plain gold frame
// x 784..907 y 616..741, purple-gem frame x 483..613 y 609..743). Empty slot = dimmed plain frame,
// occupied = plain frame, selected = purple-gem frame with a slight pop. An occupied slot shows the
// enemy's initial (to be swapped for an icon later) and its use count.
public partial class ShadowSlotsHud : Control
{
	private const string SheetPath = "res://assets/sprites/deco/HUd2.png";
	private static readonly Rect2I PlainFrameRect = new(784, 616, 124, 126);
	private static readonly Rect2I SelectedFrameRect = new(483, 609, 131, 135);

	[Export] public float SlotSize = 44f;
	[Export] public float Spacing = 6f;
	[Export] public Vector2 Margin = new(16f, 16f);

	private readonly TextureRect[] _slots = new TextureRect[SaveManager.MaxShadowSlots];
	private readonly Label[] _letters = new Label[SaveManager.MaxShadowSlots];
	private readonly Label[] _counts = new Label[SaveManager.MaxShadowSlots];
	private Texture2D _plainFrame;
	private Texture2D _selectedFrame;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Image sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		_plainFrame = ImageTexture.CreateFromImage(sheet.GetRegion(PlainFrameRect));
		_selectedFrame = ImageTexture.CreateFromImage(sheet.GetRegion(SelectedFrameRect));

		float width = SaveManager.MaxShadowSlots * SlotSize + (SaveManager.MaxShadowSlots - 1) * Spacing;
		// Anchored to the bottom-left corner of the HUD layer.
		AnchorLeft = 0f;
		AnchorRight = 0f;
		AnchorTop = 1f;
		AnchorBottom = 1f;
		OffsetLeft = Margin.X;
		OffsetRight = Margin.X + width;
		OffsetTop = -Margin.Y - SlotSize;
		OffsetBottom = -Margin.Y;

		for (int i = 0; i < _slots.Length; i++)
		{
			var slot = new TextureRect
			{
				MouseFilter = MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Texture = _plainFrame,
				Position = new Vector2(i * (SlotSize + Spacing), 0f),
				Size = new Vector2(SlotSize, SlotSize),
				PivotOffset = new Vector2(SlotSize, SlotSize) / 2f,
			};
			var letter = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Size = new Vector2(SlotSize, SlotSize),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			letter.AddThemeFontSizeOverride("font_size", 20);
			var count = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				// Inset so the number sits inside the frame's dark area, not on the gold border.
				Size = new Vector2(SlotSize - 7f, SlotSize - 5f),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			count.AddThemeFontSizeOverride("font_size", 11);
			slot.AddChild(letter);
			slot.AddChild(count);
			AddChild(slot);
			_slots[i] = slot;
			_letters[i] = letter;
			_counts[i] = count;
		}
	}

	// Redraws all 5 squares from the SaveManager slots; `selectedKey` is highlighted.
	public void Refresh(string selectedKey)
	{
		if (_slots[0] is null)
			return;
		var keys = SaveManager.Instance.GetShadowSlotKeys();
		for (int i = 0; i < _slots.Length; i++)
		{
			bool filled = i < keys.Count;
			bool selected = filled && keys[i] == selectedKey;
			int uses = filled ? SaveManager.Instance.GetShadowUses(keys[i]) : 0;
			string name = filled ? ShadowNames.DisplayName(keys[i]) : "";

			_slots[i].Texture = selected ? _selectedFrame : _plainFrame;
			_slots[i].SelfModulate = filled ? Colors.White : new Color(0.55f, 0.52f, 0.5f, 0.75f);
			_slots[i].Scale = Vector2.One * (selected ? 1.15f : 1f);
			_letters[i].Text = filled && name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "";
			// A slot at 0 uses stays, greyed out until more are farmed.
			_letters[i].Modulate = selected ? new Color(1f, 0.95f, 1f) : uses > 0 ? new Color(0.85f, 0.8f, 0.95f) : new Color(0.5f, 0.5f, 0.55f);
			_counts[i].Text = filled ? uses.ToString() : "";
			_counts[i].Modulate = uses > 0 ? Colors.White : new Color(0.6f, 0.4f, 0.4f);
		}
	}
}
