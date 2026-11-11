using Godot;

namespace Metroidvania.World;

// Static wooden mine scaffold (AndamioMina.tscn): a one-way plank deck on posts, drawn from the
// PixelLab sheet Andamio.png at 1:1 so any width/height stays crisp. The node sits at the deck's
// top-centre. The deck tiles across Width, posts go at both ends plus one every MaxBay px, each
// post's shaft repeats down to LegHeight, and each bay gets the sheet's diagonal brace stretched to
// fit. Not the burning/collapsing WoodenScaffold trap (Andamio.tscn) — this one just stands.
[Tool]
public partial class MineScaffold : Node2D
{
	private float _width = 240f;
	private float _legHeight = 60f;
	private float _maxBay = 150f;
	private bool _braces = true;
	private Texture2D _texture = GD.Load<Texture2D>("res://assets/sprites/deco/pixellab/mina/Andamio.png");

	[Export] public float Width { get => _width; set { _width = Mathf.Max(60f, value); QueueRedraw(); } }
	// Deck top to the posts' feet.
	[Export] public float LegHeight { get => _legHeight; set { _legHeight = Mathf.Max(DeckHeight + 8f, value); QueueRedraw(); } }
	// Widest gap between two posts before another post is added in between.
	[Export] public float MaxBay { get => _maxBay; set { _maxBay = Mathf.Max(60f, value); QueueRedraw(); } }
	[Export] public bool Braces { get => _braces; set { _braces = value; QueueRedraw(); } }
	[Export] public Texture2D Texture { get => _texture; set { _texture = value; QueueRedraw(); } }
	[Export(PropertyHint.Layers2DPhysics)] public uint CollisionLayer = 49;

	// Regions of Andamio.png (192x72), measured from the sheet.
	private const float DeckHeight = 12f;
	private static readonly Rect2 DeckTile = new(50, 15, 90, 12);
	private static readonly Rect2 LeftCap = new(25, 11, 19, 17);
	private static readonly Rect2 RightCap = new(144, 11, 22, 17);
	private static readonly Rect2 MidCap = new(31, 11, 12, 17);
	private static readonly Rect2 PostShaft = new(31, 30, 12, 15);
	private static readonly Rect2 PostFoot = new(146, 50, 12, 16);
	private static readonly Rect2 Brace = new(43, 28, 103, 30); // starts below the deck's underside row
	private const float CapTop = -4f;
	private const float PostWidth = 12f;
	// Where the end posts sit inside the caps (sheet x 31 - 25 and 166 - 158).
	private const float LeftPostInset = 6f;
	private const float RightPostInset = 8f;

	// Posts reach down to whatever floor is under each of them at runtime (one ray per post on
	// FloorMask), so a scaffold never floats; LegHeight is then only the editor preview and the
	// fallback when nothing is found within MaxLegHeight. Off = every post is exactly LegHeight.
	[Export] public bool ToFloor = true;
	[Export] public float MaxLegHeight = 2000f;
	[Export(PropertyHint.Layers2DPhysics)] public uint FloorMask = 1;
	// Vertical distance between stacked braces on tall legs (they zigzag down the bay).
	[Export] public float BraceSpacing = 64f;

	private float[] _legs;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		// Own shape per instance (a shared sub-resource would resize every scaffold).
		var body = new StaticBody2D { Name = "Plataforma", CollisionLayer = CollisionLayer, CollisionMask = 0 };
		body.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width, DeckHeight) },
			Position = new Vector2(0f, DeckHeight / 2f),
			OneWayCollision = true,
		});
		AddChild(body);
		SetPhysicsProcess(ToFloor);
	}

	// First physics frame: the space has every static body registered by now.
	public override void _PhysicsProcess(double delta)
	{
		SetPhysicsProcess(false);
		if (Engine.IsEditorHint())
			return;
		float[] xs = PostXs(out _, out _);
		_legs = new float[xs.Length];
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		for (int i = 0; i < xs.Length; i++)
		{
			Vector2 from = ToGlobal(new Vector2(xs[i] + PostWidth / 2f, DeckHeight + 2f));
			var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0f, MaxLegHeight), FloorMask);
			Godot.Collections.Dictionary hit = space.IntersectRay(query);
			_legs[i] = hit.Count > 0
				? Mathf.Max(DeckHeight + 8f, ToLocal((Vector2)hit["position"]).Y)
				: _legHeight;
		}
		QueueRedraw();
	}

	private float[] PostXs(out int bays, out float bayStep)
	{
		float firstPost = -_width / 2f + LeftPostInset;
		float lastPost = _width / 2f - RightPostInset - PostWidth;
		bays = Mathf.Max(1, Mathf.CeilToInt((lastPost - firstPost) / _maxBay));
		bayStep = (lastPost - firstPost) / bays;
		var xs = new float[bays + 1];
		for (int i = 0; i <= bays; i++)
			xs[i] = Mathf.Round(firstPost + bayStep * i);
		return xs;
	}

	private float Leg(int i) => _legs is not null && i < _legs.Length ? _legs[i] : _legHeight;

	public override void _Draw()
	{
		if (_texture is null)
			return;
		float left = -_width / 2f, right = _width / 2f;
		float[] xs = PostXs(out int bays, out _);

		for (int i = 0; i <= bays; i++)
		{
			DrawPost(xs[i], Leg(i));
			if (_braces && i < bays)
				DrawBraces(xs[i] + PostWidth, xs[i + 1], Mathf.Min(Leg(i), Leg(i + 1)));
		}

		for (float x = left; x < right; x += DeckTile.Size.X)
		{
			float w = Mathf.Min(DeckTile.Size.X, right - x);
			DrawTextureRectRegion(_texture, new Rect2(x, 0f, w, DeckHeight), new Rect2(DeckTile.Position, new Vector2(w, DeckTile.Size.Y)));
		}

		for (int i = 1; i < bays; i++)
			DrawTextureRectRegion(_texture, new Rect2(xs[i], CapTop, MidCap.Size), MidCap);
		DrawTextureRectRegion(_texture, new Rect2(left, CapTop, LeftCap.Size), LeftCap);
		DrawTextureRectRegion(_texture, new Rect2(right - RightCap.Size.X, CapTop, RightCap.Size), RightCap);
	}

	// The sheet's diagonal brace stretched across one bay, repeated every BraceSpacing down the
	// legs and mirrored every other row so tall scaffolds read as a zigzag of cross bracing.
	private void DrawBraces(float x1, float x2, float leg)
	{
		float bottom = leg - PostFoot.Size.Y;
		float spacing = Mathf.Max(Brace.Size.Y + 8f, BraceSpacing);
		int row = 0;
		for (float y = DeckHeight - 1f; y < bottom - 8f; y += spacing, row++)
		{
			float h = Mathf.Min(Brace.Size.Y, bottom - y);
			if (h < 12f)
				break;
			if (row % 2 == 1)
				DrawSetTransform(new Vector2(x1 + x2, 0f), 0f, new Vector2(-1f, 1f));
			DrawTextureRectRegion(_texture, new Rect2(x1, y, x2 - x1, h), Brace);
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		}
	}

	private void DrawPost(float x, float leg)
	{
		float footTop = leg - PostFoot.Size.Y;
		for (float y = DeckHeight; y < footTop; y += PostShaft.Size.Y)
		{
			float h = Mathf.Min(PostShaft.Size.Y, footTop - y);
			DrawTextureRectRegion(_texture, new Rect2(x, y, PostWidth, h), new Rect2(PostShaft.Position, new Vector2(PostShaft.Size.X, h)));
		}
		DrawTextureRectRegion(_texture, new Rect2(x, footTop, PostFoot.Size), PostFoot);
	}
}
