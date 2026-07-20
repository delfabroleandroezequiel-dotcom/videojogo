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
		set
		{
			_orientation = value;
			bool lateral = _orientation == CorridorOrientation.Lateral;
			_hasFloor = lateral;
			_hasCeiling = lateral;
			_hasLeftWall = !lateral;
			_hasRightWall = !lateral;
			Rebuild();
		}
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
				new Vector2(_length / 2f, _wallThickness / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("Ceiling"), _hasCeiling,
				new Vector2(_length, _wallThickness),
				new Vector2(_length / 2f, -_crossSize - _wallThickness / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("LeftWall"), _hasLeftWall,
				new Vector2(_wallThickness, _crossSize),
				new Vector2(-_wallThickness / 2f, -_crossSize / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("RightWall"), _hasRightWall,
				new Vector2(_wallThickness, _crossSize),
				new Vector2(_length + _wallThickness / 2f, -_crossSize / 2f));
		}
		else
		{
			ApplyPart(GetNodeOrNull<StaticBody2D>("LeftWall"), _hasLeftWall,
				new Vector2(_wallThickness, _length),
				new Vector2(-_wallThickness / 2f, _length / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("RightWall"), _hasRightWall,
				new Vector2(_wallThickness, _length),
				new Vector2(_crossSize + _wallThickness / 2f, _length / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("Floor"), _hasFloor,
				new Vector2(_crossSize, _wallThickness),
				new Vector2(_crossSize / 2f, _length + _wallThickness / 2f));

			ApplyPart(GetNodeOrNull<StaticBody2D>("Ceiling"), _hasCeiling,
				new Vector2(_crossSize, _wallThickness),
				new Vector2(_crossSize / 2f, -_wallThickness / 2f));
		}

		// Exit sits at the far end (floor level for Lateral, bottom opening for Vertical) so the
		// next piece's origin can just be dropped on Exit's global position.
		if (GetNodeOrNull<Marker2D>("Exit") is Marker2D exit)
			exit.Position = lateral ? new Vector2(_length, 0f) : new Vector2(0f, _length);
	}

	private void ApplyPart(StaticBody2D body, bool active, Vector2 size, Vector2 center)
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
			fill.Color = FillColor;
			Vector2 half = size / 2f;
			fill.Polygon = new[]
			{
				new Vector2(-half.X, -half.Y),
				new Vector2(half.X, -half.Y),
				new Vector2(half.X, half.Y),
				new Vector2(-half.X, half.Y),
			};
		}
	}
}
