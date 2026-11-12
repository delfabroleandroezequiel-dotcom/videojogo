using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo de las Nieves — frost breath. From mid range it rears back with a long, visible inhale
// (flashing ice-white, white mist curling at the mouth) and then breathes a cone of frost in front:
// a hit freezes the player's feet for FrostSnare seconds (no walking, jumping, dashing or rolling),
// and its own bite comes right after. Answer: get behind it or out of the cone during the inhale —
// the breath only covers what's in front. Up close it just bites.
public partial class LoboNieve : LoboBase
{
	[ExportGroup("Aliento")]
	[Export] public float BreathMinDistance = 55f;
	[Export] public float BreathMaxDistance = 170f;
	[Export] public float BreathCooldown = 4.5f;
	[Export] public float BreathInhale = 0.55f;
	[Export] public float BreathReach = 70f;
	[Export] public Vector2 BreathSize = new(115f, 34f);
	[Export] public float BreathDamage = 0.6f;
	[Export] public float FrostSnare = 0.55f;
	[Export] public Color FrostColor = new(0.6f, 0.9f, 1f, 1f);

	private ulong _nextBreathMsec;

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextBreathMsec && d >= BreathMinDistance && d <= BreathMaxDistance)
		{
			_nextBreathMsec = Time.GetTicksMsec() + (ulong)(BreathCooldown * 1000f);
			return await Breath();
		}
		if (d <= BiteRange)
			return await Bite();
		if (!await RushIn(BiteRange * 0.8f, 0.8f))
			return StillGoing;
		return await Bite(0.85f);
	}

	private async Task<bool> Breath()
	{
		TurnToTarget();
		if (!await PlayFrames("breath", 0, 1, 10f))
			return false;
		// Inhale: held on frame 2, flashing frost-white, mist gathering at the mouth.
		Bark("Gruñido", BreathInhale + 0.2f);
		Pose("breath", 2);
		FlashBody(BreathInhale);
		float elapsed = 0f;
		while (elapsed < BreathInhale)
		{
			SpawnPuffs(Visual.ToGlobal(MouthLocal + new Vector2(10f, 0f)), 2, new Vector2(-Forward, -0.3f), 50f, 15f, 35f, FrostColor, 0.35f, 0.4f);
			if (!await Wait(0.08f))
				return false;
			elapsed += 0.08f;
		}
		ClearFlash();

		// Breath: frames 3-8, cone live from frame 4 to 7.
		Shake(2f, 0.3f);
		bool ok = await PlayFrames("breath", 3, 8, 10f, f =>
		{
			Vector2 mouth = Visual.ToGlobal(MouthLocal);
			SpawnPuffs(mouth, 10, new Vector2(Forward, 0.05f), 14f, 140f, 220f, FrostColor, 0.6f, 0.5f);
			SpawnPuffs(mouth, 4, new Vector2(Forward, 0f), 8f, 180f, 240f, Colors.White, 0.3f, 0.35f);
			if (f == 4)
				HitOn(BreathReach, BreathSize, BiteY, BreathDamage, _ => Freeze());
			else if (f == 8)
				AttackHitbox.Deactivate();
		});
		AttackHitbox.Deactivate();
		if (!ok || !await PlayFrames("breath", 9, 12, 12f))
			return false;

		// Follow-up bite on a frozen player in reach.
		if (Target is { IsSnared: true } && TargetDistanceX <= BiteRange + 30f)
		{
			if (!await RushIn(BiteRange * 0.7f, 0.5f))
				return StillGoing;
			return await Bite(0.6f);
		}
		return true;
	}

	private void Freeze()
	{
		if (Target is not { } player)
			return;
		player.Snare(FrostSnare);
		// Frost crusting on his feet for as long as they're stuck.
		SpawnPuffs(player.GlobalPosition + new Vector2(0f, 24f), 14, Vector2.Up, 70f, 10f, 40f, FrostColor, 0.45f, FrostSnare + 0.2f);
		SpawnGlow(player.GlobalPosition + new Vector2(0f, 26f), FrostColor, 1.2f, FrostSnare);
	}
}
