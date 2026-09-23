using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Small patch of flames left on the ground (e.g. where Bandido4's torch swing lands): grows in,
// burns for a few seconds, then dies down. Same grow/hold/fade lifecycle as PoisonPuddle, but
// instead of applying a poison-over-time status it burns directly: a small Fire-typed hit every
// TickInterval for as long as the player stands in it, so stepping out stops the damage at once.
// Ticks neither respect nor grant i-frames: each one is tiny, the regular post-hit invulnerability
// would otherwise swallow most of them, and if they armed it, standing in the fire would make the
// player immune to the real hit that comes with it (Bandido4's torch).
public partial class GroundFire : Area2D
{
	[Export] public int TickDamage = 3;
	[Export] public float TickInterval = 0.5f;
	[Export] public float GrowDuration = 0.2f;
	[Export] public float HoldDuration = 2.5f;
	[Export] public float FadeDuration = 0.4f;

	public const string GroupName = "ground_fire";

	// Still dangerous (not yet dying down) — lets enemies (Bandido4) tell when the player is standing in one.
	public bool IsBurning => _burning;

	private Node2D _visual;
	private PointLight2D _light;
	private GpuParticles2D _embers;
	private float _tickTimer;
	private bool _burning = true;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_light = GetNodeOrNull<PointLight2D>("Visual/Light");
		_embers = GetNodeOrNull<GpuParticles2D>("Visual/Embers");
		_visual.Scale = Vector2.Zero;
		AddToGroup(GroupName);
		Animate();
	}

	public override void _PhysicsProcess(double delta)
	{
		_tickTimer -= (float)delta;
		if (!_burning || _tickTimer > 0f)
			return;

		foreach (Node2D body in GetOverlappingBodies())
		{
			if (!body.IsInGroup("player") || body.GetNodeOrNull<Stats>("Stats") is not { } stats)
				continue;

			stats.TakeDamage(TickDamage, element: DamageElement.Fire, ignoreInvulnerability: true, armInvulnerability: false);
			_tickTimer = TickInterval;
		}
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

		// Stops hurting as soon as it starts dying down, so the fading embers read as harmless.
		_burning = false;
		if (_embers is not null)
			_embers.Emitting = false;

		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_visual, "scale:y", 0f, FadeDuration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		fadeTween.Parallel().TweenProperty(_visual, "modulate:a", 0f, FadeDuration);
		if (_light is not null)
			fadeTween.Parallel().TweenProperty(_light, "energy", 0f, FadeDuration);
		await ToSignal(fadeTween, Tween.SignalName.Finished);
		if (IsInstanceValid(this))
			QueueFree();
	}
}
