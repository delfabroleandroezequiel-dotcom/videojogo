using Godot;

namespace Metroidvania.UI;

// Ecos counter in the top-right corner, right under the gold bag: a glowing cyan wisp (the soft
// neon dot the rest of the kit uses — cyan so it never reads as the Bastón's purple shadow
// essence) with the amount to its left in the Jekyria digits. Gains roll up one by one while the
// number swells and glows (each EcosBurst orb landing re-kicks it); losses snap straight down.
public partial class EcosHud : Control
{
	private const string FontPath = "res://resources/ui/JekyriaFont.res";
	private const string DotPath = "res://resources/particles/SoftDotGradient.tres";

	[Export] public float IconSize = 40f;
	[Export] public int FontSize = 34;
	// Top-right margin; Y sits below GoldHud (its margin + icon height + a gap).
	[Export] public Vector2 Margin = new(26f, 84f);
	[Export] public Color WispColor = new(0.45f, 0.95f, 1f, 1f);

	// Roll-up pace: this long per Ecos counted, clamped so small gains still tick and big ones
	// don't drag on.
	private const float RollSecondsPerEcos = 0.03f;
	private const float RollMinSeconds = 0.12f;
	private const float RollMaxSeconds = 0.9f;
	private const float SwellScale = 1.25f;
	private static readonly Color AmountColor = new(0.85f, 1.1f, 1.2f);
	private static readonly Color AmountGlowColor = new(1.4f, 1.9f, 2f);

	private Label _amount;
	private TextureRect _icon;
	private Tween _pop;
	private Tween _roll;
	private int _shown = -1;
	private float _displayed;

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

		var icon = _icon = new TextureRect
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
			Modulate = AmountColor,
		};
		_icon.PivotOffset = new Vector2(IconSize / 2f, IconSize / 2f);
		_amount.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
		_amount.AddThemeFontSizeOverride("font_size", FontSize);
		AddChild(_amount);
	}

	public void SetEcos(int ecos)
	{
		// Can be reached mid scene swap (an EcosBurst banking its leftovers as the old map frees), when
		// this HUD's nodes may already be gone.
		if (!IsInstanceValid(this) || !IsInstanceValid(_amount) || !IsInstanceValid(_icon))
			return;
		bool gain = _shown >= 0 && ecos > _shown;
		_shown = ecos;
		_roll?.Kill();
		if (!gain)
		{
			// First fill, a spend or a death drop: no fanfare.
			_displayed = ecos;
			_amount.Text = ecos.ToString();
			return;
		}

		float duration = Mathf.Clamp((ecos - _displayed) * RollSecondsPerEcos, RollMinSeconds, RollMaxSeconds);
		_roll = CreateTween();
		_roll.TweenMethod(Callable.From<float>(SetDisplayed), _displayed, (float)ecos, duration);

		// Swell and glow while counting, settle once the roll lands.
		_pop?.Kill();
		_amount.Scale = Vector2.One * SwellScale;
		_amount.Modulate = AmountGlowColor;
		_icon.Scale = Vector2.One * 1.3f;
		_pop = CreateTween();
		_pop.SetParallel();
		_pop.TweenProperty(_icon, "scale", Vector2.One, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		_pop.TweenProperty(_amount, "scale", Vector2.One, 0.2f).SetDelay(duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		_pop.TweenProperty(_amount, "modulate", AmountColor, 0.3f).SetDelay(duration);
	}

	private void SetDisplayed(float value)
	{
		_displayed = value;
		if (!IsInstanceValid(_amount))
			return;
		_amount.Text = Mathf.FloorToInt(value).ToString();
	}
}
