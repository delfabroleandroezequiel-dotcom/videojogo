using Godot;

namespace Metroidvania.World;

// Ambient decoration: a seagull that flies back and forth between its spawn point and a point
// FlightDistance away, with a gentle vertical bob. Purely visual, no gameplay interaction.
public partial class FlyingBirdDecoration : AnimatedSprite2D
{
	[Export] public float FlightDistance = 500f;
	[Export] public float Speed = 60f;
	[Export] public float BobAmplitude = 6f;
	[Export] public float BobSpeed = 2f;

	private float _startX;
	private float _baseY;
	private float _bobPhase;
	private int _direction = 1;

	public override void _Ready()
	{
		_startX = Position.X;
		_baseY = Position.Y;
		Play("fly");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		Position += new Vector2(_direction * Speed * dt, 0f);

		float offset = Position.X - _startX;
		if (_direction > 0 && offset >= FlightDistance)
			_direction = -1;
		else if (_direction < 0 && offset <= 0f)
			_direction = 1;

		_bobPhase += BobSpeed * dt;
		Position = new Vector2(Position.X, _baseY + Mathf.Sin(_bobPhase) * BobAmplitude);

		FlipH = _direction < 0;
	}
}
