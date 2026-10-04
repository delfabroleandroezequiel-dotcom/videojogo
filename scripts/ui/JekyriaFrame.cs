using Godot;
using System.Collections.Generic;

namespace Metroidvania.UI;

// Window frame from the Jekyria HUD kit: the vine panel of HUDChat.png (alpha bounds x 463..923,
// y 448..738), rebuilt at any size. A plain NinePatch would smear the top/bottom centre diamonds and
// the corner vines, so the image is cut into fixed pieces and plain border runs (found by scanning
// column/row differences) that get tiled: horizontally x 614..642 and x 840..868 (one each side of
// the centre diamond, so it stays centred), vertically y 578..606 (below the vines hanging from the top edge).
public static class JekyriaFrame
{
	private const string SheetPath = "res://assets/sprites/deco/HUDChat.png";
	private const string FontPath = "res://resources/ui/JekyriaFont.res";
	private const string ThemePath = "res://resources/ui/JekyriaMenuTheme.res";
	private static readonly Rect2I PanelRect = new(463, 448, 461, 291);
	private static readonly (int Start, int End)[] HorizontalTiles = { (614, 642), (840, 868) };
	private static readonly (int Start, int End)[] VerticalTiles = { (578, 606) };
	// Distance from the panel's outer bounds to the inside of its gold border (sheet pixels).
	public static readonly Vector2 ContentInset = new(46f, 44f);

	private static Image _sheet;

	// Image of the frame at `size` sheet pixels (draw it scaled down for a thinner border).
	public static Image Build(Vector2I size)
	{
		if (_sheet is null)
		{
			_sheet = GD.Load<Texture2D>(SheetPath).GetImage();
			if (_sheet.IsCompressed())
				_sheet.Decompress();
			_sheet.Convert(Image.Format.Rgba8);
		}
		Image source = _sheet.GetRegion(PanelRect);

		var columns = Segments(PanelRect.Position.X, PanelRect.Size.X, HorizontalTiles, size.X);
		var wide = Image.CreateEmpty(size.X, source.GetHeight(), false, Image.Format.Rgba8);
		foreach ((int src, int dst, int len) in columns)
			wide.BlitRect(source, new Rect2I(src, 0, len, source.GetHeight()), new Vector2I(dst, 0));

		var rows = Segments(PanelRect.Position.Y, PanelRect.Size.Y, VerticalTiles, size.Y);
		var result = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
		foreach ((int src, int dst, int len) in rows)
			result.BlitRect(wide, new Rect2I(0, src, size.X, len), new Vector2I(0, dst));
		return result;
	}

	// Maps a target length onto the source axis: fixed pieces copied once, each tile run repeated to
	// share the extra length equally. Returns (source offset, target offset, length) strips.
	private static List<(int, int, int)> Segments(int origin, int sourceLength, (int Start, int End)[] tiles, int targetLength)
	{
		int extra = Mathf.Max(0, targetLength - sourceLength);
		var strips = new List<(int, int, int)>();
		int src = 0;
		int dst = 0;
		for (int i = 0; i < tiles.Length; i++)
		{
			int tileStart = tiles[i].Start - origin;
			int tileWidth = tiles[i].End - tiles[i].Start;
			strips.Add((src, dst, tileStart - src));
			dst += tileStart - src;
			int share = extra / tiles.Length + (i < extra % tiles.Length ? 1 : 0);
			for (int filled = 0; filled < share; filled += tileWidth)
			{
				int len = Mathf.Min(tileWidth, share - filled);
				strips.Add((tileStart, dst, len));
				dst += len;
			}
			src = tileStart;
		}
		strips.Add((src, dst, sourceLength - src));
		return strips;
	}

	// Dresses an overlay window: Jekyria theme on `panel`, the frame centred behind `content`
	// (resized to `size` screen pixels, border drawn at `borderScale`), and `title` in the gold
	// Jekyria font.
	public static void Dress(Control panel, Control content, Label title, Vector2 size, float borderScale = 0.6f)
	{
		panel.Theme = GD.Load<Theme>(ThemePath);

		var frameSize = (Vector2I)(size / borderScale).Round();
		var frame = new TextureRect
		{
			Name = "JekyriaFrame",
			Texture = ImageTexture.CreateFromImage(Build(frameSize)),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		CenterOn(frame, size);
		panel.AddChild(frame);
		panel.MoveChild(frame, content.GetIndex());

		Vector2 inset = ContentInset * borderScale + new Vector2(10f, 6f);
		CenterOn(content, size - inset * 2f);

		if (title is not null)
		{
			title.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
			title.AddThemeFontSizeOverride("font_size", 30);
		}
	}

	private static void CenterOn(Control control, Vector2 size)
	{
		control.AnchorLeft = 0.5f;
		control.AnchorRight = 0.5f;
		control.AnchorTop = 0.5f;
		control.AnchorBottom = 0.5f;
		control.OffsetLeft = -size.X / 2f;
		control.OffsetRight = size.X / 2f;
		control.OffsetTop = -size.Y / 2f;
		control.OffsetBottom = size.Y / 2f;
	}

	// Section header (e.g. "En curso") in the Jekyria font, tinted.
	public static void StyleHeader(Label label, int fontSize = 22)
	{
		// The gold glyphs read dark at small sizes — lift them like the gold counter does.
		label.SelfModulate = new Color(1.35f, 1.25f, 1.1f);
		// The scene tints its headers (yellow/green); on a textured gold font that tint just muddies it.
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
		label.AddThemeFontSizeOverride("font_size", fontSize);
	}

	// Moves `nodes` (in order) out of their parent into a scrolling column that takes the parent's
	// remaining height, so long lists never spill out of the frame.
	public static VBoxContainer WrapInScroll(Container parent, int index, params Control[] nodes)
	{
		var scroll = new ScrollContainer
		{
			Name = "Scroll",
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(column);
		parent.AddChild(scroll);
		parent.MoveChild(scroll, index);
		foreach (Control node in nodes)
			node.Reparent(column, false);
		return column;
	}
}
