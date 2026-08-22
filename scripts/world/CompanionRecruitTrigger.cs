using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

// One-shot pickup: interact once to recruit the dog companion. Sets the persisted
// SaveManager.CompanionRecruited flag (so LevelBootstrap re-spawns it in every future map load —
// see LevelBootstrap.SpawnCompanionIfRecruited) and spawns it immediately in this same scene,
// then removes itself so it can't be triggered twice.
public partial class CompanionRecruitTrigger : Area2D
{
	[Export] public PackedScene CompanionScene;

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
		if (!_playerInRange || SaveManager.Instance.CompanionRecruited)
			return;

		if (@event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			Recruit();
		}
	}

	private void Recruit()
	{
		SaveManager.Instance.CompanionRecruited = true;

		Node companion = CompanionScene.Instantiate();
		GetTree().CurrentScene.AddChild(companion);
		((Node2D)companion).GlobalPosition = GlobalPosition;

		ZoneTitle.Instance.Show("UI_COMPANION_RECRUITED");
		QueueFree();
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
