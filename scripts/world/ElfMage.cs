using System.Linq;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Elf mage — the caster version of ElfArcher's AI. Fires the same spells as the other casters
// (MagicBolt / HomingBolt, see resources/attacks/elfmage_*.tres) chosen by SpellBrain, with
// predictive aim on straight bolts, release held through the player's dash/block, cast cancelled
// when hit. Where Hechicero/MagoCamino blink away, the elf is agile like the archer: keeps a
// distance band, hops back — or vaults over the player when cornered — only if the hop lands on
// ground. Its own trick is the nova ("spell_2": staff slammed into the ground, shockwave ring):
// the panic button when the player is on top of it, especially when it can't hop out. The nova
// winds up for a few frames (fair tell) and can't be interrupted.
public partial class ElfMage : Enemy
{
	[Export] public bool HoldPosition = false;
	[Export] public ElfMageAttack[] Attacks = System.Array.Empty<ElfMageAttack>();
	[Export] public float HurtDuration = 0.35f;

	[ExportGroup("Spacing")]
	[Export] public float PreferredMinDistance = 170f;
	[Export] public float PreferredMaxDistance = 300f;
	[Export] public float RetreatOvershoot = 45f;
	[Export] public float RetreatSpeedMultiplier = 1.1f;

	[ExportGroup("Casting")]
	[Export] public float CastSpacing = 0.8f;
	[Export] public float LeadAccuracy = 0.85f;
	[Export] public float VerticalLeadFactor = 0.3f;
	[Export] public float MaxCastHold = 0.5f;
	[Export] public float PostDashRelease = 0.06f;
	[Export] public Vector2 PlayerAimOffset = new(0f, -6f);

	[ExportGroup("Evasive hop")]
	[Export] public float HopTriggerDistance = 85f;
	[Export] public float HopCooldown = 2.2f;
	[Export] public float HopSpeedX = 230f;
	[Export] public float HopVelocityY = -320f;
	[Export] public float VaultSpeedX = 300f;
	[Export] public float VaultVelocityY = -420f;
	[Export] public float MaxSafeHopDrop = 40f;

	[ExportGroup("Nova")]
	[Export] public bool NovaEnabled = true;
	[Export] public float NovaRadius = 62f;
	[Export] public float NovaDamageMultiplier = 1.2f;
	[Export] public float NovaKnockback = 380f;
	[Export] public float NovaCooldown = 5f;
	// spell_2 frame where the staff hits the ground (ring starts); 0-4 are the wind-up tell.
	[Export] public int NovaHitFrame = 5;
	// Chance to prefer the nova over hopping when rushed but not cornered.
	[Export] public float NovaOverHopChance = 0.45f;

	private readonly SpellBrain _brain = new();
	private readonly DashWatcher _dash = new();
	private readonly RandomNumberGenerator _rng = new();

	private bool _casting;
	private bool _cancelCast;
	private bool _retreating;
	private bool _hopping;
	private bool _wantsEvade;
	private float _hurtTimer;
	private float _castSpacingTimer;
	private float _hopCooldownTimer;
	private float _novaCooldownTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		_castSpacingTimer = _rng.RandfRange(0.3f, 1f);
		Stats.HitTaken += _ => OnHurt();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_casting)
			_cancelCast = true;
		_wantsEvade = true;
	}

	private float LongestRange() => Attacks.Where(a => a is not null && a.Enabled).Select(a => a.Range).DefaultIfEmpty(350f).Max();

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_hurtTimer -= dt;
		_castSpacingTimer -= dt;
		_hopCooldownTimer -= dt;
		_novaCooldownTimer -= dt;
		_brain.Tick(dt);

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		if (player is not null)
			TryEvade(player);

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
		if (HoldPosition || _casting || _hurtTimer > 0f)
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

	// Rushed: nova (always when cornered, sometimes by choice), else hop back, else vault over the
	// player — each hop only if it lands on ground. HoldPosition skips the hops but keeps the nova.
	private void TryEvade(Metroidvania.Player.Player player)
	{
		if (_hopping || _casting || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool playerSwingingAtMe = player.IsAttacking && absDistance < PreferredMinDistance * 0.6f;
		bool evadeAfterHit = _wantsEvade && absDistance < PreferredMinDistance;
		_wantsEvade = false;
		if (absDistance > HopTriggerDistance && !playerSwingingAtMe && !evadeAfterHit)
			return;

		float away = distanceX == 0f ? (FacingRight ? -1f : 1f) : -Mathf.Sign(distanceX);
		bool novaReady = NovaEnabled && _novaCooldownTimer <= 0f && absDistance <= NovaRadius * 1.6f;
		bool hopReady = !HoldPosition && _hopCooldownTimer <= 0f;

		Vector2 backHop = new(away * HopSpeedX, HopVelocityY);
		Vector2 vault = new(-away * VaultSpeedX, VaultVelocityY);
		bool canBackHop = hopReady && LandsSafely(backHop, MaxSafeHopDrop);
		bool cornered = !canBackHop;

		if (novaReady && (cornered || _rng.Randf() < NovaOverHopChance))
		{
			_ = CastNova();
			return;
		}
		if (!hopReady)
			return;

		if (canBackHop)
			StartHop(backHop);
		else if (LandsSafely(vault, MaxSafeHopDrop))
			StartHop(vault);
		else
			_hopCooldownTimer = 0.4f;
	}

	private void StartHop(Vector2 velocity)
	{
		Velocity = velocity;
		_hopping = true;
		_retreating = false;
		_hopCooldownTimer = HopCooldown;
	}

	// ── Spells ──────────────────────────────────────────────────────────────────────────────

	private void TryStartCast(Metroidvania.Player.Player player)
	{
		if (_casting || _hopping || _hurtTimer > 0f || _castSpacingTimer > 0f || !IsOnFloor() || !PlayerDetected)
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
		ElfMageAttack spell = _brain.Choose(Attacks, situation, s => s.Range) as ElfMageAttack;
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

	private async Task Cast(ElfMageAttack spell)
	{
		_casting = true;
		_cancelCast = false;
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		Sprite?.Play(spell.Animation);
		if (!await Wait(spell.ReleaseDelay))
			return;

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

		if (!await Wait(Mathf.Max(0f, spell.CastDuration - spell.ReleaseDelay)))
			return;

		_brain.MarkCast(spell);
		EndCast(CastSpacing * _rng.RandfRange(0.85f, 1.2f));
	}

	private async Task CastNova()
	{
		_casting = true;
		_novaCooldownTimer = NovaCooldown;
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		Sprite?.Play("spell_2");
		float fps = Sprite is null ? 12f : (float)Sprite.SpriteFrames.GetAnimationSpeed("spell_2");
		await ToSignal(GetTree().CreateTimer(NovaHitFrame / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		player = PlayerReads.Find(this);
		Vector2 center = GlobalPosition + new Vector2(0f, BodyBottom * 0.5f);
		if (player is not null && player.GlobalPosition.DistanceTo(center) <= NovaRadius)
		{
			Stats targetStats = player.GetNodeOrNull<Stats>("Stats");
			if (targetStats is not null && !targetStats.IsInvulnerable)
			{
				targetStats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * NovaDamageMultiplier)));
				Vector2 push = (player.GlobalPosition - center).Normalized();
				if (push == Vector2.Zero)
					push = FacingRight ? Vector2.Right : Vector2.Left;
				player.ApplyKnockback(new Vector2(push.X, -0.35f).Normalized(), NovaKnockback);
			}
		}

		int framesLeft = Sprite is null ? 0 : Sprite.SpriteFrames.GetFrameCount("spell_2") - NovaHitFrame;
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, framesLeft) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_casting = false;
		_castSpacingTimer = Mathf.Max(_castSpacingTimer, CastSpacing * 0.5f);
	}

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

	private void SpawnProjectile(ElfMageAttack spell, Metroidvania.Player.Player player, int projectilesInCast)
	{
		if (spell.ProjectileScene is null)
			return;

		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? spell.SpawnOffset.X : -spell.SpawnOffset.X, spell.SpawnOffset.Y);
		Vector2 direction = SpellBrain.IsHoming(spell)
			? (player.GlobalPosition + PlayerAimOffset - origin).Normalized()
			: PlayerReads.PredictAimDirection(origin, player, spell.ProjectileSpeed, LeadAccuracy, VerticalLeadFactor, PlayerAimOffset);

		Projectile projectile = spell.ProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = origin;
		projectile.Speed = spell.ProjectileSpeed;
		projectile.Resolved += hit => _brain.Report(hit, projectilesInCast);
		projectile.Launch(direction, Stats);
		Sfx.PlayAt(this, "Magic", "Fireball");
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
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
}
