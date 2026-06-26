using Godot;
using Metroidvania.Save;

namespace Metroidvania.UI;

public partial class MainMenu : Control
{
	private VBoxContainer _topMenu;
	private VBoxContainer _slotMenu;
	private VBoxContainer _namePrompt;
	private VBoxContainer _slotsContainer;
	private LineEdit _nameEdit;
	private Button _continueButton;
	private int _pendingNewSlot = -1;

	public override void _Ready()
	{
		_topMenu = GetNode<VBoxContainer>("TopMenu");
		_slotMenu = GetNode<VBoxContainer>("SlotMenu");
		_namePrompt = GetNode<VBoxContainer>("NamePrompt");
		_slotsContainer = GetNode<VBoxContainer>("SlotMenu/SlotsContainer");
		_nameEdit = GetNode<LineEdit>("NamePrompt/NameEdit");
		_continueButton = GetNode<Button>("TopMenu/ContinueButton");

		_continueButton.Pressed += OnContinuePressed;
		GetNode<Button>("TopMenu/NewGameButton").Pressed += () => ShowSlotMenu();
		GetNode<Button>("SlotMenu/BackButton").Pressed += ShowTopMenu;
		GetNode<Button>("NamePrompt/ConfirmButton").Pressed += OnNameConfirmed;
		GetNode<Button>("NamePrompt/CancelButton").Pressed += ShowTopMenu;

		ShowTopMenu();
	}

	private void ShowTopMenu()
	{
		_continueButton.Disabled = SaveManager.Instance.FindMostRecentSlot() < 0;
		_topMenu.Visible = true;
		_slotMenu.Visible = false;
		_namePrompt.Visible = false;
	}

	private void ShowSlotMenu()
	{
		BuildSlotList();
		_topMenu.Visible = false;
		_slotMenu.Visible = true;
		_namePrompt.Visible = false;
	}

	private void ShowNamePrompt(int slot)
	{
		_pendingNewSlot = slot;
		_nameEdit.Text = "";
		_topMenu.Visible = false;
		_slotMenu.Visible = false;
		_namePrompt.Visible = true;
	}

	private void BuildSlotList()
	{
		foreach (Node child in _slotsContainer.GetChildren())
			child.QueueFree();

		for (int slot = 0; slot < SaveManager.SlotCount; slot++)
		{
			bool hasSave = SaveManager.Instance.HasSaveFile(slot);
			int capturedSlot = slot;

			HBoxContainer row = new();
			_slotsContainer.AddChild(row);

			Button slotButton = new()
			{
				Text = hasSave ? $"{SaveManager.Instance.GetCharacterName(slot)} (ocupado)" : "Vacío — crear nueva",
				Disabled = hasSave,
				CustomMinimumSize = new Vector2(220, 32),
			};
			slotButton.Pressed += () => ShowNamePrompt(capturedSlot);
			row.AddChild(slotButton);

			if (hasSave)
			{
				Button deleteButton = new()
				{
					Text = "Borrar",
					CustomMinimumSize = new Vector2(70, 32),
				};
				deleteButton.Pressed += () => OnDeletePressed(capturedSlot);
				row.AddChild(deleteButton);
			}
		}
	}

	private void OnDeletePressed(int slot)
	{
		SaveManager.Instance.DeleteSave(slot);
		BuildSlotList();
	}

	private void OnContinuePressed()
	{
		int slot = SaveManager.Instance.FindMostRecentSlot();
		if (slot < 0)
			return;

		SaveManager.Instance.CurrentSlot = slot;
		SaveData data = SaveManager.Instance.LoadGame(slot);
		GetTree().ChangeSceneToFile(data.ScenePath);
	}

	private void OnNameConfirmed()
	{
		string name = _nameEdit.Text.Trim();
		if (string.IsNullOrEmpty(name))
			name = "Héroe";

		SaveManager.Instance.CurrentSlot = _pendingNewSlot;
		SaveManager.Instance.CurrentCharacterName = name;
		SaveManager.Instance.ClearPendingLoad();
		SaveManager.Instance.ResetProgressState();
		GetTree().ChangeSceneToFile("res://scenes/world/TestLevel.tscn");
	}
}
