using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum CorridorOrientation
{
	Lateral,
	Vertical,
}

public enum CorridorPreset
{
	Small,
	Medium,
	Large,
	Custom,
}

// Reusable greybox building block: a rectangle instanced and tuned per level, the same way
// CaveRockPlatform is reused. Orientation picks the coordinate frame (Lateral = floor-level
// origin, extends sideways over Length with a CrossSize-tall gap; Vertical = top-opening origin,
// extends downward over Length with a CrossSize-wide gap) and gives each of the 4 sides
// (Floor/Ceiling/LeftWall/RightWall) a sensible on/off default — but every side stays
// independently toggleable afterward, and CrossSize is free per instance, so the same piece
// covers narrow squeezes, wide stretches, end-caps (seal one open end), and — Length and
// CrossSize both stretched, all 4 sides on — a boss arena, without a separate scene per shape.
// A 90-degree turn needs no special piece either: a Lateral piece has no end walls by default,
// so placing a Vertical piece at its Exit already connects cleanly.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class CorridorBlock : Node2D
{
	private CorridorOrientation _orientation = CorridorOrientation.Lateral;

	[Export]
	public CorridorOrientation Orientation
	{
		get => _orientation;
		// Deliberately does NOT default the 4 Has* flags for the new orientation (it used to) —
		// that side effect fought Godot's scene-diff serialization: a Vertical piece with
		// HasLeftWall explicitly set to false (off) has the same value as Lateral's own default,
		// so the override looked like "no change" and silently failed to save, and the forced
		// default from this setter won back on every reload. Has* are independent per instance;
		// set all 4 explicitly when picking a new Orientation.
		set { _orientation = value; Rebuild(); }
	}

	private CorridorPreset _preset = CorridorPreset.Medium;

	[Export]
	public CorridorPreset Preset
	{
		get => _preset;
		set
		{
			_preset = value;
			if (_preset != CorridorPreset.Custom)
				_length = GameConfig.CorridorLengthPresets[(int)_preset];
			Rebuild();
		}
	}

	private float _length = GameConfig.CorridorLengthPresets[1];

	[Export]
	public float Length
	{
		get => _length;
		set
		{
			_length = value;
			_preset = CorridorPreset.Custom;
			Rebuild();
		}
	}

	private CorridorPreset _crossPreset = CorridorPreset.Medium;

	[Export]
	public CorridorPreset CrossPreset
	{
		get => _crossPreset;
		set
		{
			_crossPreset = value;
			if (_crossPreset != CorridorPreset.Custom)
				_crossSize = GameConfig.CorridorCrossSizePresets[(int)_crossPreset];
			Rebuild();
		}
	}

	private float _crossSize = GameConfig.CorridorCrossSizePresets[1];

	// Height when Lateral, width when Vertical — the "distance between the two solid sides"
	// (floor/ceiling gap, or the gap between the two walls). Free per instance so the same piece
	// covers a super narrow squeeze, a super wide stretch, or (with Length also stretched and all
	// 4 walls on) a boss arena.
	[Export]
	public float CrossSize
	{
		get => _crossSize;
		set
		{
			_crossSize = value;
			_crossPreset = CorridorPreset.Custom;
			Rebuild();
		}
	}

	private bool _hasFloor = true;

	[Export]
	public bool HasFloor
	{
		get => _hasFloor;
		set { _hasFloor = value; Rebuild(); }
	}

	private bool _hasCeiling = true;

	[Export]
	public bool HasCeiling
	{
		get => _hasCeiling;
		set { _hasCeiling = value; Rebuild(); }
	}

	private bool _hasLeftWall;

	[Export]
	public bool HasLeftWall
	{
		get => _hasLeftWall;
		set { _hasLeftWall = value; Rebuild(); }
	}

	private bool _hasRightWall;

	[Export]
	public bool HasRightWall
	{
		get => _hasRightWall;
		set { _hasRightWall = value; Rebuild(); }
	}

	private float _wallThickness = 32f;

	[Export]
	public float WallThickness
	{
		get => _wallThickness;
		set { _wallThickness = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	// Ceiling gets its own color instead of sharing FillColor with Floor/LeftWall/RightWall —
	// the "sandwich"/map-edge backing behind a ceiling is painted a specific near-black
	// (not the same flat black as everything else) and the other 3 sides had no need to diverge
	// from FillColor yet. Defaults to the same value as FillColor so existing pieces don't change
	// until this is explicitly set.
	[Export] public Color CeilingFillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	// Extra depth added to one side's Fill polygon only, growing outward past WallThickness —
	// the collision box for that side is untouched. For solid-black backing behind a "sandwich"
	// gap between two stacked CorridorBlock pieces, or past the outer edge of the whole map,
	// where WallThickness itself would need to stay small everywhere else on the same piece.
	private float _floorFillExtend;

	[Export]
	public float FloorFillExtend
	{
		get => _floorFillExtend;
		set { _floorFillExtend = value; Rebuild(); }
	}

	private float _ceilingFillExtend;

	[Export]
	public float CeilingFillExtend
	{
		get => _ceilingFillExtend;
		set { _ceilingFillExtend = value; Rebuild(); }
	}

	private float _leftWallFillExtend;

	[Export]
	public float LeftWallFillExtend
	{
		get => _leftWallFillExtend;
		set { _leftWallFillExtend = value; Rebuild(); }
	}

	private float _rightWallFillExtend;

	[Export]
	public float RightWallFillExtend
	{
		get => _rightWallFillExtend;
		set { _rightWallFillExtend = value; Rebuild(); }
	}

	// Turn off once a side's tiles are already painted — the Fill polygon is only a greybox
	// stand-in, and left on it shows through as a translucent wash over the real art. One toggle
	// per side (not a single shared one) because a piece is often only tuned/tiled on one face —
	// e.g. a ceiling backing a "sandwich" gap — while the other 3 sides have nothing to show and
	// need to stay hidden independently. Collision is never affected by any of these.
	private bool _floorShowFill = true;

	[Export]
	public bool FloorShowFill
	{
		get => _floorShowFill;
		set { _floorShowFill = value; Rebuild(); }
	}

	private bool _ceilingShowFill = true;

	[Export]
	public bool CeilingShowFill
	{
		get => _ceilingShowFill;
		set { _ceilingShowFill = value; Rebuild(); }
	}

	private bool _leftWallShowFill = true;

	[Export]
	public bool LeftWallShowFill
	{
		get => _leftWallShowFill;
		set { _leftWallShowFill = value; Rebuild(); }
	}

	private bool _rightWallShowFill = true;

	[Export]
	public bool RightWallShowFill
	{
		get => _rightWallShowFill;
		set { _rightWallShowFill = value; Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		bool lateral = _orientation == CorridorOrientation.Lateral;

		if (lateral)
		{
			ApplyPart(GetNodeOrNull<StaticBody2D>("Floor"), _hasFloor,
				new Vector2(_length, _wallThickness),
				new Vector2(_length / 2f, _wallThickness / 2f),
				Vector2.Down, _floorFillExtend, FillColor, _floorShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("Ceiling"), _hasCeiling,
				new Vector2(_length, _wallThickness),
				new Vector2(_length / 2f, -_crossSize - _wallThickness / 2f),
				Vector2.Up, _ceilingFillExtend, CeilingFillColor, _ceilingShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("LeftWall"), _hasLeftWall,
				new Vector2(_wallThickness, _crossSize),
				new Vector2(-_wallThickness / 2f, -_crossSize / 2f),
				Vector2.Left, _leftWallFillExtend, FillColor, _leftWallShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("RightWall"), _hasRightWall,
				new Vector2(_wallThickness, _crossSize),
				new Vector2(_length + _wallThickness / 2f, -_crossSize / 2f),
				Vector2.Right, _rightWallFillExtend, FillColor, _rightWallShowFill);
		}
		else
		{
			ApplyPart(GetNodeOrNull<StaticBody2D>("LeftWall"), _hasLeftWall,
				new Vector2(_wallThickness, _length),
				new Vector2(-_wallThickness / 2f, _length / 2f),
				Vector2.Left, _leftWallFillExtend, FillColor, _leftWallShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("RightWall"), _hasRightWall,
				new Vector2(_wallThickness, _length),
				new Vector2(_crossSize + _wallThickness / 2f, _length / 2f),
				Vector2.Right, _rightWallFillExtend, FillColor, _rightWallShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("Floor"), _hasFloor,
				new Vector2(_crossSize, _wallThickness),
				new Vector2(_crossSize / 2f, _length + _wallThickness / 2f),
				Vector2.Down, _floorFillExtend, FillColor, _floorShowFill);

			ApplyPart(GetNodeOrNull<StaticBody2D>("Ceiling"), _hasCeiling,
				new Vector2(_crossSize, _wallThickness),
				new Vector2(_crossSize / 2f, -_wallThickness / 2f),
				Vector2.Up, _ceilingFillExtend, CeilingFillColor, _ceilingShowFill);
		}

		// Exit sits at the far end (floor level for Lateral, bottom opening for Vertical) so the
		// next piece's origin can just be dropped on Exit's global position.
		if (GetNodeOrNull<Marker2D>("Exit") is Marker2D exit)
			exit.Position = lateral ? new Vector2(_length, 0f) : new Vector2(0f, _length);
	}

	private void ApplyPart(StaticBody2D body, bool active, Vector2 size, Vector2 center, Vector2 outwardDir, float fillExtend, Color color, bool showFill)
	{
		if (body is null)
			return;

		body.Visible = active;
		body.Position = center;

		if (body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
		{
			collision.Disabled = !active;
			// Always assign a fresh shape instead of mutating collision.Shape in place: the
			// RectangleShape2D from the .tscn is a single shared resource, so every instance of
			// CorridorBlock would otherwise resize the same object and collisions would jump
			// between pieces.
			collision.Shape = new RectangleShape2D { Size = size };
		}

		if (body.GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
		{
			fill.Visible = showFill;
			fill.Color = color;
			// fillExtend grows the polygon past WallThickness in outwardDir only, relative to
			// body.Position (= center) — the collision shape above never sees this.
			Vector2 fillSize = size + outwardDir.Abs() * fillExtend;
			Vector2 fillCenterOffset = outwardDir * (fillExtend / 2f);
			Vector2 half = fillSize / 2f;
			fill.Polygon = new[]
			{
				fillCenterOffset + new Vector2(-half.X, -half.Y),
				fillCenterOffset + new Vector2(half.X, -half.Y),
				fillCenterOffset + new Vector2(half.X, half.Y),
				fillCenterOffset + new Vector2(-half.X, half.Y),
			};
		}
	}
}
