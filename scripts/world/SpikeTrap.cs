using Godot;

namespace Metroidvania.World;

public partial class SpikeTrap : StaticBody2D
{
	private enum SpikeState { Retracted, Extending, Extended, Retracting }

	[Export] public float RetractedDuration = 1.5f;
	[Export] public float ExtendDuration = 0.35f;
	[Export] public float ExtendedDuration = 1.2f;
	[Export] public float RetractDuration = 0.35f;
	[Export] public float StartOffset = 0f;

	// The base-plate CollisionShape2D (root, not HazardArea's) is what the player actually stands
	// on — separate from the spike damage hazard. On by default. Turn off when the surrounding
	// floor already provides real ground and this plate would just be a slightly-misaligned lip
	// that catches the player when walking sideways into it.
	[Export] public bool HasFloorCollision = true;
	// What the spikes do on contact, passed to the child HazardArea (same as SawTrap/PendulumTrap),
	// so each trap is tuned from its own Inspector without "Editable Children". Defaults = the scene's.
	[Export] public bool InstantKill = false;
	[Export] public int Damage = 25;
	[Export] public float KnockbackForce = 260f;

	// Last stretch of the retracted phase where the tips poke out of the slots as a warning.
	[Export] public float WarningDuration = 0.3f;
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float WarningExtension = 0.12f;

	private PinchosFila _visual;
	private CollisionShape2D _hazardShape;
	private SpikeState _state = SpikeState.Retracted;
	private float _stateTime;

	public override void _Ready()
	{
		_visual = GetNode<PinchosFila>("Visual");
		_hazardShape = GetNode<CollisionShape2D>("HazardArea/CollisionShape2D");
		_hazardShape.Disabled = true;
		var hazard = GetNode<Hazard>("HazardArea");
		hazard.InstantKill = InstantKill;
		hazard.Damage = Damage;
		hazard.KnockbackForce = KnockbackForce;
		GetNode<CollisionShape2D>("CollisionShape2D").Disabled = !HasFloorCollision;
		_stateTime = -StartOffset;
		// Every trap in a row shares the scene's Seed; derive one from the position so neighbours
		// don't look like copies of each other.
		_visual.Seed = (int)GD.Hash(GlobalPosition.Round());
		_visual.Extension = 0f;
	}

	public override void _Process(double delta)
	{
		_stateTime += (float)delta;

		float duration = GetStateDuration(_state);
		if (_stateTime >= duration)
		{
			_stateTime -= duration;
			AdvanceState();
			duration = GetStateDuration(_state);
		}

		float progress = duration <= 0f ? 1f : Mathf.Clamp(_stateTime / duration, 0f, 1f);
		_visual.Extension = GetExtension(progress, duration);
	}

	private float GetStateDuration(SpikeState state) => state switch
	{
		SpikeState.Retracted => RetractedDuration,
		SpikeState.Extending => ExtendDuration,
		SpikeState.Extended => ExtendedDuration,
		_ => RetractDuration,
	};

	private void AdvanceState()
	{
		_state = _state switch
		{
			SpikeState.Retracted => SpikeState.Extending,
			SpikeState.Extending => SpikeState.Extended,
			SpikeState.Extended => SpikeState.Retracting,
			_ => SpikeState.Retracted,
		};

		// Only deadly once the spikes are fully out — matches the read-and-time trap pattern
		// (full extension is the only pose where the silhouette actually blocks the safe path).
		_hazardShape.Disabled = _state != SpikeState.Extended;
	}

	private float GetExtension(float progress, float duration)
	{
		switch (_state)
		{
			case SpikeState.Retracted:
				float warnStart = duration - WarningDuration;
				if (WarningDuration <= 0f || _stateTime < warnStart)
					return 0f;
				// Tips poke out and jitter slightly: the "about to fire" tell.
				float warn = Mathf.Clamp((_stateTime - warnStart) / WarningDuration, 0f, 1f);
				return WarningExtension * Mathf.Min(1f, warn * 3f) * (0.85f + 0.15f * Mathf.Sin(_stateTime * 60f));
			case SpikeState.Extending:
				// Ease-out: snaps up fast and settles, so it reads as a strike rather than a slide.
				return Mathf.Lerp(WarningExtension, 1f, 1f - Mathf.Pow(1f - progress, 3f));
			case SpikeState.Extended:
				return 1f;
			default:
				return 1f - progress * progress;
		}
	}
}
