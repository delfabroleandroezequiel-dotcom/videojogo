using Godot;
using Metroidvania.Shared;
using System.Collections.Generic;

namespace Metroidvania.World;

// The old wandering wizard (Wizard Pack sprites, shared with NpcMagoCamino in Nix) as an enemy.
// Deliberately does NOT extend RangedEnemy/Hechicero — its own full copy of the shoot loop,
// attack pool, DesaparecerHit and escape-jump logic, so this caster can diverge from Hechicero
// later without either one dragging the other along. Only Enemy (the universal base every enemy
// needs) is shared.
public partial class MagoCamino : Enemy
{
	// Some casters should plant themselves and just cast instead of closing distance.
	[Export] public bool HoldPosition = false;

	[Export] public float ShootInterval = 2f;
	[Export] public float ShootRange = 350f;
	[Export] public float ShootAnimDuration = 0.92f;
	[Export] public float ShootReleaseDelay = 0.67f;
	[Export] public float HurtAnimDuration = 0.4f;
	[Export] public float ProjectileSpeed = 250f;
	[Export] public PackedScene ProjectileScene;

	// Full attack roster. Each entry has its own Enabled checkbox — every enabled one is in the
	// random pool, not just the first.
	[Export] public MagoCaminoAttack[] Attacks = System.Array.Empty<MagoCaminoAttack>();

	// When on, every hit taken blinks this enemy to a farther spot away from the player instead
	// of letting it get facetanked in place.
	[Export] public bool DesaparecerHit = false;
	[Export] public float TeleportMinDistance = 220f;
	[Export] public float TeleportMaxDistance = 380f;
	[Export] public float TeleportVanishDuration = 0.15f;
	[Export] public int TeleportMaxAttempts = 8;

	// Vertical distance from this body's origin down to its collision box's bottom (feet) — used
	// to plant the feet exactly on the ground point a teleport candidate raycast finds.
	[Export] public float FeetOffsetFromOrigin = 31f;

	// When on, hops away (real jump arc, not a teleport) whenever the player closes to melee
	// range — a second, preventive way of not standing still to get facetanked.
	[Export] public bool EscapeJumpEnabled = false;
	[Export] public float JumpTriggerRange = 60f;
	[Export] public float JumpCooldown = 3f;
	[Export] public float JumpVelocity = -420f;
	[Export] public float JumpAwaySpeed = 180f;

	private double _cooldown;
	protected bool IsShooting;
	private float _hurtTimer;

	private readonly RandomNumberGenerator _rng = new();
	private bool _wasShooting;
	private MagoCaminoAttack _currentAttack;
	private bool _isTeleporting;
	private bool _isJumping;
	private float _jumpCooldownTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = ShootRange * 0.9f;
		Stats.HitTaken += (isProjectile) => _hurtTimer = HurtAnimDuration;

		_rng.Randomize();
		ApplyRandomEnabledAttack();
		Stats.HitTaken += OnHitTaken;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_jumpCooldownTimer > 0f)
			_jumpCooldownTimer -= (float)delta;

		// While airborne, this fully replaces the base AI's movement instead of running alongside
		// it — Enemy._PhysicsProcess recomputes velocity.X toward the player (or decelerates it to
		// ~0) every frame regardless of CanChase, which would stomp the jump's own escape velocity
		// the instant it ran.
		if (_isJumping)
		{
			Vector2 jumpVelocity = Velocity;
			jumpVelocity.Y += Gravity * (float)delta;
			Velocity = jumpVelocity;
			MoveAndSlide();
			UpdateAnimation(Velocity);

			if (IsOnFloor())
				_isJumping = false;

			return;
		}

		base._PhysicsProcess(delta);

		if (_hurtTimer > 0f)
			_hurtTimer -= (float)delta;

		_cooldown -= delta;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is not null)
		{
			if (!(playerNode is Metroidvania.Player.Player player && player.IsDashing))
			{
				float distanceX = Mathf.Abs(playerNode.GlobalPosition.X - GlobalPosition.X);
				if (distanceX <= ShootRange && _cooldown <= 0 && !IsShooting && EnemyCombatCoordinator.TryAcquireAttackSlot())
				{
					HoldingAttackSlot = true;
					Shoot(playerNode.GlobalPosition);
					_cooldown = ShootInterval;
				}
			}
		}

		// Re-rolled only on the true->false edge (shot fully finished), never while a shot is
		// still in flight/animating — changing ProjectileScene/Speed mid-cast would otherwise
		// desync the projectile that's about to spawn from the swing the player is watching.
		if (_wasShooting && !IsShooting)
			ApplyRandomEnabledAttack();

		_wasShooting = IsShooting;

		if (EscapeJumpEnabled)
			TryStartEscapeJump();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;

		if (_isJumping)
		{
			string jumpAnim = velocity.Y < 0f ? "jump" : "fall";
			if (Sprite.Animation != jumpAnim)
				Sprite.Play(jumpAnim);
			return;
		}

		string anim = _hurtTimer > 0f ? "hit" : IsShooting ? "attack1" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool CanTurnToFacePlayer => !IsShooting;
	protected override bool CanChase => !HoldPosition && !IsShooting;

	private async void Shoot(Vector2 targetPosition)
	{
		IsShooting = true;

		try
		{
			await ToSignal(GetTree().CreateTimer(ShootReleaseDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			await FireBurst(targetPosition);

			await ToSignal(GetTree().CreateTimer(ShootAnimDuration - ShootReleaseDelay), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(this))
				IsShooting = false;
		}
		finally
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// Fires the current attack's ProjectileCount shots in sequence (BurstInterval apart) — e.g. a
	// 3-shot homing volley fired in a row.
	private async System.Threading.Tasks.Task FireBurst(Vector2 targetPosition)
	{
		int count = Mathf.Max(1, _currentAttack?.ProjectileCount ?? 1);
		float interval = _currentAttack?.BurstInterval ?? 0f;

		for (int i = 0; i < count; i++)
		{
			SpawnProjectile(targetPosition);
			if (i < count - 1)
				await ToSignal(GetTree().CreateTimer(interval), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private void SpawnProjectile(Vector2 targetPosition)
	{
		Projectile projectile = ProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GlobalPosition;
		projectile.Speed = ProjectileSpeed;
		projectile.Launch(targetPosition - GlobalPosition, Stats);
		// Generic magic-release placeholder until each HechiceroAttack/MagoCaminoAttack carries its
		// own SoundCue field.
		Sfx.PlayAt(this, "Magic", "Fireball");
	}

	private void ApplyRandomEnabledAttack()
	{
		List<MagoCaminoAttack> enabled = new();
		foreach (MagoCaminoAttack attack in Attacks)
		{
			if (attack is not null && attack.Enabled)
				enabled.Add(attack);
		}

		if (enabled.Count == 0)
			return;

		ApplyAttack(enabled[_rng.RandiRange(0, enabled.Count - 1)]);
	}

	private void ApplyAttack(MagoCaminoAttack attack)
	{
		_currentAttack = attack;
		ProjectileScene = attack.ProjectileScene;
		ProjectileSpeed = attack.ProjectileSpeed;
		ShootInterval = attack.Cooldown;
		ShootRange = attack.Range;
		ShootAnimDuration = attack.CastDuration;
		ShootReleaseDelay = attack.ReleaseDelay;
		StopDistance = ShootRange * 0.9f;
	}

	private void TryStartEscapeJump()
	{
		if (_jumpCooldownTimer > 0f || IsShooting || _isTeleporting)
			return;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is null)
			return;

		float distanceX = GlobalPosition.X - playerNode.GlobalPosition.X;
		if (Mathf.Abs(distanceX) > JumpTriggerRange)
			return;

		float awaySign = Mathf.Sign(distanceX);
		if (awaySign == 0f)
			awaySign = FacingRight ? -1f : 1f;

		_isJumping = true;
		_jumpCooldownTimer = JumpCooldown;
		Velocity = new Vector2(awaySign * JumpAwaySpeed, JumpVelocity);
	}

	private void OnHitTaken(bool isProjectile)
	{
		if (DesaparecerHit)
			TryTeleportAwayFromPlayer();
	}

	private async void TryTeleportAwayFromPlayer()
	{
		if (_isTeleporting)
			return;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is null)
			return;

		float awaySign = Mathf.Sign(GlobalPosition.X - playerNode.GlobalPosition.X);
		if (awaySign == 0f)
			awaySign = _rng.RandfRange(0f, 1f) < 0.5f ? -1f : 1f;

		Vector2? landingSpot = FindValidTeleportSpot(awaySign);
		if (landingSpot is null)
			return;

		_isTeleporting = true;
		try
		{
			Velocity = Vector2.Zero;

			if (Sprite is not null)
			{
				Tween fadeOut = GetTree().CreateTween();
				fadeOut.TweenProperty(Sprite, "modulate:a", 0f, TeleportVanishDuration);
				await ToSignal(fadeOut, Tween.SignalName.Finished);
			}

			GlobalPosition = landingSpot.Value;

			if (Sprite is not null)
			{
				Tween fadeIn = GetTree().CreateTween();
				fadeIn.TweenProperty(Sprite, "modulate:a", 1f, TeleportVanishDuration);
				await ToSignal(fadeIn, Tween.SignalName.Finished);
			}
		}
		finally
		{
			_isTeleporting = false;
		}
	}

	// Tries a handful of candidate spots along the ground: first favoring the side away from the
	// player, falling back to the near side only if the far side keeps missing (e.g. a ledge/pit
	// right past this enemy) so the ability still fires near map edges instead of doing nothing.
	private Vector2? FindValidTeleportSpot(float awaySign)
	{
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		Godot.Collections.Array<Rid> exclude = new() { GetRid() };
		const float groundCheckHeight = 600f;

		for (int attempt = 0; attempt < TeleportMaxAttempts; attempt++)
		{
			float sign = attempt < TeleportMaxAttempts / 2 ? awaySign : -awaySign;
			float distance = _rng.RandfRange(TeleportMinDistance, TeleportMaxDistance);
			float candidateX = GlobalPosition.X + sign * distance;

			PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
				new Vector2(candidateX, GlobalPosition.Y - groundCheckHeight),
				new Vector2(candidateX, GlobalPosition.Y + groundCheckHeight),
				CollisionMask,
				exclude);

			Godot.Collections.Dictionary hit = space.IntersectRay(query);
			if (hit.Count > 0)
				return hit["position"].AsVector2() - new Vector2(0f, FeetOffsetFromOrigin);
		}

		return null;
	}
}
