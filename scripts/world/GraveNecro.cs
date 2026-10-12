using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto sepulturero / Grave Necro (KBPixelArt Skeleton Enemy Pack, 128x128 cells, faces right).
// The one grave skeleton that isn't buried: it keeps the graveyard full, raising tombstones under
// your feet that each bring up a fresh swordsman.
// Sheet beats used:
//   summon (19, "TombStone_Attack"): 1-9 the staff swept low across the ground, glowing green, 10-14
//     held up as the stone erupts, 15-18 lowered.
//   attack (14): 1-5 staff drawn back, 6 a short swipe (white arc), 7-13 recovery.
//   idle / walk / hurt / death. Tombstone: its own sheet (GraveTombstone).
// Moves:
//   Raise the dead — a green glow spreads under you (move!), a tombstone bursts up out of it, and
//     if fewer than MaxMinions of its swordsmen are up, one climbs out beside the stone.
//   Staff swipe — a quick close-range swipe when you get on top of it, then it backs off.
//   Spacing — keeps MinDistance..MaxDistance away, never off a ledge.
//   Its swordsmen crumble when it dies.
// Telegraphs: green blinks while casting; the floor glow under you; white flash on the swipe wind-up.
public partial class GraveNecro : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 120f;
	[Export] public float MaxDistance = 240f;

	[ExportGroup("Raise the dead")]
	[Export] public PackedScene MinionScene;
	[Export] public int MaxMinions = 2;
	[Export] public int MinionHealth = 20;
	[Export] public float SummonRange = 320f;
	[Export] public float SummonCooldown = 4.5f;
	[Export] public float CastTime = 0.8f;
	[Export] public float TombWarningTime = 0.75f;
	[Export] public float TombDamageMultiplier = 1.2f;

	[ExportGroup("Staff swipe")]
	[Export] public float SwipeTrigger = 55f;
	[Export] public float SwipeWindup = 0.3f;
	[Export] public float SwipeCooldown = 1.6f;
	[Export] public float SwipeDamageMultiplier = 0.9f;
	// Frame 6: the white arc reaches ~60px in front at chest height.
	[Export] public Vector2 SwipeHitboxCenter = new(34f, -4f);
	[Export] public Vector2 SwipeHitboxSize = new(52f, 40f);

	[ExportGroup("Look")]
	[Export] public Color CastTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color SwipeTint = new(2.2f, 2.2f, 2.2f, 1f);

	private static readonly RandomNumberGenerator Rng = new();
	private readonly List<Enemy> _minions = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _summonCooldownTimer;
	private float _swipeCooldownTimer;

	static GraveNecro()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_summonCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_summonCooldownTimer -= dt;
		_swipeCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDx <= SwipeTrigger && _swipeCooldownTimer <= 0f)
			_ = StaffSwipe();
		else if (_summonCooldownTimer <= 0f && GlobalPosition.DistanceTo(player.GlobalPosition) <= SummonRange)
			_ = RaiseTheDead(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 30f, 1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Raise the dead ───────────────────────────────────────────────────────────────────────────

	private async Task RaiseTheDead(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_summonCooldownTimer = SummonCooldown * Rng.RandfRange(0.85f, 1.2f);
		try
		{
			Sprite.Play("summon");
			Sprite.Pause();
			FlashBody(CastTint, CastTime);
			for (int f = 0; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(CastTime / 10f))
					return;
			}
			ClearFlash();
			if (IsInstanceValid(player))
				PlaceTombstone(player.GlobalPosition.X);
			for (int f = 10; f <= 18; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.07f))
					return;
			}
		}
		finally
		{
			Finish();
		}
	}

	// On the floor under x (searched from a bit above the player's feet); nothing if there's no floor.
	private void PlaceTombstone(float x)
	{
		if (GetTree().CurrentScene is not Node scene || PlayerReads.Find(this) is not { } player)
			return;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, player.GlobalPosition.Y - 10f), new Vector2(x, player.GlobalPosition.Y + 140f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return;
		_minions.RemoveAll(m => !IsInstanceValid(m) || m.Stats is null || m.Stats.CurrentHealth <= 0);
		var tomb = new GraveTombstone
		{
			WarningTime = TombWarningTime,
			DamageMultiplier = TombDamageMultiplier,
			CasterStats = Stats,
			MinionScene = _minions.Count < MaxMinions ? MinionScene : null,
			MinionHealth = MinionHealth,
			MinionSpawned = minion => _minions.Add(minion),
		};
		scene.AddChild(tomb);
		tomb.GlobalPosition = (Vector2)hit["position"];
	}

	// ── Staff swipe ──────────────────────────────────────────────────────────────────────────────

	private async Task StaffSwipe()
	{
		if (!Begin())
			return;
		_swipeCooldownTimer = SwipeCooldown;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(SwipeTint, SwipeWindup);
			for (int f = 0; f <= 5; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(SwipeWindup / 6f))
					return;
			}
			ClearFlash();
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			Sprite.Frame = 6;
			Strike();
			for (int f = 7; f <= 13; f++)
			{
				if (!await Wait(0.06f))
					return;
				Sprite.Frame = f;
			}
		}
		finally
		{
			Finish();
		}
	}

	private void Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? SwipeHitboxCenter.X : -SwipeHitboxCenter.X, SwipeHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = SwipeHitboxSize },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * SwipeDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(), 260f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return;
		}
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_summonCooldownTimer = Mathf.Max(_summonCooldownTimer, 0.3f);
			_swipeCooldownTimer = Mathf.Max(_swipeCooldownTimer, 0.3f);
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	private void FlashBody(Color tint, float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float half = duration / 4f;
		for (int i = 0; i < 2; i++)
		{
			_flashTween.TweenProperty(Sprite, "self_modulate", tint, half);
			_flashTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	// Its swordsmen crumble with it.
	protected override void OnDefeated()
	{
		ClearFlash();
		foreach (var minion in _minions)
		{
			if (IsInstanceValid(minion) && minion.Stats is { CurrentHealth: > 0 } stats)
				stats.Kill();
		}
		_minions.Clear();
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
