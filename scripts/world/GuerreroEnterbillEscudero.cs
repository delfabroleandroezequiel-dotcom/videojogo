using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Escudero de Enterbill — tower shield + short sword. The wall of the royal line.
// - From the front the shield eats the hit: no knockback, no flinch, a spark (go around it, or
//   make it swing first). Hits from behind land normally.
// - It answers what you do with the parry: if your parry is up toward it, it shoves the shield
//   into you instead of swinging (a slow bash that eats the parry window); if you whiff a parry,
//   it slashes at once.
// - Otherwise a held, flashing wind-up and one wide slash with a short step.
public partial class GuerreroEnterbillEscudero : GuerreroReino
{
	[Export] public float SlashWindup = 0.45f;
	[Export] public float PunishWindup = 0.12f;
	[Export] public float BashWindup = 0.35f;
	[Export] public float BashKnockback = 420f;

	// attack strip: 1-2 sword drawn back, 3 slash smear, 4-5 follow-through, 6-8 back to guard.
	[Export] public int WindupFrame = 2;
	[Export] public int SlashFrame = 3;
	[Export] public int BashFrame = 1;

	protected override async Task<bool> Kit()
	{
		var player = Target;
		if (player is not null && PlayerReads.WhiffedParryToward(player, GlobalPosition.X))
			return await Slash(PunishWindup);
		if (player is not null && PlayerReads.IsBlockingToward(player, GlobalPosition.X))
			return await ShieldBash();
		return await Slash(SlashWindup * WindupScale);
	}

	private async Task<bool> Slash(float windup)
	{
		WindupScale = 1f;
		if (!await Telegraph("attack", WindupFrame, windup))
			return false;
		MaybeShout();
		Lunge(150f, 0.1f);
		return await Strike("attack", SlashFrame, 8, SlashFrame, 40f, new Vector2(56f, 36f), y: -4f,
			trail: new TrailArc(new Vector2(4f, -6f), 52f, -70f, 40f, 0.1f, 34f));
	}

	// Slow, obvious shove: it doesn't hurt much but throws you back and breaks your rhythm.
	private async Task<bool> ShieldBash()
	{
		if (!await Telegraph("attack", 0, BashWindup))
			return false;
		Lunge(240f, 0.14f);
		float oldForce = AttackHitbox.KnockbackForce;
		AttackHitbox.KnockbackForce = BashKnockback;
		bool ok = await Strike("attack", BashFrame, BashFrame + 1, BashFrame, 26f, new Vector2(30f, 56f), damage: 0.4f, activeFrames: 2, fps: 8f);
		AttackHitbox.KnockbackForce = oldForce;
		Shake(2f, 0.12f);
		ExtraCooldown = 0.2f;
		return ok;
	}

	private bool HitFromFront => Target is not null && TargetInFront && !Attacking;

	protected override bool ShieldUp => HitFromFront;

	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (Mathf.Sign(direction.X) == -Forward)
			return;
		base.ApplyKnockback(direction, force);
	}

	protected override void OnPoiseHit()
	{
		if (HitFromFront)
		{
			HitSparks.SpawnAt(this, GlobalPosition + new Vector2(Forward * 14f, -6f), new Vector2(Forward, -0.3f));
			return;
		}
		base.OnPoiseHit();
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		Attacking ? Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed) : SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f, 0.8f);
}
