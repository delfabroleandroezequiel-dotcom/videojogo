using System.Linq;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The old wandering wizard (Wizard Pack sprites, shared with NpcMagoCamino in Nix) as an enemy.
// Deliberately its own full class, not a Hechicero subclass, so the two casters can diverge. Same
// "smart enemy" toolkit as Hechicero/ElfArcher: SpellBrain spell choice (straight / homing /
// volley vs. situation and miss streak), predictive aim for straight bolts, holds the release
// through the player's dash/block, cast cancelled when hit; unless HoldPosition it keeps a distance
// band without walking off ledges, hops back only when the hop lands on ground, and with
// DesaparecerHit blinks to a same-level, wall-free spot away from the player.
public partial class MagoCamino : Enemy
{
	// Plant in place (only turn and cast) instead of moving — per placement.
	[Export] public bool HoldPosition = false;

	// Full attack roster; every Enabled entry is a candidate for SpellBrain.
	[Export] public MagoCaminoAttack[] Attacks = System.Array.Empty<MagoCaminoAttack>();

	// Optional per-placement overrides of the attack resources' own values (0 = use each attack's).
	// e.g. a long-range sniper placement sets ShootRange/ProjectileSpeed high.
	[Export] public float ShootRange = 0f;
	[Export] public float ProjectileSpeed = 0f;
	[Export] public float ShootAnimDuration = 0f;
	// Fallback projectile for an attack entry that has none.
	[Export] public PackedScene ProjectileScene;
	[Export] public float HurtAnimDuration = 0.4f;

	[ExportGroup("Spacing")]
	[Export] public float PreferredMinDistance = 160f;
	[Export] public float PreferredMaxDistance = 300f;
	[Export] public float RetreatOvershoot = 40f;
	[Export] public float RetreatSpeedMultiplier = 1f;

	[ExportGroup("Casting")]
	[Export] public float CastSpacing = 0.8f;
	[Export] public float LeadAccuracy = 0.8f;
	[Export] public float VerticalLeadFactor = 0.3f;
	[Export] public float MaxCastHold = 0.5f;
	[Export] public float PostDashRelease = 0.06f;
	[Export] public Vector2 CastOrigin = new(10f, -10f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -6f);

	[ExportGroup("Blink (DesaparecerHit)")]
	// Blinks away on every hit taken, and also as the escape when cornered.
	[Export] public bool DesaparecerHit = false;
	[Export] public float TeleportMinDistance = 220f;
	[Export] public float TeleportMaxDistance = 380f;
	[Export] public float TeleportVanishDuration = 0.15f;
	[Export] public int TeleportMaxAttempts = 10;
	// Candidate landing ground must be within this many px above/below its current feet.
	[Export] public float TeleportMaxLevelChange = 80f;

	[ExportGroup("Escape hop")]
	[Export] public bool EscapeJumpEnabled = true;
	[Export] public float JumpTriggerRange = 70f;
	[Export] public float JumpCooldown = 2.5f;
	[Export] public float JumpVelocity = -380f;
	[Export] public float JumpAwaySpeed = 200f;
	[Export] public float MaxSafeHopDrop = 40f;

	private readonly SpellBrain _brain = new();
	private readonly DashWatcher _dash = new();
	private readonly RandomNumberGenerator _rng = new();

	private bool _casting;
	private bool _cancelCast;
	private bool _retreating;
	private bool _hopping;
	private bool _isTeleporting;
	private float _hurtTimer;
	private float _castSpacingTimer;
	private float _jumpCooldownTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		_castSpacingTimer = _rng.RandfRange(0.3f, 1f);
		Stats.HitTaken += _ => OnHitTaken();
	}

	private float RangeOf(ISpellDefinition spell) => ShootRange > 0f ? ShootRange : spell.Range;
	private float LongestRange() => Attacks.Where(a => a is not null && a.Enabled).Select(a => RangeOf(a)).DefaultIfEmpty(350f).Max();

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_hurtTimer -= dt;
		_castSpacingTimer -= dt;
		_jumpCooldownTimer -= dt;
		_brain.Tick(dt);

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		if (player is not null)
			TryEscape(player);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;

		if (player is not null)
			TryStartCast(player);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (HoldPosition || _casting || _isTeleporting || _hurtTimer > 0f)
		{
			_retreating = false;
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		}

		float maxDistance = Mathf.Min(PreferredMaxDistance, LongestRange() * 0.9f);
		return KitingMoveX(distanceX, currentVelocityX, PreferredMinDistance, Mathf.Max(PreferredMinDistance + 20f, maxDistance),
			RetreatOvershoot, RetreatSpeedMultiplier, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) =>
		_retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_casting;

	// Rushed (too close, or the player is swinging nearby): hop back if it lands on ground,
	// otherwise blink if it can; else stand and fight.
	private void TryEscape(Metroidvania.Player.Player player)
	{
		if (HoldPosition || _hopping || _casting || _isTeleporting || _jumpCooldownTimer > 0f || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool rushed = absDistance < JumpTriggerRange || (player.IsAttacking && absDistance < JumpTriggerRange * 1.5f);
		if (!rushed)
			return;

		_jumpCooldownTimer = JumpCooldown;
		float away = distanceX == 0f ? (FacingRight ? -1f : 1f) : -Mathf.Sign(distanceX);
		Vector2 hop = new(away * JumpAwaySpeed, JumpVelocity);
		if (EscapeJumpEnabled && LandsSafely(hop, MaxSafeHopDrop))
		{
			Velocity = hop;
			_hopping = true;
			_retreating = false;
		}
		else if (DesaparecerHit)
		{
			TryTeleportAwayFromPlayer();
		}
	}

	// ── Casting ─────────────────────────────────────────────────────────────────────────────

	private void TryStartCast(Metroidvania.Player.Player player)
	{
		if (_casting || _hopping || _isTeleporting || _hurtTimer > 0f || _castSpacingTimer > 0f || !IsOnFloor() || !PlayerDetected)
			return;
		if (player.IsDashing)
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (_retreating && absDistance < PreferredMinDistance)
			return;

		var situation = new SpellSituation(
			absDistance,
			!player.IsOnFloor(),
			PlayerReads.IsClosingIn(player, GlobalPosition.X),
			absDistance < PreferredMinDistance && !CanStepTo(-Mathf.Sign(distanceX)),
			PlayerReads.IsBlockingToward(player, GlobalPosition.X));
		MagoCaminoAttack spell = _brain.Choose(Attacks, situation, RangeOf) as MagoCaminoAttack;
		if (spell is null)
			return;

		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_castSpacingTimer = 0.25f;
			return;
		}
		HoldingAttackSlot = true;
		_ = Cast(spell);
	}

	private async Task Cast(MagoCaminoAttack spell)
	{
		_casting = true;
		_cancelCast = false;
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		float castDuration = ShootAnimDuration > 0f ? ShootAnimDuration : spell.CastDuration;
		Sprite?.Play("attack1");

		if (!await Wait(spell.ReleaseDelay))
			return;

		// Hold the release (animation frozen at the release pose) through a dash/block.
		float held = 0f;
		while (player is not null && held < MaxCastHold && !_dash.GoodMomentToRelease(player, GlobalPosition.X, PostDashRelease))
		{
			Sprite?.Pause();
			if (!await Wait(0f))
				return;
			held += (float)GetPhysicsProcessDeltaTime();
			player = PlayerReads.Find(this);
		}
		Sprite?.Play();

		int count = Mathf.Max(1, spell.ProjectileCount);
		for (int i = 0; i < count; i++)
		{
			player = PlayerReads.Find(this);
			if (player is not null)
				SpawnProjectile(spell, player, count);
			if (i < count - 1 && !await Wait(spell.BurstInterval))
				return;
		}

		if (!await Wait(Mathf.Max(0f, castDuration - spell.ReleaseDelay)))
			return;

		_brain.MarkCast(spell);
		EndCast(CastSpacing * _rng.RandfRange(0.85f, 1.2f));
	}

	// Waits `seconds` (at least one physics frame); false if the cast got cancelled or it died.
	private async Task<bool> Wait(float seconds)
	{
		float elapsed = 0f;
		do
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			if (_cancelCast)
			{
				EndCast(CastSpacing * 0.6f);
				return false;
			}
			elapsed += (float)GetPhysicsProcessDeltaTime();
		} while (elapsed < seconds);
		return true;
	}

	private void EndCast(float spacing)
	{
		_casting = false;
		_cancelCast = false;
		_castSpacingTimer = spacing;
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private void SpawnProjectile(MagoCaminoAttack spell, Metroidvania.Player.Player player, int projectilesInCast)
	{
		PackedScene scene = spell.ProjectileScene ?? ProjectileScene;
		if (scene is null)
			return;

		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? CastOrigin.X : -CastOrigin.X, CastOrigin.Y);
		float speed = ProjectileSpeed > 0f ? ProjectileSpeed : spell.ProjectileSpeed;
		Vector2 direction = SpellBrain.IsHoming(spell)
			? (player.GlobalPosition + PlayerAimOffset - origin).Normalized()
			: PlayerReads.PredictAimDirection(origin, player, speed, LeadAccuracy, VerticalLeadFactor, PlayerAimOffset);

		Projectile projectile = scene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = origin;
		projectile.Speed = speed;
		projectile.Resolved += hit => _brain.Report(hit, projectilesInCast);
		projectile.Launch(direction, Stats);
		Sfx.PlayAt(this, "Magic", "Fireball");
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private void OnHitTaken()
	{
		_hurtTimer = HurtAnimDuration;
		if (_casting)
			_cancelCast = true;
		if (DesaparecerHit)
			TryTeleportAwayFromPlayer();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _casting)
			return;

		string anim;
		if (_hurtTimer > 0f)
			anim = "hit";
		else if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else
			anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";

		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;

	// ── Blink ───────────────────────────────────────────────────────────────────────────────

	private async void TryTeleportAwayFromPlayer()
	{
		if (_isTeleporting)
			return;

		var player = PlayerReads.Find(this);
		if (player is null)
			return;

		float awaySign = Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X);
		if (awaySign == 0f)
			awaySign = _rng.Randf() < 0.5f ? -1f : 1f;

		Vector2? landingSpot = FindValidTeleportSpot(awaySign, player.GlobalPosition);
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
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

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
			if (IsInstanceValid(this))
				_isTeleporting = false;
		}
	}

	// Candidate spots favor the side away from the player (then the near side as a fallback). A
	// spot counts only if: there's ground within TeleportMaxLevelChange of its current feet (same
	// level — no blinking down a pit onto a far-away floor), the body fits there (no wall/ceiling
	// overlap) and it isn't right next to the player.
	private Vector2? FindValidTeleportSpot(float awaySign, Vector2 playerPosition)
	{
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		Godot.Collections.Array<Rid> exclude = new() { GetRid() };
		float feetY = GlobalPosition.Y + BodyBottom;

		for (int attempt = 0; attempt < TeleportMaxAttempts; attempt++)
		{
			float sign = attempt < TeleportMaxAttempts * 0.6f ? awaySign : -awaySign;
			float candidateX = GlobalPosition.X + sign * _rng.RandfRange(TeleportMinDistance, TeleportMaxDistance);

			var query = PhysicsRayQueryParameters2D.Create(
				new Vector2(candidateX, feetY - TeleportMaxLevelChange),
				new Vector2(candidateX, feetY + TeleportMaxLevelChange),
				CollisionMask, exclude);
			var hit = space.IntersectRay(query);
			if (hit.Count == 0 || ((Vector2)hit["normal"]).Y > -0.7f)
				continue;

			Vector2 spot = (Vector2)hit["position"] - new Vector2(0f, BodyBottom + 1f);
			if (Mathf.Abs(spot.X - playerPosition.X) < TeleportMinDistance * 0.6f)
				continue;

			Transform2D there = GlobalTransform with { Origin = spot };
			if (TestMove(there, new Vector2(0f, -1f), null, 0.08f, true))
				continue;

			return spot;
		}

		return null;
	}
}
