using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Guerrero de las cimitarras de Radem — two curved blades, desert speed. Radem fights by pressure,
// and he's the purest form of it:
// - Flurry: a short tell, then he dashes in and lands four alternating cuts while still moving
//   forward (attack, 12 frames). Each cut is light; the danger is staying in front of it. The
//   flurry is parryable cut by cut — and a parry on the 2nd cut breaks it off.
// - Reads your evade: he starts the flurry the moment your dash/roll ENDS near him (not during
//   it, he isn't a cheat).
// - After a flurry he springs back out of reach (hop) instead of standing there to be punished,
//   unless he's cornered.
public partial class GuerreroRademCimitarras : GuerreroReino
{
	[Export] public float FlurryWindup = 0.32f;
	[Export] public float DashEndPunishWindow = 0.15f;
	[Export] public float PunishRange = 120f;
	[Export] public float HopBackSpeed = 230f;

	// attack (12 frames + input): 3-4 first cut, 7 high back-cut, 8 third, 10 fourth.
	[Export] public int WindupFrame = 2;
	[Export] public int[] CutFrames = { 3, 7, 8, 10 };

	private bool _parriedDuringFlurry;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || Attacking || Target is null)
			return;
		if (Dash.SinceDashEnded < DashEndPunishWindow && TargetDistanceX < PunishRange)
			TryAttackNow(0.5f);
	}

	protected override async Task<bool> Kit()
	{
		float windup = FlurryWindup * WindupScale;
		WindupScale = 1f;
		if (!await Telegraph("attack", WindupFrame, windup))
			return false;
		MaybeShout();
		_parriedDuringFlurry = false;
		Lunge(170f, 0.55f);
		int cut = 0;
		for (int f = WindupFrame + 1; f <= 12; f++)
		{
			Pose("attack", f);
			if (System.Array.IndexOf(CutFrames, f) >= 0)
			{
				bool high = cut == 1;
				Trail.Play(high ? new Vector2(0f, -14f) : new Vector2(4f, -4f), 44f, high ? -150f : -60f, high ? -30f : 45f, 0.07f, 30f);
				HitOn(high ? 20f : 34f, high ? new Vector2(50f, 40f) : new Vector2(48f, 34f), high ? -24f : -4f, 0.6f);
				cut++;
			}
			else
			{
				AttackHitbox.Deactivate();
			}
			if (!await Wait(1f / 18f))
				return false;
			if (cut == 2 && Target is { } p && PlayerReads.IsBlockingToward(p, GlobalPosition.X))
			{
				_parriedDuringFlurry = true;
				break;
			}
		}
		AttackHitbox.Deactivate();
		StopLunge();
		if (!_parriedDuringFlurry && TargetDistanceX < 60f)
		{
			float away = -Mathf.Sign(Target.GlobalPosition.X - GlobalPosition.X);
			TryAttackHop(new Vector2(away * HopBackSpeed, -280f));
		}
		ExtraCooldown = _parriedDuringFlurry ? 0.6f : 0f;
		return true;
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f, 1.1f);
}
