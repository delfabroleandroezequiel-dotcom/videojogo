using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

// One-way shortcut gate, paired with another Gate sharing the same GateId. AlwaysOpen marks the
// "far side" of the pair (reachable first, e.g. after a gauntlet) — interacting with it teleports
// the player to TargetPosition and permanently marks GateId open in SaveManager. The other
// instance (AlwaysOpen = false) stays locked, showing LockedSpeakerKey/LockedLineKey instead of
// teleporting, until GateId shows up as opened — same tease-then-payoff idea as a Lever unlocking
// a Door, but for a same-map teleport pair instead of a physical barrier.
public partial class Gate : Area2D
{
	[Export] public string GateId = "";
	[Export] public Vector2 TargetPosition;
	[Export] public bool AlwaysOpen;
	[Export] public string LockedSpeakerKey = "GATE_LOCKED_SPEAKER";
	[Export] public string LockedLineKey = "GATE_LOCKED_LINE";

	private Label _interactPrompt;
	private bool _playerInRange;
	private Node2D _playerBody;

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
			GetViewport().SetInputAsHandled();
			Interact();
		}
	}

	private void Interact()
	{
		if (AlwaysOpen || SaveManager.Instance.IsGateOpened(GateId))
		{
			SaveManager.Instance.MarkGateOpened(GateId);

			Node2D player = _playerBody;
			Vector2 target = TargetPosition;
			TeleportFader.FadeTeleport(GetTree(), () =>
			{
				player.GlobalPosition = target;
				player.GetNodeOrNull<Camera2D>("Camera2D")?.ResetSmoothing();
			});
		}
		else
		{
			DialogueBox.Instance.Show(LockedSpeakerKey, new[] { LockedLineKey });
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;
		_playerBody = body;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = true;
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
}
