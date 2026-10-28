using Godot;

namespace Metroidvania.UI;

// The boss health bar at the bottom of the HUD (scenes/ui/BarraVidaJefe.tscn): a BossBarStyle's
// frame (with its empty channel) and the red fill on top, plus the boss's name (a translation key)
// above it. Player.TrackBoss drives it.
public partial class BossHealthBar : Control
{
	[Export] public BossBarStyle DefaultStyle;
	// The style textures are ~450px wide; this scales the whole bar (frame, fill, name).
	[Export] public float BarScale = 0.65f;
	[Export] public float FadeTime = 0.35f;
	[Export] public float NameHeight = 24f;
	// How far the name dips into the frame's top decoration.
	[Export] public float NameOverlap = 8f;
	// The bar sits at the top centre (the bottom one covered the player, whose feet are near the
	// screen's bottom edge in most rooms); false = the original bottom-centre placement.
	[Export] public bool AtTop = true;
	[Export] public float TopMargin = 10f;
	[Export] public bool ShowName = false;

	private TextureProgressBar _bar;
	private Label _name;
	private Tween _fadeTween;
	// Set by the first Close() until the next Open(): a later Close() (e.g. Player dropping the boss
	// the moment it's freed) must not cut short the delayed one the death already started.
	private bool _closing;

	public override void _Ready()
	{
		_bar = GetNode<TextureProgressBar>("Bar");
		_name = GetNode<Label>("Name");
		Scale = new Vector2(BarScale, BarScale);
		if (AtTop)
		{
			AnchorTop = 0f;
			AnchorBottom = 0f;
			OffsetTop = TopMargin;
			OffsetBottom = TopMargin;
		}
		_name.Visible = ShowName;
		ApplyStyle(DefaultStyle);
	}

	// Null = DefaultStyle.
	public void ApplyStyle(BossBarStyle style)
	{
		style ??= DefaultStyle;
		if (style is null || _bar is null)
			return;
		Vector2 size = style.Frame?.GetSize() ?? _bar.Size;
		_bar.TextureUnder = style.Frame;
		_bar.TextureProgress = style.Fill;
		_bar.TextureProgressOffset = style.FillOffset;
		_bar.Size = size;
		// Centred on this control's anchor point (top or bottom centre of the screen), hanging
		// down from it at the top and sitting on it at the bottom.
		_bar.Position = new Vector2(-size.X * 0.5f, AtTop ? 0f : -size.Y);
		_name.Size = new Vector2(size.X, NameHeight);
		_name.Position = new Vector2(-size.X * 0.5f, -size.Y - NameHeight + NameOverlap);
	}

	// Shows the bar for a new boss at its current health, fading in.
	public void Open(BossBarStyle style, string nameKey, float ratio)
	{
		_closing = false;
		ApplyStyle(style);
		_name.Text = nameKey ?? "";
		_name.Visible = !string.IsNullOrEmpty(nameKey);
		SetRatio(ratio);
		if (!Visible)
			Modulate = new Color(Modulate, 0f);
		Visible = true;
		Fade(1f);
	}

	public void Close(float delay = 0f)
	{
		if (!Visible || _closing)
			return;
		_closing = true;
		Fade(0f, hideAfter: true, delay);
	}

	public void SetRatio(float ratio) => _bar.Value = Mathf.Clamp(ratio, 0f, 1f) * 100.0;

	private void Fade(float to, bool hideAfter = false, float delay = 0f)
	{
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		if (delay > 0f)
			_fadeTween.TweenInterval(delay);
		_fadeTween.TweenProperty(this, "modulate:a", to, FadeTime);
		if (hideAfter)
			_fadeTween.TweenCallback(Callable.From(() => Visible = false));
	}
}
