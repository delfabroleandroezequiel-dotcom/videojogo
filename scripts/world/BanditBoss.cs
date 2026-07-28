using Godot;

namespace Metroidvania.World;

public partial class BanditBoss : Boss
{
	[Export] public float ChargedAttackChance = 0.35f;
	[Export] public float ChargeDuration = 0.8f;
	[Export] public float ChargedHitboxReach = 70f;
	[Export] public float ChargedHitboxDuration = 0.18f;
	[Export] public string ChargedAttackAnimation = "attack2";

	private GpuParticles2D _chargeAura;
	private readonly RandomNumberGenerator _bossRng = new();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_chargeAura = Visual.GetNodeOrNull<GpuParticles2D>("ChargeAura");
		_bossRng.Randomize();
	}

	protected override void Attack(bool isCombo = false)
	{
		if (!isCombo && Sprite.SpriteFrames.HasAnimation(ChargedAttackAnimation) && _bossRng.Randf() < ChargedAttackChance)
		{
			ChargedAttack();
			return;
		}

		base.Attack(isCombo);
	}

	private async void ChargedAttack()
	{
		_attacking = true;
		_canAttack = false;
		_attackAnimation = ChargedAttackAnimation;
		Sprite.Play(ChargedAttackAnimation);
		if (_chargeAura is not null)
			_chargeAura.Emitting = true;

		await ToSignal(GetTree().CreateTimer(ChargeDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		if (_chargeAura is not null)
			_chargeAura.Emitting = false;

		_hitbox.Position = new Vector2(FacingRight ? ChargedHitboxReach : -ChargedHitboxReach, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(ChargedHitboxDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();
		_attacking = false;

		float cooldown = AttackCooldown * (IsEnraged ? EnrageAttackCooldownMultiplier : 1f);
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
