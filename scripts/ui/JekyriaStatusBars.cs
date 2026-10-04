using Godot;

namespace Metroidvania.UI;

// Player health + stamina in the Jekyria HUD art (HUDChat.png, the emblem-orb piece): the frame is
// drawn with its two rails emptied (darkened), and the original red/green rail art is laid on top,
// clipped to the current ratio — so the bars shrink inside the ornate frame. A pale "recent damage"
// segment trails behind each bar and catches up after a short delay (Souls-style).
// All coordinates below are measured from HUDChat.png (not guessed).
public partial class JekyriaStatusBars : Control
{
	private const string SheetPath = "res://assets/sprites/deco/HUDChat.png";
	private static readonly Rect2I Piece = new(13, 773, 682, 239);
	// Rails, in piece-local pixels.
	// The health rail runs to the tip (local x 617), but the art only paints red up to x 573 — the rest
	// is drawn empty. HealthArtRedEnd marks that, and the fill gets extended to the full rail.
	private static readonly Rect2I HealthRail = new(171, 85, 446, 27);
	private const int HealthArtRedEnd = 573;
	private static readonly Rect2I StaminaRail = new(172, 139, 357, 20);
	// Columns (piece-local) of a clean stretch of frame + both rails, repeated to lengthen the bar as
	// max health grows; the orb (left) and the tip ornament (right) stay as drawn.
	private const int StretchStart = 400;
	private const int StretchEnd = 450;

	[Export] public float HudScale = 0.45f;
	[Export] public float TrailDelay = 0.45f;
	[Export] public float TrailCatchUpTime = 0.35f;
	[Export] public Color TrailColor = new(1f, 0.8f, 0.55f, 0.45f);
	// Growth: at BaseMaxHealth the bar is its drawn length; every extra point of max health adds
	// PixelsPerHealth (art pixels), up to MaxExtraLength.
	[Export] public int BaseMaxHealth = 100;
	[Export] public float PixelsPerHealth = 2.5f;
	[Export] public float MaxExtraLength = 600f;

	private Bar _health;
	private Bar _stamina;
	private NinePatchRect _frame;
	private float _extra;

	private sealed class Bar
	{
		public Control Clip;
		public Control TrailClip;
		public NinePatchRect Fill;
		public ColorRect Trail;
		public float BaseWidth;
		public float Width;
		public float Ratio = 1f;
		public Tween TrailTween;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Piece.Size.X, Piece.Size.Y) * HudScale;

		Image sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		Image piece = sheet.GetRegion(Piece);
		ClearStrayTop(piece);

		// Frame with the rails emptied.
		Image frame = (Image)piece.Duplicate();
		EmptyRail(frame, HealthRail);
		EmptyRail(frame, StaminaRail);

		var root = new Control { Scale = Vector2.One * HudScale, MouseFilter = MouseFilterEnum.Ignore };
		AddChild(root);
		_frame = Stretchy(ImageTexture.CreateFromImage(frame), StretchStart, Piece.Size.X - StretchEnd);
		_frame.Size = new Vector2(Piece.Size.X, Piece.Size.Y);
		root.AddChild(_frame);
		_health = BuildBar(root, ExtendedFill(piece, HealthRail, HealthArtRedEnd), HealthRail);
		_stamina = BuildBar(root, ImageTexture.CreateFromImage(piece.GetRegion(StaminaRail)), StaminaRail);
	}

	// The big panel above it on the sheet hangs vine tips into this piece's top rows: keep only the
	// orb's top ornament there (a wedge widening downward from its tip, measured), clear the rest.
	private static void ClearStrayTop(Image image)
	{
		const int strayRows = 42;
		const float orbCenterX = 97f;
		for (int y = 0; y < strayRows; y++)
		{
			float halfWidth = y * 1.8f + 6f;
			for (int x = 0; x < image.GetWidth(); x++)
			{
				if (Mathf.Abs(x - orbCenterX) > halfWidth)
					image.SetPixel(x, y, Colors.Transparent);
			}
		}
	}

	// Darkens the colored fill inside a rail so the frame reads as an empty slot. Red must be strongly
	// red (so the gold border, also reddish, survives); green can be looser (gold is never green-dominant).
	private static void EmptyRail(Image image, Rect2I rail)
	{
		for (int y = rail.Position.Y; y < rail.End.Y; y++)
		{
			for (int x = rail.Position.X; x < rail.End.X; x++)
			{
				Color c = image.GetPixel(x, y);
				bool fill = c.A > 0.6f && (c.R > c.G * 1.7f && c.R > c.B * 1.6f || c.G > c.R * 1.12f && c.G > c.B * 1.05f);
				if (fill)
					image.SetPixel(x, y, new Color(0.07f, 0.05f, 0.05f, c.A));
			}
		}
	}

	// The rail art with its painted fill stretched to the rail end: columns past artFillEnd are copies of
	// a column from inside the fill, so a full bar reaches the tip.
	private static Texture2D ExtendedFill(Image piece, Rect2I rail, int artFillEnd)
	{
		Image fill = piece.GetRegion(rail);
		int from = artFillEnd - rail.Position.X;
		int source = from - 30;
		for (int x = from; x < fill.GetWidth(); x++)
			for (int y = 0; y < fill.GetHeight(); y++)
				fill.SetPixel(x, y, fill.GetPixel(source, y));
		return ImageTexture.CreateFromImage(fill);
	}

	private Bar BuildBar(Control root, Texture2D fillTexture, Rect2I rail)
	{
		var bar = new Bar { BaseWidth = rail.Size.X, Width = rail.Size.X };

		bar.TrailClip = new Control { Position = rail.Position, Size = rail.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
		bar.Trail = new ColorRect { Size = rail.Size, Color = TrailColor, MouseFilter = MouseFilterEnum.Ignore };
		bar.TrailClip.AddChild(bar.Trail);
		root.AddChild(bar.TrailClip);

		bar.Clip = new Control { Position = rail.Position, Size = rail.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
		bar.Fill = Stretchy(fillTexture, StretchStart - rail.Position.X, rail.End.X - StretchEnd);
		bar.Fill.Size = rail.Size;
		bar.Clip.AddChild(bar.Fill);
		root.AddChild(bar.Clip);
		return bar;
	}

	// Horizontal-only nine-patch: left/right margins stay as drawn, the middle columns tile.
	private static NinePatchRect Stretchy(Texture2D texture, int left, int right) => new()
	{
		Texture = texture,
		PatchMarginLeft = left,
		PatchMarginRight = right,
		AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
		MouseFilter = MouseFilterEnum.Ignore,
	};

	// Lengthens the whole bar (frame + both rails) for this max health.
	public void SetMaxHealth(int maxHealth)
	{
		float extra = Mathf.Clamp((maxHealth - BaseMaxHealth) * PixelsPerHealth, 0f, MaxExtraLength);
		if (_frame is null || Mathf.IsEqualApprox(extra, _extra))
			return;
		_extra = extra;
		_frame.Size = new Vector2(Piece.Size.X + extra, Piece.Size.Y);
		CustomMinimumSize = _frame.Size * HudScale;
		foreach (Bar bar in new[] { _health, _stamina })
		{
			bar.Width = bar.BaseWidth + extra;
			bar.Fill.Size = new Vector2(bar.Width, bar.Fill.Size.Y);
			bar.Trail.Size = new Vector2(bar.Width, bar.Trail.Size.Y);
			bar.Clip.Size = new Vector2(bar.Width * bar.Ratio, bar.Clip.Size.Y);
			bar.TrailClip.Size = new Vector2(bar.Width * bar.Ratio, bar.TrailClip.Size.Y);
		}
	}

	public void SetHealthRatio(float ratio) => SetRatio(_health, ratio);
	public void SetStaminaRatio(float ratio) => SetRatio(_stamina, ratio);

	private void SetRatio(Bar bar, float ratio)
	{
		if (bar is null)
			return;
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		float width = bar.Width * ratio;
		bar.Clip.Size = new Vector2(width, bar.Clip.Size.Y);

		bar.TrailTween?.Kill();
		if (ratio >= bar.Ratio)
		{
			// Healing/regen: the trail just follows.
			bar.TrailClip.Size = new Vector2(width, bar.TrailClip.Size.Y);
		}
		else
		{
			// Damage: the pale segment lingers, then catches up.
			bar.TrailTween = CreateTween();
			bar.TrailTween.TweenInterval(TrailDelay);
			bar.TrailTween.TweenProperty(bar.TrailClip, "size:x", width, TrailCatchUpTime).SetEase(Tween.EaseType.Out);
		}
		bar.Ratio = ratio;
	}
}
