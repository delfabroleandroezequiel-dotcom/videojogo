using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Musgoso — moss and fungus grow on its back. Crowd it and it shakes like a wet dog: the body
// flashes sickly green, then a burst of spores goes off all around it (front AND back), poisoning
// whoever is inside. It punishes the player who sticks to it to trade hits; the answer is to back
// off when it flashes and come back in after the burst. Otherwise slow, tanky, a plain biter.
public partial class LoboMusgo : LoboBase
{
	[ExportGroup("Esporas")]
	[Export] public float ShakeRange = 110f;
	[Export] public float ShakeCooldown = 4f;
	[Export] public float ShakeTelegraph = 0.45f;
	[Export] public Vector2 SporeSize = new(150f, 60f);
	[Export] public float SporeDamage = 0.5f;
	[Export] public int SporePoisonTick = 3;
	[Export] public float SporePoisonSeconds = 3f;
	[Export] public Color SporeColor = new(0.65f, 0.95f, 0.3f, 0.9f);

	private ulong _nextShakeMsec;

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextShakeMsec && d <= ShakeRange)
		{
			_nextShakeMsec = Time.GetTicksMsec() + (ulong)(ShakeCooldown * 1000f);
			return await Sacudida();
		}
		if (d > BiteRange && !await RushIn(BiteRange * 0.8f, 0.9f))
			return StillGoing;
		if (!await Bite())
			return false;
		// Still crowding it after the bite → the spores go off.
		if (Time.GetTicksMsec() >= _nextShakeMsec && TargetDistanceX <= ShakeRange)
		{
			_nextShakeMsec = Time.GetTicksMsec() + (ulong)(ShakeCooldown * 1000f);
			return await Sacudida();
		}
		return true;
	}

	private async Task<bool> Sacudida()
	{
		Bark("Gruñido", ShakeTelegraph + 0.2f);
		Color tell = TellColor;
		TellColor = new Color(1.4f, 2.4f, 1f, 1f);
		bool ok = await Telegraph("shake", 0, ShakeTelegraph);
		TellColor = tell;
		ClearFlash();
		if (!ok)
			return false;

		return await PlayFrames("shake", 1, 8, 12f, f =>
		{
			Vector2 body = GlobalPosition + new Vector2(0f, -4f);
			if (f == 2)
			{
				HitOn(0f, SporeSize, 0f, SporeDamage, _ => PoisonTarget());
				SpawnGlow(body, SporeColor, 2.2f, 0.4f);
				SpawnPuffs(body, 26, Vector2.Up, 180f, 50f, 120f, SporeColor, 0.5f, 0.9f);
				SpawnPuffs(body, 10, Vector2.Up, 180f, 20f, 60f, new Color(0.95f, 1f, 0.6f, 1f), 0.3f, 1.2f);
			}
			else if (f == 5)
			{
				AttackHitbox.Deactivate();
			}
			if (f >= 3)
				SpawnPuffs(body, 3, Vector2.Up, 180f, 10f, 30f, SporeColor, 0.4f, 0.8f);
		});
	}

	private void PoisonTarget()
	{
		if (Target is { } player && player.HasMethod("ApplyPoison"))
			player.Call("ApplyPoison", SporePoisonTick, 0.5f, SporePoisonSeconds);
	}
}
