using Godot;

namespace Metroidvania.Player;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 200f;
	[Export] public float JumpVelocity = -400f;
	[Export] public float Gravity = 900f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			velocity.Y = JumpVelocity;

		float direction = Input.GetAxis("move_left", "move_right");
		velocity.X = direction != 0 ? direction * Speed : Mathf.MoveToward(velocity.X, 0, Speed);

		Velocity = velocity;
		MoveAndSlide();
	}
}
