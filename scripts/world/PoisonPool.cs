using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A ground poison zone that cycles Dormant -> Active forever, same two-state timer idiom as
// SpikeTrap. Dormant is the deliberate "safe to attack" window; Active both punishes contact hard
// (once, on entry) and leaves a lingering poison drain via the target's own ApplyPoison, so simply
// stepping out doesn't end the punishment the way a plain instant hit would.
//
// MinPulses/MaxPulses can widen this into a randomly-sized burst (1-3 pulses back to back, short
// PulseGapDuration flicker between them) but default to a single pulse per cycle — a variable
// burst count made the safe window too unpredictable to actually read and react to in practice.
public partial class PoisonPool : Area2D
{
	[Export] public float DormantDuration = 3f;
	// Matches "pulse"'s own natural playback length (31 frames @ 14fps ≈ 2.2s) so the hazard window
	// and the visual actually agree, instead of the visual finishing early and holding a static
	// frame while still armed.
	[Export] public float ActiveDuration = 2.2f;
	[Export] public int MinPulses = 1;
	[Export] public int MaxPulses = 1;
	[Export] public float PulseGapDuration = 0.5f;
	[Export] public int ContactDamage = 30;
	[Export] public int PoisonTickDamage = 4;
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDuration = 3f;
	[Export] public string ActiveFramesPath = "res://resources/sprites/FanfxWindSpellLargeGreenSpriteFrames.tres";

	private CollisionShape2D _shape;
	private AnimatedSprite2D _sprite;
	private bool _active;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		_sprite = GetNode<AnimatedSprite2D>("Visual");
		_sprite.SpriteFrames = GD.Load<SpriteFrames>(ActiveFramesPath);
		BodyEntered += OnBodyEntered;

		SetActive(false);
		RunCycle();
	}

	private async void RunCycle()
	{
		while (true)
		{
			await ToSignal(GetTree().CreateTimer(DormantDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;

			int pulses = _rng.RandiRange(MinPulses, MaxPulses);
			for (int i = 0; i < pulses; i++)
			{
				SetActive(true);
				await ToSignal(GetTree().CreateTimer(ActiveDuration), SceneTreeTimer.SignalName.Timeout);
				if (!IsInstanceValid(this))
					return;
				SetActive(false);

				if (i < pulses - 1 && PulseGapDuration > 0f)
				{
					await ToSignal(GetTree().CreateTimer(PulseGapDuration), SceneTreeTimer.SignalName.Timeout);
					if (!IsInstanceValid(this))
						return;
				}
			}
		}
	}

	private void SetActive(bool value)
	{
		_active = value;
		_shape.Disabled = !value;
		_sprite.Visible = value;
		if (!value)
			return;

		// Plays once at its own normal speed (see FanfxWindSpellLargeGreenSpriteFrames.tres, not
		// looping) and holds its last frame for whatever's left of ActiveDuration — reads fine as a
		// lingering mist rather than needing to be stretched/slowed to fill the whole window.
		_sprite.Play("pulse");
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!_active || !body.IsInGroup("player"))
			return;

		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null)
			return;

		stats.TakeDamage(ContactDamage);
		if (body.HasMethod("ApplyPoison"))
			body.Call("ApplyPoison", PoisonTickDamage, PoisonTickInterval, PoisonDuration);
	}
}
