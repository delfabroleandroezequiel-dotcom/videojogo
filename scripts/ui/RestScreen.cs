using Godot;
using System;

namespace Metroidvania.UI;

public partial class RestScreen : CanvasLayer
{
	[Export] public float FadeDuration = 0.5f;
	[Export] public float HoldDuration = 0.6f;
	[Export] public float MaxBlur = 8f;

	public static RestScreen Instance { get; private set; }

	private Control _panel;
	private ColorRect _blur;
	private ColorRect _background;
	private Label _label;
	private ShaderMaterial _blurMaterial;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_panel = GetNode<Control>("Panel");
		_blur = GetNode<ColorRect>("Panel/Blur");
		_background = GetNode<ColorRect>("Panel/Background");
		_label = GetNode<Label>("Panel/Label");
		_blurMaterial = (ShaderMaterial)_blur.Material;

		_panel.Visible = false;
		SetTransitionAmount(0f);
	}

	public void Show(string message, Action onBlackout)
	{
		_label.Text = message;
		_panel.Visible = true;
		GetTree().Paused = true;

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(SetTransitionAmount), 0f, 1f, FadeDuration);
		tween.TweenCallback(Callable.From(() => onBlackout?.Invoke()));
		tween.TweenInterval(HoldDuration);
		tween.TweenMethod(Callable.From<float>(SetTransitionAmount), 1f, 0f, FadeDuration);
		tween.TweenCallback(Callable.From(() =>
		{
			_panel.Visible = false;
			GetTree().Paused = false;
		}));
	}

	private void SetTransitionAmount(float t)
	{
		_blurMaterial.SetShaderParameter("blur_amount", t * MaxBlur);
		_background.Modulate = new Color(1, 1, 1, t);
		_label.Modulate = new Color(1, 1, 1, t);
	}
}
