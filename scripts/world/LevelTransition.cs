using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class LevelTransition : Area2D
{
	[Export] public string TargetScenePath = "";
	[Export] public Vector2 TargetSpawnPosition = Vector2.Zero;
	[Export] public bool RequireInteract = false;
	[Export] public bool RememberOriginForReturn = false;
	[Export] public bool UseStoredReturnPosition = false;

	private bool _triggered;
	private bool _playerInRange;
	private Node2D _playerBody;
	private Label _interactPrompt;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		_interactPrompt = GetNodeOrNull<Label>("InteractPrompt");
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!RequireInteract || !_playerInRange || _triggered)
			return;

		if (@event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			Trigger(_playerBody);
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || !body.IsInGroup("player") || string.IsNullOrEmpty(TargetScenePath))
			return;

		if (RequireInteract)
		{
			_playerInRange = true;
			_playerBody = body;
			if (_interactPrompt is not null)
				_interactPrompt.Visible = true;
			return;
		}

		Trigger(body);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_playerBody = null;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	private void Trigger(Node2D body)
	{
		_triggered = true;

		if (RememberOriginForReturn)
			SaveManager.Instance.PendingReturnPosition = GlobalPosition;

		SaveManager.Instance.PendingSpawnPosition =
			UseStoredReturnPosition && SaveManager.Instance.PendingReturnPosition.HasValue
				? SaveManager.Instance.PendingReturnPosition
				: TargetSpawnPosition;

		SceneFader.ChangeSceneWithFade(GetTree(), TargetScenePath);
	}
}
