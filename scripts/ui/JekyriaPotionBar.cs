using Godot;

namespace Metroidvania.UI;

// Heal charges as the red potion bar from the Jekyria HUD kit (HUd2.png, measured with alpha/red
// scans: bar x 21..243, y 754..867; pips at x 124/156/188, 24 px wide, y 800..823; plain track at
// x 151..153). The sheet's bar is drawn with 3 pips, so the image is rebuilt to fit MaxCharges:
// medallion + N pip cells of plain track + right cap, with one pip sprite per charge on top.
// Lit pip = charge available, dark pip = spent; the potion in the medallion drains to grey at 0.
public partial class JekyriaPotionBar : Control
{
	private const string SheetPath = "res://assets/sprites/deco/HUd2.png";
	private const int Top = 754;
	private const int Bottom = 868;
	private const int MedallionStart = 21;
	private const int MedallionEnd = 120;   // exclusive: track begins just before the first pip
	private const int CapStart = 212;
	private const int CapEnd = 244;
	private const int TrackSliceX = 151;
	private const int TrackSliceWidth = 3;
	private const int PipCell = 32;          // pip-to-pip spacing on the sheet
	private const int FirstPipOffset = 2;    // first pip's rim starts 2 px into its cell (x 122)
	private static readonly Rect2I PipRect = new(122, 797, 28, 30);

	[Export] public float HudScale = 0.45f;

	private Image _sheet;
	private TextureRect _frame;
	private TextureRect[] _pips = System.Array.Empty<TextureRect>();
	private ImageTexture _pipLit;
	private ImageTexture _pipDim;
	private ImageTexture _frameFull;
	private ImageTexture _frameEmpty;
	private int _max = -1;
	private int _current = -1;
	private Tween _pop;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (_sheet.IsCompressed())
			_sheet.Decompress();
		_sheet.Convert(Image.Format.Rgba8);

		Image pip = _sheet.GetRegion(PipRect);
		_pipLit = ImageTexture.CreateFromImage(pip);
		_pipDim = ImageTexture.CreateFromImage(Drained(pip, PipRect.Size.X, 0.12f));

		_frame = new TextureRect
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
		};
		AddChild(_frame);
	}

	public void SetCharges(int current, int max)
	{
		if (_frame is null)
			return;
		max = Mathf.Max(max, 1);
		if (max != _max)
			Rebuild(max);

		bool used = _current >= 0 && current < _current;
		_current = current;
		for (int i = 0; i < _pips.Length; i++)
			_pips[i].Texture = i < current ? _pipLit : _pipDim;
		_frame.Texture = current > 0 ? _frameFull : _frameEmpty;

		if (used)
		{
			_pop?.Kill();
			Scale = Vector2.One * 1.08f;
			_pop = CreateTween();
			_pop.TweenProperty(this, "scale", Vector2.One, 0.18f).SetEase(Tween.EaseType.Out);
		}
	}

	private void Rebuild(int max)
	{
		_max = max;
		int height = Bottom - Top;
		int medallion = MedallionEnd - MedallionStart;
		int track = max * PipCell;
		int cap = CapEnd - CapStart;
		var img = Image.CreateEmpty(medallion + track + cap, height, false, Image.Format.Rgba8);

		img.BlitRect(_sheet, new Rect2I(MedallionStart, Top, medallion, height), Vector2I.Zero);
		for (int x = 0; x < track; x += TrackSliceWidth)
		{
			int w = Mathf.Min(TrackSliceWidth, track - x);
			img.BlitRect(_sheet, new Rect2I(TrackSliceX, Top, w, height), new Vector2I(medallion + x, 0));
		}
		img.BlitRect(_sheet, new Rect2I(CapStart, Top, cap, height), new Vector2I(medallion + track, 0));

		_frameFull = ImageTexture.CreateFromImage(img);
		// Empty flask: only the medallion's red liquid drains (the rest of the bar keeps its colours).
		_frameEmpty = ImageTexture.CreateFromImage(Drained(img, medallion, 0.06f));

		Vector2 size = new Vector2(img.GetWidth(), height) * HudScale;
		CustomMinimumSize = size;
		Size = size;
		PivotOffset = new Vector2(medallion * HudScale / 2f, size.Y / 2f);
		_frame.Size = size;

		foreach (TextureRect old in _pips)
			old.QueueFree();
		_pips = new TextureRect[max];
		for (int i = 0; i < max; i++)
		{
			var pipRect = new TextureRect
			{
				MouseFilter = MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Position = new Vector2(medallion + i * PipCell + FirstPipOffset, PipRect.Position.Y - Top) * HudScale,
				Size = (Vector2)PipRect.Size * HudScale,
			};
			AddChild(pipRect);
			_pips[i] = pipRect;
		}
	}

	// Copy of `source` where saturated red pixels (hue within `hueReach` of red) left of `untilX` turn dark grey (spent pip / empty
	// flask). Glass, cork and gold trim aren't red enough to be touched; the pips get a wider reach so their orange glints dim too.
	private static Image Drained(Image source, int untilX, float hueReach)
	{
		var img = (Image)source.Duplicate();
		for (int y = 0; y < img.GetHeight(); y++)
		{
			for (int x = 0; x < untilX; x++)
			{
				Color c = img.GetPixel(x, y);
				if (c.A < 0.08f || c.S < 0.45f)
					continue;
				bool red = c.H < hueReach || c.H > 1f - hueReach;
				if (!red)
					continue;
				float grey = c.V * 0.32f;
				img.SetPixel(x, y, new Color(grey, grey * 0.95f, grey * 0.92f, c.A));
			}
		}
		return img;
	}
}
