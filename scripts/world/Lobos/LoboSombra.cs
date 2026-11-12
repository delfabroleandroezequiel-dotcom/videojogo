using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Sombra — dissolves into violet smoke and reforms at the player's back. The tell is fair:
// it bleeds smoke while dissolving, and when it reforms behind him the smoke gathers first
// (ReformTell) with a flash before the bite. Turn around when it vanishes. It also smokes away on
// the defensive: hit it twice in quick succession and it slips off instead of taking a third.
// Can't be hit while it's smoke. If there's no floor behind the player (wall, pit) it reforms in
// front instead.
public partial class LoboSombra : LoboBase
{
	[ExportGroup("Humo")]
	[Export] public float SmokeMinDistance = 70f;
	[Export] public float SmokeCooldown = 4.5f;
	[Export] public float BehindOffset = 62f;
	[Export] public float SmokeTime = 0.4f;
	[Export] public float ReformTell = 0.35f;
	[Export] public int EvadeHits = 2;
	[Export] public float EvadeWindow = 1f;
	[Export] public Color SmokeColor = new(0.55f, 0.3f, 1f, 0.85f);

	private ulong _nextSmokeMsec;
	private int _recentHits;
	private ulong _lastHitMsec;
	private bool _evadeRequested;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		Stats.HitTaken += _ => OnHitTaken();
	}

	private void OnHitTaken()
	{
		ulong now = Time.GetTicksMsec();
		_recentHits = now - _lastHitMsec <= (ulong)(EvadeWindow * 1000f) ? _recentHits + 1 : 1;
		_lastHitMsec = now;
		if (_recentHits >= EvadeHits && Stats.CurrentHealth > 0)
		{
			_recentHits = 0;
			_evadeRequested = true;
		}
	}

	protected override void Think(float dt)
	{
		if (_evadeRequested && CanAttack)
			TryAttackNow();
	}

	protected override async Task<bool> Kit()
	{
		if (_evadeRequested)
		{
			_evadeRequested = false;
			return await Smoke(behind: false);
		}
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextSmokeMsec && d >= SmokeMinDistance && Target is { } p && p.IsOnFloor())
		{
			_nextSmokeMsec = Time.GetTicksMsec() + (ulong)(SmokeCooldown * 1000f);
			return await Smoke(behind: true);
		}
		if (d <= BiteRange)
			return await Bite();
		if (!await RushIn(BiteRange * 0.8f, 0.8f))
			return StillGoing;
		return await Bite(0.85f);
	}

	// behind = reform at the player's back and bite; otherwise (evading) reform away from him.
	private async Task<bool> Smoke(bool behind)
	{
		TurnToTarget();
		// Dissolve: frames 0-6 (smoke billowing off), 7-9 gone.
		bool ok = await PlayFrames("smoke", 0, 7, 16f, f =>
		{
			SpawnPuffs(GlobalPosition, 4, Vector2.Up, 80f, 15f, 45f, SmokeColor, 0.6f, 0.6f);
			if (f == 5)
				SetHidden(true);
		});
		if (!ok || !await Wait(SmokeTime))
			return false;

		if (Target is { } p)
		{
			float back = p.IsFacingRight ? -1f : 1f;
			float x = behind ? p.GlobalPosition.X + back * BehindOffset : GlobalPosition.X - TowardTarget * 150f;
			if (!TryTeleportTo(x) && behind)
				TryTeleportTo(p.GlobalPosition.X - back * BehindOffset);
		}

		// Reform: smoke gathers on the spot, then the body pops back in, flashing.
		TurnToTarget();
		float gathered = 0f;
		while (gathered < ReformTell)
		{
			SpawnPuffs(GlobalPosition + new Vector2(RandRange(-30f, 30f), RandRange(-20f, 10f)), 2, Vector2.Up, 180f, 5f, 15f, SmokeColor, 0.5f, 0.3f);
			if (!await Wait(0.05f))
				return false;
			gathered += 0.05f;
		}
		SetHidden(false);
		if (!await PlayFrames("smoke", 10, 12, 14f))
			return false;
		if (!behind)
			return true;
		return await Bite(0.5f);
	}
}
