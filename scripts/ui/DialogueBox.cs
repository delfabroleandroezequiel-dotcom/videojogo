using Godot;
using System;

namespace Metroidvania.UI;

public partial class DialogueBox : CanvasLayer
{
	public static DialogueBox Instance { get; private set; }

	private Control _panel;
	private Label _nameLabel;
	private Label _textLabel;
	private Label _continueHint;
	private string[] _lines;
	private int _lineIndex;
	private Action _onFinished;
	private bool _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_panel = GetNode<Control>("Panel");
		_nameLabel = GetNode<Label>("Panel/VBox/Name");
		_textLabel = GetNode<Label>("Panel/VBox/Text");
		_continueHint = GetNode<Label>("Panel/VBox/ContinueHint");

		_panel.Visible = false;
	}

	public bool IsOpen => _isOpen;

	public void Show(string speakerName, string[] lines, Action onFinished = null)
	{
		if (lines is null || lines.Length == 0)
			return;

		_nameLabel.Text = speakerName;
		_lines = lines;
		_lineIndex = 0;
		_onFinished = onFinished;
		_isOpen = true;
		_panel.Visible = true;
		GetTree().Paused = true;
		DisplayCurrentLine();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isOpen)
			return;

		if (@event.IsActionPressed("interact") || @event.IsActionPressed("attack"))
		{
			Advance();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Advance()
	{
		_lineIndex++;
		if (_lineIndex >= _lines.Length)
		{
			Close();
			return;
		}

		DisplayCurrentLine();
	}

	private void DisplayCurrentLine()
	{
		_textLabel.Text = _lines[_lineIndex];
		_continueHint.Text = _lineIndex < _lines.Length - 1 ? "[E] continuar" : "[E] cerrar";
	}

	private void Close()
	{
		_isOpen = false;
		_panel.Visible = false;
		GetTree().Paused = false;
		Action callback = _onFinished;
		_onFinished = null;
		callback?.Invoke();
	}
}
