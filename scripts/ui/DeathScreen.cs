using Godot;
using System;

namespace Metroidvania.UI;

public partial class DeathScreen : CanvasLayer
{
	[Export] public float FadeInDuration = 0.6f;
	[Export] public float HoldDuration = 1.2f;

	public static DeathScreen Instance { get; private set; }

	private Control _panel;
	private ColorRect _background;
	private Label _label;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_panel = GetNode<Control>("Panel");
		_background = GetNode<ColorRect>("Panel/Background");
		_label = GetNode<Label>("Panel/Label");

		_panel.Visible = false;
		_background.Modulate = new Color(1, 1, 1, 0);
		_label.Modulate = new Color(1, 1, 1, 0);
	}

	public void Show(Action onComplete)
	{
		_panel.Visible = true;
		_background.Modulate = new Color(1, 1, 1, 0);
		_label.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		tween.SetParallel();
		tween.TweenProperty(_background, "modulate:a", 1f, FadeInDuration);
		tween.TweenProperty(_label, "modulate:a", 1f, FadeInDuration);
		tween.Chain().TweenInterval(HoldDuration);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			_panel.Visible = false;
			onComplete?.Invoke();
		}));
	}
}
