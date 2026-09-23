using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Ground residue left by PoisonSpit: grows in with a little overshoot, holds, then fades out.
// While it's alive it also functions as a lingering hazard — same ApplyPoison contact contract
// PoisonPool uses, just re-armed as long as the puddle itself hasn't faded yet.
public partial class PoisonPuddle : Area2D
{
	[Export] public float GrowDuration = 0.25f;
	[Export] public float HoldDuration = 3.5f;
	[Export] public float FadeDuration = 1f;
	[Export] public int PoisonTickDamage = 3;
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDurationOnContact = 2f;

	private Node2D _visual;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_visual.Scale = Vector2.Zero;
		BodyEntered += OnBodyEntered;
		Animate();
	}

	private async void Animate()
	{
		Tween growTween = CreateTween();
		growTween.TweenProperty(_visual, "scale", Vector2.One, GrowDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		await ToSignal(growTween, Tween.SignalName.Finished);
		if (!IsInstanceValid(this))
			return;

		await ToSignal(GetTree().CreateTimer(HoldDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_visual, "modulate:a", 0f, FadeDuration);
		await ToSignal(fadeTween, Tween.SignalName.Finished);
		if (IsInstanceValid(this))
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null)
			return;

		if (body.HasMethod("ApplyPoison"))
			body.Call("ApplyPoison", PoisonTickDamage, PoisonTickInterval, PoisonDurationOnContact);
	}
}
