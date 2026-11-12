using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Rojizo — hit and run. Small, fast and fragile: it darts in, bites with a short wind-up and
// always hops straight back out of reach (its own "hopback" clip), so trading hits with it doesn't
// work — you catch it on the way in (parry the bite) or chase it down on the landing. It reads the
// player too: a whiffed swing within PunishRange brings it darting back in at once.
public partial class LoboRojizo : LoboBase
{
	[ExportGroup("Golpe y fuga")]
	[Export] public float PunishRange = 190f;
	[Export] public float PunishWindupScale = 0.4f;
	[Export] public Vector2 HopBackVelocity = new(230f, -250f);
	[Export] public float PounceChance = 0.25f;

	protected override void Think(float dt)
	{
		if (CanAttack && Whiff.JustWhiffed && TargetDistanceX <= PunishRange)
		{
			Whiff.Consume();
			TryAttackNow(PunishWindupScale);
		}
	}

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		bool bit;
		if (d > BiteRange && d >= 90f && KitWindupScale >= 1f && Roll() < PounceChance)
		{
			bit = await Pounce();
		}
		else
		{
			if (d > BiteRange && !await RushIn(BiteRange * 0.8f, 0.8f))
				return StillGoing;
			bit = await Bite(0.75f);
		}
		if (!bit)
			return false;
		return await HopOut();
	}

	private async Task<bool> HopOut()
	{
		TurnToTarget();
		if (!TryAttackHop(new Vector2(-Forward * HopBackVelocity.X, HopBackVelocity.Y)))
			return true;
		int frames = Sprite.SpriteFrames.GetFrameCount("hopback");
		float airTime = 2f * -HopBackVelocity.Y / Mathf.Max(1f, Gravity);
		float elapsed = 0f;
		while (Hopping && elapsed < airTime + 0.4f)
		{
			if (!await WaitPhysicsFrame())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			int f = Mathf.Clamp((int)(elapsed / airTime * (frames - 1)), 0, frames - 1);
			if (Sprite.Animation != "hopback" || Sprite.Frame != f)
				Pose("hopback", f);
		}
		return true;
	}
}
