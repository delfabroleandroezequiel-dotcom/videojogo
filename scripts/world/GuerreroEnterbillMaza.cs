using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Soldado de maza de Enterbill — bulky plate, flanged mace and buckler. Slow and heavy, he doesn't
// flinch while swinging (HyperArmorWhileAttacking in the scene).
// - Smash: the mace goes up with a long flash and comes down into the floor in front; the impact
//   throws a ring of shock both ways along the ground (close range all around him is unsafe).
// - Buckler punch: when you whiff a swing next to him, or crowd him right after he got hit, he
//   answers with a quick shield punch that knocks you back out of your combo. Short tell — it's
//   the "get off me" button, weak but fast.
public partial class GuerreroEnterbillMaza : GuerreroReino
{
	[Export] public float SmashWindup = 0.7f;
	[Export] public float PunchWindup = 0.18f;
	[Export] public float PunchRange = 48f;
	[Export] public float PunchKnockback = 380f;

	// attack: 2-3 mace raised, 4 into the floor, 5-8 back up. attack2: 2-3 drawn back, 4-5 punch.
	[Export] public int SmashWindupFrame = 3;
	[Export] public int SmashFrame = 4;
	[Export] public int PunchWindupFrame = 2;
	[Export] public int PunchFrame = 4;

	private ulong _lastHitMsec;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		Stats.HitTaken += _ => _lastHitMsec = Time.GetTicksMsec();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || Attacking || Target is null)
			return;
		bool crowded = TargetDistanceX < PunchRange && Time.GetTicksMsec() - _lastHitMsec < 500;
		if (TargetDistanceX < PunchRange && (Whiff.JustWhiffed || crowded))
			TryAttackNow(0.6f);
	}

	protected override async Task<bool> Kit()
	{
		bool punch = TargetDistanceX < PunchRange && (Whiff.JustWhiffed || Time.GetTicksMsec() - _lastHitMsec < 600);
		return punch ? await Punch() : await Smash();
	}

	private async Task<bool> Smash()
	{
		WindupScale = 1f;
		if (!await Strike("attack", 1, SmashWindupFrame - 1, 99, 0f, Vector2.Zero, fps: 10f))
			return false;
		if (!await Telegraph("attack", SmashWindupFrame, SmashWindup))
			return false;
		MaybeShout();
		if (!await Strike("attack", SmashFrame, SmashFrame, SmashFrame, 34f, new Vector2(46f, 60f), damage: 1.4f, activeFrames: 1,
			trail: new TrailArc(new Vector2(-2f, -6f), 48f, -110f, 50f, 0.09f, 42f)))
			return false;
		Vector2 ground = GroundPoint(34f);
		GroundSlamFx.Impact(this, ground, 1f);
		Shake(5f, 0.2f);
		// Shock ring along the floor, both sides, for one frame.
		HitOn(0f, new Vector2(150f, 16f), BodyBottom - 8f, 0.6f);
		if (!await Wait(1f / AttackFps))
			return false;
		AttackHitbox.Deactivate();
		ExtraCooldown = 0.5f;
		return await Strike("attack", SmashFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 9f);
	}

	private async Task<bool> Punch()
	{
		WindupScale = 1f;
		if (!await Telegraph("attack2", PunchWindupFrame, PunchWindup))
			return false;
		float oldForce = AttackHitbox.KnockbackForce;
		AttackHitbox.KnockbackForce = PunchKnockback;
		Lunge(160f, 0.08f);
		bool ok = await Strike("attack2", PunchFrame - 1, 8, PunchFrame, 26f, new Vector2(34f, 30f), y: -6f, damage: 0.5f);
		AttackHitbox.KnockbackForce = oldForce;
		ExtraCooldown = -0.3f;
		return ok;
	}
}
