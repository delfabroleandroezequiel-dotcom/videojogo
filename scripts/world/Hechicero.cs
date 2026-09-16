using Godot;
using System.Collections.Generic;

namespace Metroidvania.World;

// Standard ranged caster for now — reuses EvilWizard3's existing "attack"/"hit" frames aliased as
// "shoot"/"hurt" (see EvilWizard3SpriteFrames.tres) as a placeholder until a real spellcast
// animation replaces them. Its own class per the project's enemy-uniqueness rule, even while the
// logic is still minimal — mechanics get layered on here as they're worked out.
public partial class Hechicero : RangedEnemy
{
	// Some casters should plant themselves and just cast instead of closing distance like a
	// normal RangedEnemy — toggle per placement rather than needing a second script.
	[Export] public bool HoldPosition = false;

	// Full attack roster. Each entry has its own Enabled checkbox — every enabled one is in the
	// random pool, not just the first. Disabled entries stay in the array (so their tuning isn't
	// lost) but are skipped when picking the next attack.
	[Export] public HechiceroAttack[] Attacks = System.Array.Empty<HechiceroAttack>();

	// When on, every hit taken blinks the Hechicero to a farther spot away from the player instead
	// of letting it get facetanked in place — makes it a moving target the player has to keep
	// chasing/cutting off rather than a stationary damage sponge.
	[Export] public bool DesaparecerHit = false;
	[Export] public float TeleportMinDistance = 220f;
	[Export] public float TeleportMaxDistance = 380f;
	[Export] public float TeleportVanishDuration = 0.15f;
	[Export] public int TeleportMaxAttempts = 8;

	// Vertical distance from this body's origin down to its collision box's bottom (feet) — same
	// convention as every other enemy's manually-measured offset. Used to plant the feet exactly
	// on the ground point a teleport candidate raycast finds, instead of embedding/floating.
	[Export] public float FeetOffsetFromOrigin = 31f;

	// When on, the Hechicero hops away (real jump arc, not a teleport) whenever the player closes
	// to melee range — a second, preventive way of not standing still to get facetanked, on top of
	// (and independent of) DesaparecerHit's on-hit reaction.
	[Export] public bool EscapeJumpEnabled = false;
	[Export] public float JumpTriggerRange = 60f;
	[Export] public float JumpCooldown = 3f;
	[Export] public float JumpVelocity = -420f;
	[Export] public float JumpAwaySpeed = 180f;

	// RangedEnemy's own CanChase only checks !IsShooting, so without this override a
	// HoldPosition Hechicero would still walk toward the player the moment it wasn't mid-cast
	// (e.g. between shots, or after the player steps out of ShootRange and the cooldown lapses).
	protected override bool CanChase => !HoldPosition && !IsShooting;

	private readonly RandomNumberGenerator _rng = new();
	private bool _wasShooting;
	private HechiceroAttack _currentAttack;
	private bool _isTeleporting;
	private bool _isJumping;
	private float _jumpCooldownTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

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
		// the instant it ran. Mirrors the same early-return shape Enemy.cs already uses for its own
		// knockback state.
		if (_isJumping)
		{
			Vector2 velocity = Velocity;
			velocity.Y += Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(Velocity);

			if (IsOnFloor())
				_isJumping = false;

			return;
		}

		base._PhysicsProcess(delta);

		// Re-rolled only on the true->false edge (shot fully finished), never while a shot is
		// still in flight/animating — changing ProjectileScene/Speed mid-cast would otherwise
		// desync the projectile that's about to spawn from the swing the player is watching.
		if (_wasShooting && !IsShooting)
			ApplyRandomEnabledAttack();

		_wasShooting = IsShooting;

		if (EscapeJumpEnabled)
			TryStartEscapeJump();
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

	private void ApplyRandomEnabledAttack()
	{
		List<HechiceroAttack> enabled = new();
		foreach (HechiceroAttack attack in Attacks)
		{
			if (attack is not null && attack.Enabled)
				enabled.Add(attack);
		}

		if (enabled.Count == 0)
			return;

		ApplyAttack(enabled[_rng.RandiRange(0, enabled.Count - 1)]);
	}

	private void ApplyAttack(HechiceroAttack attack)
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

	// Fires the current attack's ProjectileCount shots in sequence (BurstInterval apart) instead
	// of RangedEnemy's usual single shot — e.g. a 3-shot homing volley fired in a row, each one
	// independently chasing the player for its own HomingProjectile.HomingDuration.
	protected override async System.Threading.Tasks.Task FireAsync(Vector2 targetPosition)
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

		base.UpdateAnimation(velocity);
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
	// player (the whole point of the ability), falling back to the near side only if the far side
	// keeps missing (e.g. a ledge/pit right past this Hechicero) so the ability still fires near
	// map edges instead of silently doing nothing.
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
