using Godot;

namespace Metroidvania.World;

// Reusable greybox staircase: set Count and get that many MiniCorridor floor segments chained
// directly one after another (no gap, unlike MiniCorridorAccordion/MiniPlataformaAccordion —
// this isn't testing a jump, it's just a walkable stepped floor), each one offset from the
// previous by StepHeight. Negative StepHeight climbs (steps go up), positive descends — same sign
// convention as VerticalStep on the other Mini*Accordion pieces.
// Purely the walkable skeleton: no riser walls, no decoration — same "just set the count" idea as
// SpikeAccordion, applied to stairs. Detach a step into its own MiniCorridor.tscn instance if one
// needs to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class MiniEscaleraAccordion : Node2D
{
	private const string MiniCorridorScenePath = "res://scenes/world/Rehusables/MiniCorridor.tscn";

	private int _count = 5;

	[Export(PropertyHint.Range, "1,60,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _stepDepth = 64f;

	[Export]
	public float StepDepth
	{
		get => _stepDepth;
		set { _stepDepth = Mathf.Max(1f, value); Rebuild(); }
	}

	private float _stepHeight = -32f;

	// Vertical offset from one step to the next — negative climbs, positive descends.
	[Export]
	public float StepHeight
	{
		get => _stepHeight;
		set { _stepHeight = value; Rebuild(); }
	}

	private float _thickness = 32f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = value; Rebuild(); }
	}

	private bool _climbable;

	// Passed through to every MiniCorridor step — see MiniCorridor.Climbable.
	[Export]
	public bool Climbable
	{
		get => _climbable;
		set { _climbable = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private bool _initialized;

	public override void _Ready()
	{
		_initialized = true;
		Rebuild();
	}

	private void Rebuild()
	{
		// See RopeAccordion.Rebuild for why _initialized gates this — property setters restoring
		// this node's saved state before _Ready runs can otherwise reenter Instantiate<MiniCorridor>()
		// mid scene-instantiation and throw a spurious InvalidCastException.
		if (!_initialized || !IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		PackedScene miniCorridorScene = GD.Load<PackedScene>(MiniCorridorScenePath);

		Vector2 cursor = Vector2.Zero;
		for (int i = 0; i < _count; i++)
		{
			MiniCorridor step = miniCorridorScene.Instantiate<MiniCorridor>();
			step.Name = $"Step{i + 1}";
			step.Orientation = CorridorOrientation.Lateral;
			step.Length = _stepDepth;
			step.Thickness = _thickness;
			step.Climbable = _climbable;
			step.FillColor = FillColor;
			step.Position = cursor;
			AddChild(step);
			step.Owner = this;

			cursor += new Vector2(_stepDepth, _stepHeight);
		}

		// Exit sits right after the last step, so the next block's origin can just be dropped on
		// Exit's global position — same convention as the other Mini*Accordion pieces.
		var exit = new Marker2D { Name = "Exit", Position = cursor };
		AddChild(exit);
		exit.Owner = this;
	}
}
