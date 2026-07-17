using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

// A scripted back-and-forth between characters, played through the same DialogueBox used for
// NPC conversations — so it inherits the same "game pauses, player can't do anything but read
// and advance" lock for free. Unlike DialogueTrigger (always one fixed speaker, needs Interact),
// this supports several speakers switching line by line, and can fire on its own when the
// player just walks into the area — the classic cutscene trigger volume.
//
// PlayOnce is persisted per save (SaveManager.PlayedCutscenes), not just an in-memory flag —
// so once it's been seen it stays seen even after resting at a save point or reloading, unlike
// common-enemy defeats which are meant to reset every checkpoint.
public partial class CutsceneTrigger : Area2D
{
	[Export] public string[] SpeakerKeys = System.Array.Empty<string>();
	[Export] public string[] LineKeys = System.Array.Empty<string>();
	[Export] public bool RequireInteract;
	[Export] public bool PlayOnce = true;
	[Export] public string CustomPersistenceId = "";

	private Label _interactPrompt;
	private bool _playerInRange;
	private string _persistenceId;

	public override void _Ready()
	{
		_persistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		_interactPrompt = GetNodeOrNull<Label>("InteractPrompt");
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	private bool AlreadyPlayed => PlayOnce && SaveManager.Instance.IsCutscenePlayed(_persistenceId);

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!RequireInteract || !_playerInRange || DialogueBox.Instance.IsOpen || AlreadyPlayed)
			return;

		if (@event.IsActionPressed("interact"))
		{
			Play();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;

		if (RequireInteract)
		{
			if (_interactPrompt is not null)
				_interactPrompt.Visible = !AlreadyPlayed;
			return;
		}

		if (!DialogueBox.Instance.IsOpen && !AlreadyPlayed)
			Play();
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	private void Play()
	{
		if (PlayOnce)
			SaveManager.Instance.MarkCutscenePlayed(_persistenceId);

		DialogueBox.Instance.Show(SpeakerKeys, LineKeys);
	}
}
