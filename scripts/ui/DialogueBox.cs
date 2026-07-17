using Godot;
using Metroidvania.Shared;
using System;

namespace Metroidvania.UI;

public partial class DialogueBox : CanvasLayer
{
	public static DialogueBox Instance { get; private set; }

	private Control _panel;
	private Label _nameLabel;
	private Label _textLabel;
	private Label _continueHint;
	private string[] _speakerKeys;
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

	public void Show(string speakerNameKey, string[] lineKeys, Action onFinished = null)
	{
		Show(new[] { speakerNameKey }, lineKeys, onFinished);
	}

	// Cutscene-style overload: SpeakerKeys can list a different speaker per line (a back-and-forth
	// between characters), or just one entry to keep speaking as the same character for every
	// line — same as the single-speaker overload above, which just forwards here.
	public void Show(string[] speakerNameKeys, string[] lineKeys, Action onFinished = null)
	{
		if (lineKeys is null || lineKeys.Length == 0 || speakerNameKeys is null || speakerNameKeys.Length == 0)
			return;

		_speakerKeys = speakerNameKeys;
		_lines = lineKeys;
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
		string speakerKey = _speakerKeys[Mathf.Min(_lineIndex, _speakerKeys.Length - 1)];
		_nameLabel.Text = TranslationServer.Translate(speakerKey);
		_textLabel.Text = TranslationServer.Translate(_lines[_lineIndex]);
		_continueHint.Text = TranslationServer.Translate(_lineIndex < _lines.Length - 1 ? "UI_DIALOGUE_CONTINUE" : "UI_DIALOGUE_CLOSE");
		Sfx.PlayVoice(this, _lines[_lineIndex]);
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
