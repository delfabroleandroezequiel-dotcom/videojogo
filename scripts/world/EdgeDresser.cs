using Godot;

namespace Metroidvania.World;

// Hollow Knight-style dressing for a greybox block: instead of tiles that have to interlock, painted
// edge pieces are scattered along the block's outline, each one overlapping the next so no seam or
// grid ever shows, with random mirroring and a little size jitter — plus a fill polygon covering the
// rock interior. Purely visual: the block's own collision is untouched.
// Point Target at a MiniCorridor (its rect is read from Orientation/Length/Thickness) or leave it
// empty and set Area by hand. Seed makes the scatter repeatable; change it to reroll a block.
// Pieces are rebuilt on load (editor and game), never saved into the scene.
[Tool]
public partial class EdgeDresser : Node2D
{
	private NodePath _target = new();
	private Rect2 _area = new(0f, 0f, 256f, 64f);
	private int _seed = 1;
	private bool _initialized;

	[Export]
	public NodePath Target
	{
		get => _target;
		set { _target = value; Rebuild(); }
	}

	// Used when Target is empty, in this node's local space.
	[Export]
	public Rect2 Area
	{
		get => _area;
		set { _area = value; Rebuild(); }
	}

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; Rebuild(); }
	}

	[Export] public bool DressTop = true;
	[Export] public bool DressBottom = true;
	[Export] public bool DressLeft = true;
	[Export] public bool DressRight = true;
	// Grass/rock edges facing up, down, left and right; one is picked at random per piece.
	[Export] public Texture2D[] TopPieces = System.Array.Empty<Texture2D>();
	[Export] public Texture2D[] BottomPieces = System.Array.Empty<Texture2D>();
	[Export] public Texture2D[] LeftPieces = System.Array.Empty<Texture2D>();
	[Export] public Texture2D[] RightPieces = System.Array.Empty<Texture2D>();
	[Export] public float PieceScale = 0.5f;
	// Fraction of each piece covered by the next one — the overlap is what hides the joins.
	[Export(PropertyHint.Range, "0,0.8,0.05")] public float Overlap = 0.35f;
	[Export(PropertyHint.Range, "0,0.4,0.01")] public float ScaleJitter = 0.12f;
	// How far an edge piece reaches outside the block (grass tips poking above a floor), as a
	// fraction of the piece's own depth; the rest sits over the fill.
	[Export(PropertyHint.Range, "0,1,0.05")] public float Outset = 0.45f;
	[Export] public bool Fill = true;
	[Export] public Color FillColor = new(0f, 0f, 0f, 1f);
	[Export] public Texture2D FillTexture;

	public override void _Ready()
	{
		_initialized = true;
		Rebuild();
	}

	private Rect2 ResolveArea()
	{
		if (_target is null || _target.IsEmpty || GetNodeOrNull<Node2D>(_target) is not Node2D block)
			return _area;
		float length = (float)block.Get("Length");
		float thickness = (float)block.Get("Thickness");
		bool lateral = (int)block.Get("Orientation") == (int)CorridorOrientation.Lateral;
		// MiniCorridor: Lateral = top surface at the origin, right over Length, down Thickness;
		// Vertical = down over Length, straddling the origin by Thickness.
		Rect2 local = lateral ? new Rect2(0f, 0f, length, thickness) : new Rect2(-thickness / 2f, 0f, thickness, length);
		Vector2 topLeft = ToLocal(block.ToGlobal(local.Position));
		return new Rect2(topLeft, local.Size);
	}

	private void Rebuild()
	{
		if (!_initialized)
			return;
		foreach (Node child in GetChildren())
		{
			if (child.HasMeta("edge_dresser"))
			{
				RemoveChild(child);
				child.QueueFree();
			}
		}

		Rect2 r = ResolveArea();
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };

		if (Fill)
		{
			var fill = new Polygon2D
			{
				Polygon = new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) },
				Color = FillColor,
				Texture = FillTexture,
				TextureRepeat = TextureRepeatEnum.Enabled,
				ZIndex = -1,
			};
			Tag(fill);
		}

		if (DressTop)
			Scatter(TopPieces, rng, r.Position, new Vector2(r.End.X, r.Position.Y), Vector2.Up);
		if (DressBottom)
			Scatter(BottomPieces, rng, new Vector2(r.Position.X, r.End.Y), r.End, Vector2.Down);
		if (DressLeft)
			Scatter(LeftPieces, rng, r.Position, new Vector2(r.Position.X, r.End.Y), Vector2.Left);
		if (DressRight)
			Scatter(RightPieces, rng, new Vector2(r.End.X, r.Position.Y), r.End, Vector2.Right);
	}

	// Lays pieces along one edge (from -> to), each one pushed Outset of its depth outward along
	// `outward`, stepping by its length minus the overlap.
	private void Scatter(Texture2D[] pieces, RandomNumberGenerator rng, Vector2 from, Vector2 to, Vector2 outward)
	{
		if (pieces is null || pieces.Length == 0)
			return;
		bool horizontal = outward.Y != 0f;
		Vector2 along = (to - from).Normalized();
		float edgeLength = from.DistanceTo(to);
		float t = 0f;
		while (t < edgeLength)
		{
			Texture2D tex = pieces[rng.RandiRange(0, pieces.Length - 1)];
			if (tex is null)
				return;
			float s = PieceScale * (1f + rng.RandfRange(-ScaleJitter, ScaleJitter));
			Vector2 size = tex.GetSize() * s;
			float pieceLength = horizontal ? size.X : size.Y;
			float depth = horizontal ? size.Y : size.X;
			// Centered on the edge line, then pushed out so Outset of the depth pokes past it.
			Vector2 onEdge = from + along * Mathf.Min(t + pieceLength / 2f, edgeLength);
			var sprite = new Sprite2D
			{
				Texture = tex,
				Scale = Vector2.One * s,
				Position = onEdge + outward * depth * (Outset - 0.5f),
				FlipH = horizontal && rng.Randf() < 0.5f,
				FlipV = !horizontal && rng.Randf() < 0.5f,
				ZIndex = 1,
			};
			Tag(sprite);
			t += pieceLength * (1f - Overlap);
		}
	}

	private void Tag(Node2D node)
	{
		node.SetMeta("edge_dresser", true);
		AddChild(node);
	}
}
