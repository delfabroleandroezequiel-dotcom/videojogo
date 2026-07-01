using Godot;

namespace Metroidvania.World;

public partial class Elevator : AnimatableBody2D
{
	[Export] public float TravelDistance = 230f;
	[Export] public float Speed = 80f;

	private Vector2 _bottomPosition;
	private Vector2 _topPosition;
	private Vector2 _target;

	public override void _Ready()
	{
		_bottomPosition = Position;
		_topPosition = Position - new Vector2(0, TravelDistance);
		_target = _bottomPosition;

		Area2D triggerZone = GetNode<Area2D>("TriggerZone");
		triggerZone.BodyEntered += OnTriggerEntered;
	}

	private void OnTriggerEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_target = _target == _topPosition ? _bottomPosition : _topPosition;
	}

	public void CallToTop() => _target = _topPosition;
	public void CallToBottom() => _target = _bottomPosition;

	public override void _PhysicsProcess(double delta)
	{
		Position = Position.MoveToward(_target, Speed * (float)delta);
	}
}
