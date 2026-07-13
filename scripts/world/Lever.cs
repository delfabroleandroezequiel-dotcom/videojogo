using Godot;

namespace Metroidvania.World;

public partial class Lever : Area2D
{
	// Optional — a Lever doesn't need an Elevator at all. Leave ElevatorPath empty and instead
	// wire this node's Activated signal (Node > Signals in the editor) to whatever it should
	// trigger, e.g. a Door.Open(). Both can be used together on the same lever if needed.
	[Export] public NodePath ElevatorPath;
	[Export] public bool CallsToTop;

	// A lever that permanently unlocks something (like a door) shouldn't keep responding to
	// interact afterwards. Elevator call-levers should stay repeatable, so this defaults off.
	[Export] public bool SingleUse;

	[Signal] public delegate void ActivatedEventHandler();

	private Elevator _elevator;
	private Polygon2D _handle;
	private Label _interactPrompt;
	private bool _playerInRange;
	private bool _activated;

	public override void _Ready()
	{
		if (ElevatorPath is not null && !ElevatorPath.IsEmpty)
			_elevator = GetNode<Elevator>(ElevatorPath);

		_handle = GetNodeOrNull<Polygon2D>("Handle");
		_interactPrompt = GetNode<Label>("InteractPrompt");
		_interactPrompt.Visible = false;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange || (SingleUse && _activated))
			return;

		if (@event.IsActionPressed("interact"))
		{
			Activate();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Activate()
	{
		_activated = true;
		if (_handle is not null)
			_handle.RotationDegrees = -40f;

		if (_elevator is not null)
		{
			if (CallsToTop)
				_elevator.CallToTop();
			else
				_elevator.CallToBottom();
		}

		EmitSignal(SignalName.Activated);

		if (SingleUse)
			_interactPrompt.Visible = false;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;
		if (!(SingleUse && _activated))
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
