using Godot;

namespace Metroidvania.UI;

// Tornado variant picker: one square per variant, stacked just above the Bastón slot row in the
// bottom-left corner, same frames as ShadowSlotsHud (HUd2.png: plain gold frame / purple-gem frame
// for the selected one). Each square shows the variant name's initial (icons later); hidden while
// the tornado spell isn't available.
public partial class TornadoVariantsHud : Control
{
	private const string SheetPath = "res://assets/sprites/deco/HUd2.png";
	private static readonly Rect2I PlainFrameRect = new(784, 616, 124, 126);
	private static readonly Rect2I SelectedFrameRect = new(483, 609, 131, 135);

	[Export] public float SlotSize = 36f;
	[Export] public float Spacing = 5f;
	// Bottom-left margin; Y leaves room for the Bastón row (44 + 16) underneath.
	[Export] public Vector2 Margin = new(16f, 72f);

	private TextureRect[] _slots = System.Array.Empty<TextureRect>();
	private Label[] _letters = System.Array.Empty<Label>();
	private Texture2D _plainFrame;
	private Texture2D _selectedFrame;

	// nameKeys: translation keys of the variants, in cycle order.
	public void Build(string[] nameKeys)
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Image sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		_plainFrame = ImageTexture.CreateFromImage(sheet.GetRegion(PlainFrameRect));
		_selectedFrame = ImageTexture.CreateFromImage(sheet.GetRegion(SelectedFrameRect));

		int count = nameKeys.Length;
		float width = count * SlotSize + (count - 1) * Spacing;
		AnchorLeft = 0f;
		AnchorRight = 0f;
		AnchorTop = 1f;
		AnchorBottom = 1f;
		OffsetLeft = Margin.X;
		OffsetRight = Margin.X + width;
		OffsetTop = -Margin.Y - SlotSize;
		OffsetBottom = -Margin.Y;

		_slots = new TextureRect[count];
		_letters = new Label[count];
		for (int i = 0; i < count; i++)
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
			letter.AddThemeFontSizeOverride("font_size", 16);
			slot.AddChild(letter);
			AddChild(slot);
			_slots[i] = slot;
			_letters[i] = letter;
		}
		_nameKeys = nameKeys;
	}

	private string[] _nameKeys = System.Array.Empty<string>();

	public void Refresh(int selected)
	{
		for (int i = 0; i < _slots.Length; i++)
		{
			bool isSelected = i == selected;
			string name = TranslationServer.Translate(_nameKeys[i]);
			_slots[i].Texture = isSelected ? _selectedFrame : _plainFrame;
			_slots[i].Scale = Vector2.One * (isSelected ? 1.15f : 1f);
			_slots[i].SelfModulate = isSelected ? Colors.White : new Color(0.8f, 0.78f, 0.75f, 0.85f);
			_letters[i].Text = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "";
			_letters[i].Modulate = isSelected ? Colors.White : new Color(0.75f, 0.75f, 0.8f);
		}
	}
}
