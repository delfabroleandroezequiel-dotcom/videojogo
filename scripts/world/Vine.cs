using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// A hanging vine strand from vines.png (the 19 vertical strands on the sheet's top row), stretched
// to any Length like Rope. Climbable like a ladder (Climbable), and it stays still unless Sway is
// turned on (then it bends gently from the anchor, the tip moving most). The strand's body tiles
// along a Line2D (so any length works without stretching the art) and its tapered tip is drawn at
// the end. Hangs down from this node's position.
// [Tool] so style/length/sway preview live in the editor.
[Tool]
public partial class Vine : Node2D
{
	private const string SheetPath = "res://assets/sprites/deco/vines.png";

	// Per strand, measured from vines.png's alpha (not guessed): x, width, and the rows where the
	// strand starts, where its tapered tip starts, and where it ends.
	private static readonly (int X, int W, int Top, int TipStart, int Bottom)[] Strands =
	{
		(39, 21, 10, 445, 457), (103, 20, 10, 440, 452), (168, 29, 10, 444, 456), (244, 40, 10, 445, 457),
		(326, 47, 10, 442, 454), (416, 53, 10, 437, 453), (510, 51, 10, 442, 454), (602, 49, 10, 439, 452),
		(695, 48, 10, 448, 460), (787, 46, 10, 450, 465), (874, 44, 10, 438, 450), (957, 45, 10, 448, 460),
		(1042, 62, 10, 452, 464), (1140, 45, 10, 428, 451), (1230, 63, 10, 445, 462), (1320, 62, 10, 447, 464),
		(1419, 48, 10, 438, 453), (1496, 64, 10, 398, 449), (1593, 61, 10, 387, 450),
	};

	// Cropped body (rotated so it tiles along the line) + tip textures, shared by every vine.
	private static readonly Dictionary<int, (Texture2D Body, Texture2D Tip)> Cache = new();

	private int _style;

	[Export(PropertyHint.Range, "0,18,1")]
	public int Style
	{
		get => _style;
		set { _style = Mathf.Clamp(value, 0, Strands.Length - 1); Rebuild(); }
	}

	private float _length = 160f;

	// Total hanging length in world pixels (tip included).
	[Export(PropertyHint.Range, "10,2000,1")]
	public float Length
	{
		get => _length;
		set { _length = Mathf.Max(10f, value); Rebuild(); }
	}

	private float _vineScale = 0.35f;

	// Art scale — the strand's width and how often its leaf pattern repeats.
	[Export(PropertyHint.Range, "0.05,2,0.01")]
	public float VineScale
	{
		get => _vineScale;
		set { _vineScale = Mathf.Max(0.05f, value); Rebuild(); }
	}

	private Color _tint = Colors.White;

	[Export]
	public Color Tint
	{
		get => _tint;
		set { _tint = value; Modulate = value; }
	}

	// Off by default: the vine hangs still.
	[Export] public bool Sway;
	// Horizontal travel of the tip, in world pixels, each side.
	[Export] public float SwayAmplitude = 6f;
	// Full swings per second.
	[Export] public float SwayFrequency = 0.35f;
	[Export] public float SwayPhase;

	// Climbable like a ladder (up to grab, up/down to climb, jump to let go): an invisible Ladder
	// zone of the same length is built along the strand, so the player's existing ladder climbing
	// handles it. ClimbWidth is how close (horizontally) the player has to be to grab it.
	[Export] public bool Climbable = true;
	[Export] public float ClimbWidth = 20f;

	private const int Segments = 12;

	private Ladder _climbZone;
	private Line2D _body;
	private Sprite2D _tip;
	private float _bodyLength;
	private float _time;

	public override void _Ready()
	{
		Modulate = _tint;
		Rebuild();
	}

	private static (Texture2D Body, Texture2D Tip) TexturesFor(int style)
	{
		if (Cache.TryGetValue(style, out var cached))
			return cached;

		Image sheet = GD.Load<Texture2D>(SheetPath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		var s = Strands[style];
		Image body = sheet.GetRegion(new Rect2I(s.X, s.Top, s.W, s.TipStart - s.Top));
		// Line2D tiles its texture along the line (U) — turn the strand sideways, top at the left, so
		// the anchor end of the line starts at the top of the strand.
		body.Rotate90(ClockDirection.Counterclockwise);
		Image tip = sheet.GetRegion(new Rect2I(s.X, s.TipStart, s.W, s.Bottom - s.TipStart + 1));
		var textures = (ImageTexture.CreateFromImage(body) as Texture2D, ImageTexture.CreateFromImage(tip) as Texture2D);
		Cache[style] = textures;
		return textures;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		var (bodyTexture, tipTexture) = TexturesFor(_style);
		var strand = Strands[_style];

		if (_body is null)
		{
			_body = new Line2D
			{
				TextureMode = Line2D.LineTextureMode.Tile,
				TextureRepeat = TextureRepeatEnum.Enabled,
				JointMode = Line2D.LineJointMode.Round,
				DefaultColor = Colors.White,
			};
			AddChild(_body);
			_tip = new Sprite2D { Centered = false };
			AddChild(_tip);
		}

		float tipHeight = tipTexture.GetHeight() * _vineScale;
		_bodyLength = Mathf.Max(0f, _length - tipHeight);
		_body.Texture = bodyTexture;
		_body.Width = strand.W * _vineScale;
		_tip.Texture = tipTexture;
		_tip.Scale = Vector2.One * _vineScale;
		_tip.Offset = new Vector2(-strand.W / 2f, 0f);
		UpdateShape(Sway ? SwayOffset() : 0f);
		if (!Engine.IsEditorHint())
			RebuildClimbZone();
	}

	private void RebuildClimbZone()
	{
		_climbZone?.QueueFree();
		_climbZone = null;
		if (!Climbable)
			return;

		// Ladder sizes its own shape from Length/Width in _Ready and has no texture → no visual.
		_climbZone = new Ladder
		{
			Name = "ClimbZone",
			Length = _length,
			Width = ClimbWidth,
			CollisionLayer = 0,
			CollisionMask = 2,
			Position = new Vector2(0f, _length / 2f),
		};
		_climbZone.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new RectangleShape2D() });
		AddChild(_climbZone);
	}

	private float SwayOffset() => SwayAmplitude * Mathf.Sin(_time * SwayFrequency * Mathf.Tau + SwayPhase);

	// Bends the strand: horizontal offset grows with the square of the distance from the anchor, so
	// the top stays put and the tip moves the most.
	private void UpdateShape(float tipOffset)
	{
		if (_body is null)
			return;

		var points = new Vector2[Segments + 1];
		float total = Mathf.Max(1f, _length);
		for (int i = 0; i <= Segments; i++)
		{
			float y = _bodyLength * i / Segments;
			float t = y / total;
			points[i] = new Vector2(tipOffset * t * t, y);
		}
		_body.Points = points;

		Vector2 end = points[Segments];
		Vector2 before = Segments > 0 ? points[Segments - 1] : Vector2.Zero;
		_tip.Position = end;
		Vector2 direction = (end - before).LengthSquared() > 0.0001f ? (end - before).Normalized() : Vector2.Down;
		_tip.Rotation = Vector2.Down.AngleTo(direction);
	}

	public override void _Process(double delta)
	{
		if (!Sway)
			return;
		_time += (float)delta;
		UpdateShape(SwayOffset());
	}
}
