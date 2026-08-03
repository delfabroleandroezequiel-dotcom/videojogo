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

	private AnimatedSprite2D _sprite;
	private CollisionShape2D _hazardShape;
	private SpikeState _state = SpikeState.Retracted;
	private float _stateTime;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Visual");
		_hazardShape = GetNode<CollisionShape2D>("HazardArea/CollisionShape2D");
		_hazardShape.Disabled = true;
		GetNode<CollisionShape2D>("CollisionShape2D").Disabled = !HasFloorCollision;
		_stateTime = -StartOffset;
		_sprite.Frame = 0;
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
		_sprite.Frame = GetFrame(progress);
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
		// (frame 3 is the only pose where the sprite silhouette actually blocks the safe path).
		_hazardShape.Disabled = _state != SpikeState.Extended;
	}

	private int GetFrame(float progress) => _state switch
	{
		SpikeState.Retracted => 0,
		SpikeState.Extending => Mathf.RoundToInt(progress * 3f),
		SpikeState.Extended => 3,
		_ => Mathf.RoundToInt((1f - progress) * 3f),
	};
}
