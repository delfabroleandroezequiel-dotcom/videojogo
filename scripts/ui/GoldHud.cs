using Godot;

namespace Metroidvania.UI;

// Gold counter in the top-right corner: the coin-bag icon from the Jekyria HUD kit (HUDChat.png,
// measured: x 1153..1272, y 218..360) with the amount to its left in the Jekyria gold digits.
// The number pops slightly when it changes.
public partial class GoldHud : Control
{
	private const string SheetPath = "res://assets/sprites/deco/HUDChat.png";
	private const string FontPath = "res://resources/ui/JekyriaFont.res";
	private static readonly Rect2I BagRect = new(1153, 218, 120, 143);

	[Export] public float IconHeight = 64f;
	[Export] public int FontSize = 40;
	[Export] public Vector2 Margin = new(14f, 10f);

	private Label _amount;
	private Tween _pop;
	private int _shown = -1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Image sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		Image bag = sheet.GetRegion(BagRect);
		float scale = IconHeight / bag.GetHeight();
		float iconWidth = bag.GetWidth() * scale;

		// Anchored to the top-right corner; children laid out right-to-left from it.
		AnchorLeft = 1f;
		AnchorRight = 1f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		OffsetRight = -Margin.X;
		OffsetLeft = -Margin.X - 260f;
		OffsetTop = Margin.Y;
		OffsetBottom = Margin.Y + IconHeight;

		var icon = new TextureRect
		{
			Texture = ImageTexture.CreateFromImage(bag),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 1f,
			AnchorRight = 1f,
			OffsetLeft = -iconWidth,
			OffsetRight = 0f,
			OffsetTop = 0f,
			OffsetBottom = IconHeight,
		};
		AddChild(icon);

		_amount = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetLeft = 0f,
			OffsetRight = -iconWidth - 4f,
			OffsetTop = 0f,
			OffsetBottom = IconHeight,
			PivotOffset = new Vector2(220f, IconHeight / 2f),
		};
		_amount.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
		_amount.AddThemeFontSizeOverride("font_size", FontSize);
		// The gold glyphs read a little dark once scaled down — lift them a touch.
		_amount.Modulate = new Color(1.3f, 1.2f, 1.05f);
		AddChild(_amount);
	}

	public void SetGold(int gold)
	{
		if (_amount is null)
			return;
		_amount.Text = gold.ToString();
		if (_shown >= 0 && gold != _shown)
		{
			_pop?.Kill();
			_amount.Scale = Vector2.One * 1.2f;
			_pop = CreateTween();
			_pop.TweenProperty(_amount, "scale", Vector2.One, 0.2f).SetEase(Tween.EaseType.Out);
		}
		_shown = gold;
	}
}
