using Godot;

namespace Metroidvania.World;

// Ambient decoration: a perched crow that idles and occasionally pecks at the ground.
// Purely visual, no gameplay interaction.
public partial class CrowDecoration : AnimatedSprite2D
{
	[Export] public float MinStateDuration = 2f;
	[Export] public float MaxStateDuration = 5f;

	private RandomNumberGenerator _rng = new();
	private float _stateTimer;

	public override void _Ready()
	{
		_rng.Randomize();
		Play("idle");
		_stateTimer = _rng.RandfRange(MinStateDuration, MaxStateDuration);
	}

	public override void _Process(double delta)
	{
		_stateTimer -= (float)delta;
		if (_stateTimer > 0f)
			return;

		Play(Animation == "idle" ? "peck" : "idle");
		_stateTimer = _rng.RandfRange(MinStateDuration, MaxStateDuration);
	}
}
