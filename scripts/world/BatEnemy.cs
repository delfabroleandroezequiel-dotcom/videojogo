using Godot;

namespace Metroidvania.World;

// Hunts the player like any FlyingEnemy, then commits to a full dive-bomb once in range: lunge
// in, hang at the player for a beat, and peel off into a short retreat before it's willing to
// dive again. A beast, not a swordsman — there's no separate attack hitbox here, the dive just
// puts its body where the player is and ordinary contact damage (see Enemy.ApplyContactDamage)
// does the rest. The retreat is what keeps it from just being a slow projectile — it disengages
// instead of grinding contact damage against the player forever.
// StartsAsleep hangs it from the ceiling (the sheet's "fall" pose) until the player comes within
// WakeRadius or hits it; it then unfolds ("fly_to_fall" played backwards), drops a little and
// hunts as usual. Harmless while asleep.
public partial class BatEnemy : FlyingEnemy
{
	[Export] public bool StartsAsleep = false;
	[Export] public float WakeRadius = 140f;
	[Export] public float WakeDrop = 14f;
	[Export] public float AttackRange = 56f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float DiveSpeed = 260f;
	[Export] public float DiveDuration = 0.3f;
	[Export] public float BiteDuration = 0.18f;
	[Export] public float RecoverDuration = 0.45f;
	[Export] public float RetreatSpeed = 140f;

	private enum AttackPhase { Dive, Bite, Retreat }

	private bool _attacking;
	private bool _canAttack = true;
	private Vector2 _diveDirection;
	private float _phaseTimer;
	private AttackPhase _phase;
	private bool _asleep;
	private float _wakeTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval || !StartsAsleep)
			return;
		_asleep = true;
		Sprite?.Play("fall");
		Stats.HitTaken += (isProjectile) => Wake();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_asleep)
		{
			if (EnemyTargeting.Current(GetTree()) is Node2D target
				&& GlobalPosition.DistanceTo(target.GlobalPosition) <= WakeRadius)
				Wake();
			return;
		}
		if (_wakeTimer > 0f)
		{
			// Unfolding: let go of the ceiling and sink a little before taking off.
			float step = (float)delta;
			_wakeTimer -= step;
			Anchor += new Vector2(0f, WakeDrop * step / WakeDuration());
			GlobalPosition = Anchor;
			if (_wakeTimer <= 0f)
				Sprite?.Play("fly");
			return;
		}

		base._PhysicsProcess(delta);

		if (_attacking || !_canAttack || !Hunting || Player is null)
			return;

		if (Player is Metroidvania.Player.Player player && player.IsDashing)
			return;

		if (GlobalPosition.DistanceTo(Player.GlobalPosition) <= AttackRange)
			StartAttack();
	}

	protected override bool OverrideSteering(double delta)
	{
		if (!_attacking)
			return false;

		_phaseTimer -= (float)delta;
		switch (_phase)
		{
			case AttackPhase.Dive:
				GlobalPosition += _diveDirection * DiveSpeed * (float)delta;
				if (_phaseTimer <= 0f)
					EnterBitePhase();
				break;
			case AttackPhase.Bite:
				GlobalPosition += _diveDirection * DiveSpeed * 0.4f * (float)delta;
				if (_phaseTimer <= 0f)
					EnterRetreatPhase();
				break;
			case AttackPhase.Retreat:
				GlobalPosition -= _diveDirection * RetreatSpeed * (float)delta;
				if (_phaseTimer <= 0f)
					EndAttack();
				break;
		}

		Anchor = GlobalPosition;
		return true;
	}

	private void Wake()
	{
		if (!_asleep || !IsInstanceValid(this))
			return;
		_asleep = false;
		_wakeTimer = WakeDuration();
		if (Sprite?.SpriteFrames?.HasAnimation("fly_to_fall") == true)
			Sprite.PlayBackwards("fly_to_fall");
	}

	private float WakeDuration()
	{
		if (Sprite?.SpriteFrames is not { } frames || !frames.HasAnimation("fly_to_fall"))
			return 0.2f;
		return frames.GetFrameCount("fly_to_fall") / (float)Mathf.Max(1.0, frames.GetAnimationSpeed("fly_to_fall"));
	}

	private void StartAttack()
	{
		_attacking = true;
		_canAttack = false;
		Sprite?.Play("attack");

		_diveDirection = (Player.GlobalPosition - GlobalPosition).Normalized();
		_phase = AttackPhase.Dive;
		_phaseTimer = DiveDuration;
	}

	private void EnterBitePhase()
	{
		_phase = AttackPhase.Bite;
		_phaseTimer = BiteDuration;
	}

	private void EnterRetreatPhase()
	{
		_phase = AttackPhase.Retreat;
		_phaseTimer = RecoverDuration;
	}

	private async void EndAttack()
	{
		_attacking = false;
		Sprite?.Play("fly");

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
