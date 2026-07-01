using Godot;

namespace Metroidvania.World;

public partial class Lever : Area2D
{
	[Export] public NodePath ElevatorPath;
	[Export] public bool CallsToTop;

	private Elevator _elevator;
	private Label _interactPrompt;
	private bool _playerInRange;

	public override void _Ready()
	{
		_elevator = GetNode<Elevator>(ElevatorPath);
		_interactPrompt = GetNode<Label>("InteractPrompt");
		_interactPrompt.Visible = false;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange)
			return;

		if (@event.IsActionPressed("interact"))
		{
			if (CallsToTop)
				_elevator.CallToTop();
			else
				_elevator.CallToBottom();

			GetViewport().SetInputAsHandled();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;
		_interactPrompt.Visible = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_interactPrompt.Visible = false;
	}
}
