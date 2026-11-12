using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo del Desierto — the burrower. From mid range it digs in (its "burrow" clip: paws flinging
// sand, then gone), travels underground and surfaces right under the player: a sand crack glows on
// the floor where it's about to erupt (WarningTime), then it bursts out with its jaws up. Keep
// moving off the crack. Underground it can't be hit. It only digs when the player stands on
// floor it can reach at about its own height; otherwise it fights like a plain wolf.
public partial class LoboDesierto : LoboBase
{
	[ExportGroup("Excavar")]
	[Export] public float BurrowMinDistance = 90f;
	[Export] public float BurrowCooldown = 5f;
	[Export] public float TunnelTime = 0.45f;
	[Export] public float WarningTime = 0.6f;
	[Export] public Vector2 EruptSize = new(54f, 56f);
	[Export] public float EruptDamage = 1.2f;
	[Export] public float PounceChance = 0.3f;

	private static readonly GroundSlamFx.Palette Sand = new()
	{
		Hot = new Color(1f, 0.75f, 0.3f, 1f),
		Core = new Color(1f, 0.95f, 0.75f, 1f),
		Rock = new Color(0.78f, 0.62f, 0.38f, 1f),
		Dust = new Color(0.85f, 0.72f, 0.5f, 0.6f),
		Light = new Color(1f, 0.8f, 0.45f, 1f),
	};
	private static readonly Color SandColor = new(0.86f, 0.7f, 0.45f, 0.9f);

	private ulong _nextBurrowMsec;

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextBurrowMsec && d >= BurrowMinDistance && Target is { } p && p.IsOnFloor()
			&& FloorAt(p.GlobalPosition.X, 28f) is not null)
		{
			_nextBurrowMsec = Time.GetTicksMsec() + (ulong)(BurrowCooldown * 1000f);
			return await Burrow();
		}
		if (d <= BiteRange)
			return await Bite();
		if (d >= 90f && Roll() < PounceChance)
			return await Pounce();
		if (!await RushIn(BiteRange * 0.8f, 0.8f))
			return StillGoing;
		return await Bite(0.85f);
	}

	private async Task<bool> Burrow()
	{
		TurnToTarget();
		// Dig: frames 0-8 (sand flying from the paws), 9-12 sinking out of sight.
		bool ok = await PlayFrames("burrow", 0, 11, 14f, f =>
		{
			if (f >= 3 && f <= 9)
				SpawnPuffs(GroundPoint(4f), 3, new Vector2(-Forward, -1f), 35f, 60f, 120f, SandColor, 0.4f, 0.45f, additive: false, gravity: 400f);
			if (f == 10)
			{
				GroundSlamFx.Chip(this, GroundPoint(0f), 0.5f, Sand);
				SetHidden(true);
			}
		});
		if (!ok)
			return false;

		// Underground: a dust line runs toward the player, then it picks the spot under him.
		float elapsed = 0f;
		while (elapsed < TunnelTime)
		{
			if (Target is { } p)
			{
				float x = Mathf.MoveToward(GlobalPosition.X, p.GlobalPosition.X, MoveSpeed * 1.4f * 0.05f);
				TryTeleportTo(x);
				GroundSlamFx.Chip(this, GroundPoint(0f), 0.25f, Sand);
			}
			if (!await Wait(0.05f))
				return false;
			elapsed += 0.05f;
		}
		if (Target is { } target)
		{
			// Aim a little ahead of where he's running.
			float aim = target.GlobalPosition.X + Mathf.Clamp(target.Velocity.X * 0.15f, -40f, 40f);
			if (!TryTeleportTo(aim))
				TryTeleportTo(target.GlobalPosition.X);
		}

		GroundSlamFx.WarningCrack(this, GroundPoint(0f), EruptSize.X, WarningTime, Sand);
		if (!await Wait(WarningTime))
			return false;

		// Erupt: reverse dig frames, jaws up, hitbox centred on the spot.
		TurnToTarget();
		SetHidden(false);
		GroundSlamFx.Impact(this, GroundPoint(0f), 0.6f, Sand);
		SpawnPuffs(GroundPoint(0f), 14, Vector2.Up, 50f, 80f, 180f, SandColor, 0.5f, 0.5f, additive: false, gravity: 500f);
		Shake(3f, 0.2f);
		Bark("Ataque", 0.7f);
		_hitOnce = false;
		ok = await PlayFrames("burrow", 8, 4, 16f, f =>
		{
			if (f == 8 && !_hitOnce)
			{
				_hitOnce = true;
				HitOn(0f, EruptSize, BodyBottom - EruptSize.Y * 0.5f, EruptDamage);
			}
			else if (f == 6)
				AttackHitbox.Deactivate();
		});
		AttackHitbox.Deactivate();
		if (!ok)
			return false;
		return await PlayFrames("idle", 0, 1, 8f);
	}

	private bool _hitOnce;
}
