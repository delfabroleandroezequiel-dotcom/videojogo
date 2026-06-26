using Godot;

namespace Metroidvania.World;

public partial class Door : Node2D
{
	[Export] public string RequiredKeyId = "";
	[Export] public float OpenSpeed = 150f;
	[Export] public float SlideDistance = 200f;

	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Area2D _detector;
	private bool _isOpen;

	public override void _Ready()
	{
		_body = GetNode<StaticBody2D>("Body");
		_collision = GetNode<CollisionShape2D>("Body/CollisionShape2D");
		_detector = GetNode<Area2D>("Detector");
		_detector.BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_isOpen || !body.IsInGroup("player"))
			return;

		// TODO: once a player key/lever inventory exists, check it here instead of always failing.
		if (!string.IsNullOrEmpty(RequiredKeyId))
			return;

		Open();
	}

	private async void Open()
	{
		_isOpen = true;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_body, "position:y", _body.Position.Y - SlideDistance, SlideDistance / OpenSpeed);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
