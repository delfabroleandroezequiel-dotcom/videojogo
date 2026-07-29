using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum JumpGapMode
{
	SingleJump,
	DoubleJump,
	Custom,
}

// Reusable greybox row: set Count and get that many MiniCorridor pieces chained along Orientation,
// each one separated from the next by a jump-gap distance instead of sitting side by side — same
// "just set the count" idea as SpikeAccordion, but the gap itself is picked to match what the
// player's jump can actually cross (GameConfig.JumpGap*), not a hand-guessed pixel value (see
// CorridorBlock's Physics-over-blind-paths precedent). GapMode picks which: SingleJump/DoubleJump
// pull from GameConfig, Custom takes CustomGapDistance instead — useful once playtesting says the
// computed gap needs hand-tuning for a specific spot.
// Rebuild wipes and regenerates every MiniCorridor child from scratch on any change, so detach a
// piece into its own MiniCorridor.tscn instance if one needs to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class MiniCorridorAccordion : Node2D
{
	private const string MiniCorridorScenePath = "res://scenes/world/Rehusables/MiniCorridor.tscn";

	private CorridorOrientation _orientation = CorridorOrientation.Lateral;

	[Export]
	public CorridorOrientation Orientation
	{
		get => _orientation;
		set { _orientation = value; Rebuild(); }
	}

	private int _count = 4;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _segmentLength = 96f;

	[Export]
	public float SegmentLength
	{
		get => _segmentLength;
		set { _segmentLength = value; Rebuild(); }
	}

	private float _thickness = 32f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = value; Rebuild(); }
	}

	private bool _climbable;

	// Passed through to every MiniCorridor piece — see MiniCorridor.Climbable.
	[Export]
	public bool Climbable
	{
		get => _climbable;
		set { _climbable = value; Rebuild(); }
	}

	private bool _oneWay;

	// Passed through to every MiniCorridor piece — see MiniCorridor.OneWay.
	[Export]
	public bool OneWay
	{
		get => _oneWay;
		set { _oneWay = value; Rebuild(); }
	}

	private JumpGapMode _gapMode = JumpGapMode.SingleJump;

	[Export]
	public JumpGapMode GapMode
	{
		get => _gapMode;
		set { _gapMode = value; Rebuild(); }
	}

	private float _customGapDistance = 96f;

	// Only used when GapMode is Custom — the computed SingleJump/DoubleJump gaps ignore this.
	[Export]
	public float CustomGapDistance
	{
		get => _customGapDistance;
		set { _customGapDistance = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	public override void _Ready() => Rebuild();

	private float ResolveGap()
	{
		bool lateral = _orientation == CorridorOrientation.Lateral;
		return _gapMode switch
		{
			JumpGapMode.SingleJump => lateral ? GameConfig.JumpGapHorizontalSingle : GameConfig.JumpGapVerticalSingle,
			JumpGapMode.DoubleJump => lateral ? GameConfig.JumpGapHorizontalDouble : GameConfig.JumpGapVerticalDouble,
			_ => _customGapDistance,
		};
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		bool lateral = _orientation == CorridorOrientation.Lateral;
		float gap = ResolveGap();
		PackedScene miniCorridorScene = GD.Load<PackedScene>(MiniCorridorScenePath);

		Vector2 cursor = Vector2.Zero;
		for (int i = 0; i < _count; i++)
		{
			MiniCorridor piece = miniCorridorScene.Instantiate<MiniCorridor>();
			piece.Name = $"MiniCorridor{i + 1}";
			piece.Orientation = _orientation;
			piece.Length = _segmentLength;
			piece.Thickness = _thickness;
			piece.Climbable = _climbable;
			piece.OneWay = _oneWay;
			piece.FillColor = FillColor;
			piece.Position = cursor;
			AddChild(piece);
			piece.Owner = this;

			float advance = _segmentLength + gap;
			cursor += lateral ? new Vector2(advance, 0f) : new Vector2(0f, advance);
		}

		// Exit sits right after the last piece (before its trailing gap), so the next block's
		// origin can just be dropped on Exit's global position. Generated fresh each Rebuild
		// (like the MiniCorridor pieces above) since the free-all loop just wiped any prior one.
		Vector2 lastEnd = lateral ? new Vector2((_count - 1) * (_segmentLength + gap) + _segmentLength, 0f)
			: new Vector2(0f, (_count - 1) * (_segmentLength + gap) + _segmentLength);
		var exit = new Marker2D { Name = "Exit", Position = lastEnd };
		AddChild(exit);
		exit.Owner = this;
	}
}
