using Godot;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class DialogueTrigger : Area2D
{
	[Export] public string SpeakerName = "DIALOGUETRIGGER_DEFAULT_SPEAKER";
	[Export] public string[] DialogueLines = { "NPC_DEFAULT_LINE" };

	private Label _interactPrompt;
	private bool _playerInRange;

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
		if (!_playerInRange || DialogueBox.Instance.IsOpen)
			return;

		if (@event.IsActionPressed("interact"))
		{
			DialogueBox.Instance.Show(SpeakerName, DialogueLines);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}
}
