using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class TrainingDummy : StaticBody2D
{
	private AnimatedSprite2D _sprite;
	private Stats _stats;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		_stats = GetNode<Stats>("Stats");
		_stats.HealthChanged += OnHealthChanged;
		_sprite.AnimationFinished += OnAnimationFinished;
	}

	// Resetting here (instead of a MaxHealth big enough to "never" run out) makes the dummy
	// truly unkillable: this runs before Stats.TakeDamage's own CurrentHealth<=0 check, so the
	// health is already back to full by the time that check reads it — Died never fires.
	private void OnHealthChanged(int current, int max)
	{
		if (current >= max)
			return;

		_stats.SetCurrentHealth(max);
		_sprite.Play("hit");
	}

	private void OnAnimationFinished()
	{
		if (_sprite.Animation == "hit")
			_sprite.Play("idle");
	}
}
