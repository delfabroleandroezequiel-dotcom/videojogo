using Godot;

namespace Metroidvania.World;

// Generic "rock side to side" component — attach directly to any Node2D (a ship's
// AnimatedSprite2D root, a sign, a hanging lantern, etc.) to give it a subtle continuous
// rotation oscillation. PhaseOffset lets several instances rock out of sync with each other
// instead of all tilting in lockstep.
public partial class RotationSway : Node2D
{
	[Export] public float AmplitudeDegrees = 3f;
	[Export] public float Speed = 0.6f;
	[Export] public float PhaseOffset = 0f;

	private float _time;
	private float _baseRotation;

	public override void _Ready()
	{
		_baseRotation = Rotation;
		_time = PhaseOffset;
	}

	public override void _Process(double delta)
	{
		_time += (float)delta * Speed;
		Rotation = _baseRotation + Mathf.DegToRad(Mathf.Sin(_time) * AmplitudeDegrees);
	}
}
