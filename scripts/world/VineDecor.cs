using Godot;

namespace Metroidvania.World;

// The fixed-shape pieces of vines.png that aren't strands (so they don't stretch): hanging garlands,
// ledge fringes/curtains, corner pieces, a branching vine and loose leaves. Anchored at the top
// center, so it hangs from this node's position like Vine. Still unless Sway is on — then it shears
// gently from the top (the bottom edge moves, the top stays attached).
// [Tool] so style/scale/flip/sway preview live in the editor.
[Tool]
public partial class VineDecor : Node2D
{
	private const string SheetPath = "res://assets/sprites/deco/vines.png";

	// Measured from vines.png's alpha (connected pieces), in sheet order.
	private static readonly Rect2[] Pieces =
	{
		// 0-3: garlands (plain ×2, flowered ×2)
		new(24, 479, 317, 89), new(28, 558, 322, 104), new(381, 479, 310, 89), new(381, 557, 322, 103),
		// 4: branching vine, 5: corner garland with a hanging strand
		new(733, 479, 133, 182), new(908, 480, 271, 181),
		// 6-7: curtains (plain, flowered)
		new(1214, 480, 209, 187), new(1455, 480, 290, 188),
		// 8-13: ledge fringes
		new(16, 683, 170, 171), new(207, 683, 272, 186), new(498, 682, 257, 177), new(773, 681, 209, 188),
		new(998, 683, 97, 114), new(1123, 683, 203, 154),
		// 14: flowered hanging strand, 15-19: corner pieces
		new(1345, 683, 99, 186), new(1478, 684, 96, 63), new(1478, 768, 111, 102), new(1606, 683, 60, 62),
		new(1613, 768, 73, 62),
		// 20: small fringe, 21-26: loose leaves
		new(1693, 683, 53, 80),
		new(1691, 12, 53, 46), new(1691, 77, 53, 49), new(1691, 142, 53, 101), new(1698, 250, 46, 70),
		new(1696, 339, 48, 35), new(1691, 402, 53, 47),
	};

	private int _style;

	[Export(PropertyHint.Range, "0,26,1")]
	public int Style
	{
		get => _style;
		set { _style = Mathf.Clamp(value, 0, Pieces.Length - 1); Rebuild(); }
	}

	private float _vineScale = 0.35f;

	[Export(PropertyHint.Range, "0.05,2,0.01")]
	public float VineScale
	{
		get => _vineScale;
		set { _vineScale = Mathf.Max(0.05f, value); Rebuild(); }
	}

	private bool _flipH;

	[Export]
	public bool FlipH
	{
		get => _flipH;
		set { _flipH = value; Rebuild(); }
	}

	private Color _tint = Colors.White;

	[Export]
	public Color Tint
	{
		get => _tint;
		set { _tint = value; Modulate = value; }
	}

	// Off by default: it hangs still.
	[Export] public bool Sway;
	// Max shear angle in degrees (how far the bottom edge leans), each side.
	[Export] public float SwayAngleDegrees = 3f;
	[Export] public float SwayFrequency = 0.35f;
	[Export] public float SwayPhase;

	private Sprite2D _sprite;
	private float _time;

	public override void _Ready()
	{
		Modulate = _tint;
		Rebuild();
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		if (_sprite is null)
		{
			_sprite = new Sprite2D { Centered = false };
			AddChild(_sprite);
		}

		Rect2 region = Pieces[_style];
		_sprite.Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>(SheetPath), Region = region };
		_sprite.Offset = new Vector2(-region.Size.X / 2f, 0f);
		_sprite.FlipH = _flipH;
		_sprite.Scale = Vector2.One * _vineScale;
		if (!Sway)
			_sprite.Skew = 0f;
	}

	public override void _Process(double delta)
	{
		if (!Sway || _sprite is null)
			return;
		_time += (float)delta;
		// Skew shears x by y around the sprite's origin (its top edge), so the top stays anchored.
		_sprite.Skew = Mathf.DegToRad(SwayAngleDegrees) * Mathf.Sin(_time * SwayFrequency * Mathf.Tau + SwayPhase);
	}
}
