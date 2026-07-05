using Godot;

namespace Metroidvania.UI;

public partial class ZoneTitle : CanvasLayer
{
	[Export] public float FadeInDuration = 0.8f;
	[Export] public float HoldDuration = 1.5f;
	[Export] public float FadeOutDuration = 0.8f;

	public static ZoneTitle Instance { get; private set; }

	private Label _label;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_label = GetNode<Label>("Label");
		_label.Modulate = new Color(1, 1, 1, 0);
	}

	public void Show(string zoneName)
	{
		_label.Text = zoneName;
		_label.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		tween.TweenProperty(_label, "modulate:a", 1f, FadeInDuration);
		tween.TweenInterval(HoldDuration);
		tween.TweenProperty(_label, "modulate:a", 0f, FadeOutDuration);
	}
}
