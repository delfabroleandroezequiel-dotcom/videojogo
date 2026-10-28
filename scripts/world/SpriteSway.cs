using Godot;

namespace Metroidvania.World;

// Gentle sway for a decoration sprite (hanging vines, garlands, ferns): shears the sprite around its
// own origin, so whatever sits at the origin stays attached and the far end moves. Put the origin
// where the piece is "fixed" — top centre for something hanging (Centered off, Offset = (-w/2, 0)),
// bottom centre for something growing from the floor (Offset = (-w/2, -h)). Several instances with
// different PhaseOffset/Speed sway out of step instead of in lockstep. Runtime only — the editor
// shows the piece still, exactly where it's placed.
public partial class SpriteSway : Sprite2D
{
	[Export] public float AmplitudeDegrees = 2f;
	[Export] public float Speed = 0.5f;
	[Export] public float PhaseOffset;

	private float _time;

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		_time += (float)delta;
		Skew = Mathf.DegToRad(Mathf.Sin(_time * Speed * Mathf.Tau + PhaseOffset) * AmplitudeDegrees);
	}
}
