using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Terrible — the heavy. Big, slow to turn, hyper-armoured while attacking. Its signature is the
// headbutt charge: it lowers its head and paws (held, flashing, a rumbling growl), then charges in a
// straight line and rams with its skull — a hit throws the player far back. The charge doesn't steer:
// jump or roll through it. If it rams a wall instead, it staggers, dazed, for WallStunSeconds —
// the big punish window. Up close it bites hard.
public partial class LoboTerrible : LoboBase
{
	[ExportGroup("Cabezazo")]
	[Export] public float ChargeMinDistance = 80f;
	[Export] public float ChargeMaxDistance = 260f;
	[Export] public float ChargeCooldown = 3.5f;
	[Export] public float ChargeTelegraph = 0.6f;
	[Export] public float ChargeSpeed = 360f;
	[Export] public float ChargeMaxTime = 0.9f;
	[Export] public float RamReach = 30f;
	[Export] public Vector2 RamSize = new(44f, 40f);
	[Export] public float RamDamage = 1.4f;
	[Export] public float RamKnockback = 560f;
	[Export] public float WallStunSeconds = 1.4f;
	[Export] public float BiteDamage = 1.2f;

	private ulong _nextChargeMsec;

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextChargeMsec && d >= ChargeMinDistance && d <= ChargeMaxDistance)
		{
			_nextChargeMsec = Time.GetTicksMsec() + (ulong)(ChargeCooldown * 1000f);
			return await Headbutt();
		}
		if (d <= BiteRange)
			return await Bite(1f, BiteDamage);
		if (!await RushIn(BiteRange * 0.8f, 0.8f))
			return StillGoing;
		return await Bite(1f, BiteDamage);
	}

	private async Task<bool> Headbutt()
	{
		TurnToTarget();
		// Head down, pawing: frames 0-3, held on 3 flashing.
		if (!await PlayFrames("headbutt", 0, 2, 10f))
			return false;
		Bark("Gruñido", ChargeTelegraph + 0.3f);
		Pose("headbutt", 3);
		FlashBody(ChargeTelegraph);
		GroundSlamFx.Chip(this, GroundPoint(-10f), 0.4f);
		if (!await Wait(ChargeTelegraph))
			return false;
		ClearFlash();

		// The charge: straight line, no steering, skull hitbox live the whole way.
		Bark("Ataque", 0.8f);
		HitOn(RamReach, RamSize, BiteY + 6f, RamDamage, _ => Ram());
		Lunge(ChargeSpeed, ChargeMaxTime);
		float elapsed = 0f;
		bool wall = false;
		while (elapsed < ChargeMaxTime)
		{
			Pose("headbutt", elapsed % 0.16f < 0.08f ? 4 : 5);
			if (!await WaitPhysicsFrame())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			// Stopped dead: a wall right ahead (rammed it) or a ledge (CanStepTo held it back).
			if (elapsed > 0.08f && Mathf.Abs(Velocity.X) < 5f)
			{
				wall = TestMove(GlobalTransform, new Vector2(Forward * 14f, 0f));
				break;
			}
			if (elapsed % 0.1f < 0.02f)
				GroundSlamFx.Chip(this, GroundPoint(-20f), 0.3f);
		}
		StopLunge();
		AttackHitbox.Deactivate();

		if (wall)
		{
			GroundSlamFx.Impact(this, GroundPoint(RamReach + 10f), 0.7f);
			Shake(6f, 0.3f);
			Bark("Golpeado", 0.8f);
			Pose("hurt", 2);
			SpawnDazedStars();
			ExtraCooldown = 0.5f;
			// Dazed means open: no hyper-armour while it reels.
			HyperArmorWhileAttacking = false;
			bool ok = await Wait(WallStunSeconds);
			if (IsInstanceValid(this))
				HyperArmorWhileAttacking = true;
			return ok;
		}
		return await PlayFrames("headbutt", 6, 8, 10f);
	}

	private void Ram()
	{
		Shake(5f, 0.25f);
		if (Target is { } player)
			player.ApplyKnockback(new Vector2(Forward, -0.45f).Normalized(), RamKnockback);
	}

	private void SpawnDazedStars()
	{
		Vector2 head = Visual.ToGlobal(new Vector2(BiteReach * 0.6f, BiteY - 16f));
		for (int i = 0; i < 3; i++)
			SpawnPuffs(head, 2, Vector2.Up, 60f, 10f, 25f, new Color(1f, 0.95f, 0.5f, 1f), 0.25f, WallStunSeconds * 0.6f);
	}
}
