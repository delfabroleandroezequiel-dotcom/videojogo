using Godot;

namespace Metroidvania.World;

public partial class SlimeEnemy : Enemy
{
	[Export] public float HurtAnimDuration = 0.35f;

	private float _hurtTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		Stats.HitTaken += (isProjectile) => _hurtTimer = HurtAnimDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_hurtTimer > 0f)
			_hurtTimer -= (float)delta;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _hurtTimer > 0f ? "hurt" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}
}
