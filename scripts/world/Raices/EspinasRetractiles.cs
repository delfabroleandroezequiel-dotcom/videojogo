using Godot;

namespace Metroidvania.World;

// Espinas que se retraen (Las Raíces): a thorn vine that lives inside a wall or the ceiling and
// shoots out on a fixed rhythm — hidden, a short twitching peek as a warning, out fast, held,
// pulled back. It is a real ThornVine (same art and kill area) on a pivot that is scaled along
// its length, so the hazard grows and shrinks with the visual. Rotate the node to aim it
// (0 = out to the right, 90 = straight down); the node sits where it comes out of the wall.
public partial class EspinasRetractiles : Node2D
{
	private const string ThornScenePath = "res://scenes/world/Rehusables/ThornVine.tscn";

	[Export] public float Length = 220f;
	[Export] public float RetractedTime = 1.6f;
	[Export] public float WarningTime = 0.5f;
	[Export] public float OutTime = 1.2f;
	// Seconds into the cycle at start, so neighbours can be put out of step.
	[Export] public float Phase;
	[Export] public float VineScale = 0.3f;

	private enum Stage { Retracted, Warning, Out, Retracting }

	private Node2D _pivot;
	private Stage _stage = Stage.Retracted;
	private float _timer;

	public override void _Ready()
	{
		_pivot = new Node2D { Name = "Pivote" };
		AddChild(_pivot);
		var thorn = GD.Load<PackedScene>(ThornScenePath).Instantiate<Node2D>();
		thorn.Set("Width", Length);
		thorn.Set("VineScale", VineScale);
		_pivot.AddChild(thorn);
		SetScale(0.001f);
		_timer = RetractedTime - Mathf.PosMod(Phase, CycleLength());
		if (_timer <= 0f)
			_timer = 0.01f;
	}

	private float CycleLength() => RetractedTime + WarningTime + OutTime + 0.3f;

	private void SetScale(float t) => _pivot.Scale = new Vector2(Mathf.Max(0.001f, t), 1f);

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_timer -= dt;
		switch (_stage)
		{
			case Stage.Retracted:
				if (_timer <= 0f)
				{
					_stage = Stage.Warning;
					_timer = WarningTime;
				}
				break;
			case Stage.Warning:
				// A short twitching peek: enough to read, too short to hurt anyone outside the wall.
				SetScale(0.06f + 0.04f * Mathf.Sin(_timer * 60f));
				if (_timer <= 0f)
				{
					_stage = Stage.Out;
					_timer = OutTime;
					CreateTween().TweenMethod(Callable.From<float>(SetScale), 0.1f, 1f, 0.12f);
				}
				break;
			case Stage.Out:
				if (_timer <= 0f)
				{
					_stage = Stage.Retracting;
					_timer = 0.3f;
					CreateTween().TweenMethod(Callable.From<float>(SetScale), 1f, 0.001f, 0.3f);
				}
				break;
			case Stage.Retracting:
				if (_timer <= 0f)
				{
					_stage = Stage.Retracted;
					_timer = RetractedTime;
				}
				break;
		}
	}
}
