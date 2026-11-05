using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class SavePoint : Area2D
{
	[Export] public Vector2 RespawnOffset = new(-50, 0);

	private string _persistenceId;
	private bool _isLit;
	private bool _playerInRange;
	private Node2D _unlitVisual;
	private Node2D _litVisual;
	private Label _interactPrompt;

	public override void _Ready()
	{
		_persistenceId = GetPath().ToString();
		_unlitVisual = GetNode<Node2D>("UnlitVisual");
		_litVisual = GetNode<Node2D>("LitVisual");
		_interactPrompt = GetNode<Label>("InteractPrompt");
		_interactPrompt.Visible = false;

		_isLit = SaveManager.Instance.IsSavePointLit(_persistenceId);
		UpdateVisual();

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange)
			return;

		if (@event.IsActionPressed("interact"))
		{
			Interact();
			GetViewport().SetInputAsHandled();
		}
	}

	private void UpdateVisual()
	{
		_unlitVisual.Visible = !_isLit;
		_litVisual.Visible = _isLit;
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

	// Leveling up doesn't belong at every hoguera (user, 2026-11-05): only the ones with this on
	// open the level-up menu before resting. Where it goes is still to be decided.
	[Export] public bool PermiteSubirNivel = false;

	// No questions asked (user, 2026-11-05): an unlit hoguera lights on interact, a lit one rests.
	// Lighting doesn't rest by itself — the burst is its own moment; interact again to rest.
	private void Interact()
	{
		if (!_isLit)
			Light();
		else if (PermiteSubirNivel)
			LevelUpUI.Open(this, Rest);
		else
			Rest();
	}

	private void Light()
	{
		_isLit = true;
		SaveManager.Instance.MarkSavePointLit(_persistenceId);
		UpdateVisual();
		HogueraFx.Ignite(this);
	}

	private void Rest()
	{
		if (GetTree().CurrentScene is LevelBootstrap level)
			RestScreen.Instance.Show("UI_RESTSCREEN_TEXT", () => level.SaveAtCheckpoint(GlobalPosition + RespawnOffset));
	}
}
