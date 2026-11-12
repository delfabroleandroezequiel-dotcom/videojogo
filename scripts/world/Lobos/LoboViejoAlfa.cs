using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Viejo Alfa — the old pack leader, grey muzzle and scars. When it has wolves around it bares its
// teeth and growls (a long, exposed tell): every wolf nearby, itself included, attacks and moves
// faster for a while (red pulse on each). Its own bite comes in twos: a second snap right after the
// first if the player is still in reach. Kill it and the pack loses its nerve: every wolf nearby
// backs off for a moment — the reason to go for the alfa first.
public partial class LoboViejoAlfa : LoboBase
{
	[ExportGroup("Gruñido")]
	[Export] public float GrowlRadius = 550f;
	[Export] public float GrowlCooldown = 12f;
	[Export] public float GrowlHasteSeconds = 8f;
	[Export] public float GrowlCooldownMul = 0.7f;
	[Export] public float GrowlSpeedMul = 1.25f;
	[Export] public Color GrowlColor = new(1.6f, 0.55f, 0.45f, 1f);
	[Export] public float ScareSeconds = 2.5f;

	[ExportGroup("IA")]
	[Export] public float PounceChance = 0.3f;
	[Export] public float SecondBiteWindupScale = 0.45f;

	private ulong _nextGrowlMsec;

	protected override async Task<bool> Kit()
	{
		ulong now = Time.GetTicksMsec();
		if (now >= _nextGrowlMsec && CountPackMates(GrowlRadius) > 0 && TargetDistanceX > BiteRange)
		{
			_nextGrowlMsec = now + (ulong)(GrowlCooldown * 1000f);
			return await Growl();
		}

		float d = TargetDistanceX;
		if (d > BiteRange && d >= 90f && Roll() < PounceChance)
			return await Pounce(1.1f);
		if (d > BiteRange && !await RushIn(BiteRange * 0.8f, 0.9f))
			return StillGoing;
		if (!await Bite())
			return false;
		// Second snap if he's still there.
		if (TargetDistanceX <= BiteRange + 15f)
			return await Bite(SecondBiteWindupScale);
		return true;
	}

	private async Task<bool> Growl()
	{
		TurnToTarget();
		if (!await PlayFrames("growl", 0, 1, 10f))
			return false;
		Bark("Gruñido", 1.6f);
		return await PlayFrames("growl", 2, 8, 8f, f =>
		{
			if (f == 3)
			{
				Shake(2.5f, 0.4f);
				SpawnRing(new Vector2(BiteReach * 0.6f, BiteY), GrowlColor, 80f, 0.6f);
				Hasten(GrowlCooldownMul, GrowlSpeedMul, GrowlHasteSeconds, GrowlColor);
				foreach (LoboBase mate in PackMates(GrowlRadius))
					mate.Hasten(GrowlCooldownMul, GrowlSpeedMul, GrowlHasteSeconds, GrowlColor);
			}
		});
	}

	protected override void OnDefeated()
	{
		foreach (LoboBase mate in PackMates(GrowlRadius))
			mate.Scare(ScareSeconds);
		base.OnDefeated();
	}
}
