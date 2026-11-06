using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Soldado del khopesh de Radem — round shield and hooked sickle-sword.
// - Hook: a long low reach with the khopesh. If it catches you, he YANKS you in toward him and
//   follows straight into a downward diagonal cut (attack2). Getting hooked is the mistake, the
//   cut is the price.
// - Shield: from the front the shield takes the hit (no knockback, no flinch). Unlike Enterbill's
//   shield wall he answers a blocked hit at once with the cut — Radem counters, it doesn't wait.
// - When you're not in hook range he comes forward at a steady walk behind the shield.
public partial class GuerreroRademKhopesh : GuerreroReino
{
	[Export] public float HookWindup = 0.4f;
	[Export] public float CutWindup = 0.32f;
	[Export] public float PullForce = 330f;
	[Export] public float CounterChance = 0.6f;

	// attack: 2 cocked low, 3-6 hook extended, 7 yanked back. attack2: 1-2 raised, 3 diagonal smear.
	[Export] public int HookWindupFrame = 2;
	[Export] public int HookFrame = 3;
	[Export] public int CutWindupFrame = 2;
	[Export] public int CutFrame = 3;

	private bool _hooked;
	private bool _counterQueued;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		AttackHitbox.HitTarget += OnHitTarget;
	}

	private void OnHitTarget(Node2D body)
	{
		if (!_hookPhase || body is not Metroidvania.Player.Player player)
			return;
		_hooked = true;
		// After the hitbox's own knockback, pull the other way.
		Callable.From(() =>
		{
			if (IsInstanceValid(player) && IsInstanceValid(this))
				player.ApplyKnockback(new Vector2(-Forward, -0.15f).Normalized(), PullForce);
		}).CallDeferred();
	}

	private bool _hookPhase;

	protected override async Task<bool> Kit()
	{
		if (_counterQueued)
		{
			_counterQueued = false;
			return await Cut(0.12f);
		}
		float windup = HookWindup * WindupScale;
		WindupScale = 1f;
		if (!await Telegraph("attack", HookWindupFrame, windup))
			return false;
		MaybeShout();
		_hooked = false;
		_hookPhase = true;
		try
		{
			if (!await Strike("attack", HookFrame, 7, HookFrame, 40f, new Vector2(52f, 16f), y: 14f, activeFrames: 3, damage: 0.5f,
				trail: new TrailArc(new Vector2(-6f, 12f), 62f, 20f, -6f, 0.1f, 14f)))
				return false;
		}
		finally
		{
			_hookPhase = false;
		}
		if (_hooked)
			return await Cut(0.08f);
		return await Strike("attack", 8, 8, 99, 0f, Vector2.Zero);
	}

	private async Task<bool> Cut(float windup)
	{
		TurnToTarget();
		if (!await Telegraph("attack2", CutWindupFrame, windup))
			return false;
		Bark("Ataque", 0.8f);
		ExtraCooldown = 0.25f;
		return await Strike("attack2", CutFrame, 8, CutFrame, 34f, new Vector2(52f, 56f), y: -10f, damage: 1.2f,
			trail: new TrailArc(new Vector2(2f, -10f), 52f, -110f, 50f, 0.09f, 44f));
	}

	protected override bool ShieldUp => !Attacking && TargetInFront;

	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (Mathf.Sign(direction.X) == -Forward && !Attacking)
			return;
		base.ApplyKnockback(direction, force);
	}

	protected override void OnPoiseHit()
	{
		if (!Attacking && TargetInFront)
		{
			HitSparks.SpawnAt(this, GlobalPosition + new Vector2(Forward * 12f, -4f), new Vector2(Forward, -0.3f));
			if (Roll() < CounterChance && CanAttack)
			{
				_counterQueued = true;
				TryAttackNow(0.5f);
			}
			return;
		}
		base.OnPoiseHit();
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		Attacking ? Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed) : SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f, 0.85f);
}
