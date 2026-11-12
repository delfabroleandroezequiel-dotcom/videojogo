using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Gris — the pack's caller. The first time it spots the player it sits back and howls: every
// wolf within HowlRadius is alerted from afar and hastened (faster attacks and footwork) for a
// while. It howls again whenever the pack is around and the howl is off cooldown. The howl is a
// long, exposed animation: the window to punish it before the pack arrives.
// Otherwise a plain, honest hunter: bites up close, rushes in, sometimes pounces from mid range.
public partial class LoboGris : LoboBase
{
	[ExportGroup("Aullido")]
	[Export] public float HowlRadius = 650f;
	[Export] public float HowlCooldown = 14f;
	[Export] public float HowlHasteSeconds = 7f;
	[Export] public float HowlCooldownMul = 0.6f;
	[Export] public float HowlSpeedMul = 1.15f;
	[Export] public Color HowlColor = new(0.75f, 0.85f, 1f, 0.55f);

	[ExportGroup("IA")]
	[Export] public float PounceChance = 0.35f;
	[Export] public float PounceMinDistance = 90f;

	private bool _wantHowl;
	private ulong _nextHowlMsec;

	protected override void OnSpottedPlayer() => _wantHowl = true;

	protected override async Task<bool> Kit()
	{
		ulong now = Time.GetTicksMsec();
		if (now >= _nextHowlMsec && (_wantHowl || CountPackMates(HowlRadius) > 0) && TargetDistanceX > BiteRange)
		{
			_wantHowl = false;
			_nextHowlMsec = now + (ulong)(HowlCooldown * 1000f);
			return await Howl();
		}

		if (TargetDistanceX <= BiteRange)
			return await Bite();
		if (TargetDistanceX >= PounceMinDistance && Roll() < PounceChance && Target is { } p && p.IsOnFloor())
			return await Pounce();
		if (!await RushIn(BiteRange * 0.8f, 0.9f))
			return StillGoing;
		return await Bite(0.8f);
	}

	private async Task<bool> Howl()
	{
		TurnToTarget();
		// Sit down (0-2), head up and howl (3-10), get back up (11-12).
		if (!await PlayFrames("howl", 0, 2, 10f))
			return false;
		Bark("Aullido", 2.4f);
		Vector2 head = new(BiteReach * 0.5f, BiteY - 14f);
		for (int f = 3; f <= 10; f++)
		{
			Pose("howl", f);
			if (f == 4)
			{
				SpawnRing(head, HowlColor, 90f, 0.7f);
				Hasten(HowlCooldownMul, HowlSpeedMul, HowlHasteSeconds, HowlColor);
				foreach (LoboBase mate in PackMates(HowlRadius))
					mate.Hasten(HowlCooldownMul, HowlSpeedMul, HowlHasteSeconds, HowlColor);
			}
			else if (f == 7)
			{
				SpawnRing(head, HowlColor, 70f, 0.6f);
			}
			if (!await Wait(0.12f))
				return false;
		}
		return await PlayFrames("howl", 11, 12, 10f);
	}
}
