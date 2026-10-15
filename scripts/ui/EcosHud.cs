using Godot;

namespace Metroidvania.UI;

// Ecos counter in the top-right corner, right under the gold bag: a glowing cyan wisp (the soft
// neon dot the rest of the kit uses — cyan so it never reads as the Bastón's purple shadow
// essence) with the amount to its left in the Jekyria digits. Pops like GoldHud on change.
public partial class EcosHud : Control
{
	private const string FontPath = "res://resources/ui/JekyriaFont.res";
	private const string DotPath = "res://resources/particles/SoftDotGradient.tres";

	[Export] public float IconSize = 40f;
	[Export] public int FontSize = 34;
	// Top-right margin; Y sits below GoldHud (its margin + icon height + a gap).
	[Export] public Vector2 Margin = new(26f, 84f);
	[Export] public Color WispColor = new(0.45f, 0.95f, 1f, 1f);

	private Label _amount;
	private Tween _pop;
	private int _shown = -1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 1f;
		AnchorRight = 1f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		OffsetRight = -Margin.X;
		OffsetLeft = -Margin.X - 260f;
		OffsetTop = Margin.Y;
		OffsetBottom = Margin.Y + IconSize;

		var icon = new TextureRect
		{
			Texture = GD.Load<Texture2D>(DotPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = WispColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			AnchorLeft = 1f,
			AnchorRight = 1f,
			OffsetLeft = -IconSize,
			OffsetRight = 0f,
			OffsetTop = 0f,
			OffsetBottom = IconSize,
		};
		AddChild(icon);
		// A brighter core so it reads as a wisp, not a smudge.
		float core = IconSize * 0.45f;
		icon.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>(DotPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = WispColor.Lightened(0.6f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Position = new Vector2((IconSize - core) / 2f, (IconSize - core) / 2f),
			Size = new Vector2(core, core),
		});

		_amount = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetLeft = 0f,
			OffsetRight = -IconSize - 4f,
			OffsetTop = 0f,
			OffsetBottom = IconSize,
			PivotOffset = new Vector2(220f, IconSize / 2f),
			Modulate = new Color(0.85f, 1.1f, 1.2f),
		};
		_amount.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
		_amount.AddThemeFontSizeOverride("font_size", FontSize);
		AddChild(_amount);
	}

	public void SetEcos(int ecos)
	{
		if (_amount is null)
			return;
		_amount.Text = ecos.ToString();
		if (_shown >= 0 && ecos != _shown)
		{
			_pop?.Kill();
			_amount.Scale = Vector2.One * 1.2f;
			_pop = CreateTween();
			_pop.TweenProperty(_amount, "scale", Vector2.One, 0.2f).SetEase(Tween.EaseType.Out);
		}
		_shown = ecos;
	}
}
