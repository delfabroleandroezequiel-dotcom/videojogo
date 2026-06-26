using Godot;
using System;

namespace Metroidvania.UI;

public partial class ConfirmPrompt : CanvasLayer
{
	public static ConfirmPrompt Instance { get; private set; }

	private Control _panel;
	private Label _questionLabel;
	private Action _onYes;
	private Action _onNo;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_panel = GetNode<Control>("Panel");
		_questionLabel = GetNode<Label>("Panel/VBox/Question");
		GetNode<Button>("Panel/VBox/YesButton").Pressed += OnYesPressed;
		GetNode<Button>("Panel/VBox/NoButton").Pressed += OnNoPressed;

		_panel.Visible = false;
	}

	public void Show(string question, Action onYes, Action onNo = null)
	{
		_questionLabel.Text = question;
		_onYes = onYes;
		_onNo = onNo;
		_panel.Visible = true;
		GetTree().Paused = true;
	}

	private void OnYesPressed()
	{
		HidePrompt();
		_onYes?.Invoke();
	}

	private void OnNoPressed()
	{
		HidePrompt();
		_onNo?.Invoke();
	}

	private void HidePrompt()
	{
		_panel.Visible = false;
		GetTree().Paused = false;
	}
}
