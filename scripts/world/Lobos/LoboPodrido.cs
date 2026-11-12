using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Podrido — the carrion wolf, ribs showing. Its retch (gagging, held and flashing sick green)
// lobs a glob of rot in an arc toward where the player stands; it splashes poison and leaves a
// puddle that keeps poisoning whoever steps in. It retches to zone the player off, then bites —
// and its bite poisons too. Slow, tanky, never in a hurry.
public partial class LoboPodrido : LoboBase
{
	[ExportGroup("Arcada")]
	[Export] public PackedScene PuddleScene;
	[Export] public float RetchMinDistance = 60f;
	[Export] public float RetchMaxDistance = 230f;
	[Export] public float RetchCooldown = 3.5f;
	[Export] public float RetchTelegraph = 0.5f;
	[Export] public float GlobAirTime = 0.6f;
	[Export] public float GlobDamage = 0.5f;
	[Export] public Color RotColor = new(0.6f, 0.9f, 0.2f, 1f);
	[Export] public int BitePoisonTick = 2;
	[Export] public float BitePoisonSeconds = 2.5f;

	private ulong _nextRetchMsec;

	protected override async Task<bool> Kit()
	{
		float d = TargetDistanceX;
		if (Time.GetTicksMsec() >= _nextRetchMsec && d >= RetchMinDistance && d <= RetchMaxDistance)
		{
			_nextRetchMsec = Time.GetTicksMsec() + (ulong)(RetchCooldown * 1000f);
			return await Retch();
		}
		if (d > BiteRange && !await RushIn(BiteRange * 0.8f, 0.9f))
			return StillGoing;
		return await Bite(1f, 1f, _ => PoisonTarget());
	}

	private void PoisonTarget()
	{
		if (Target is { } player && player.HasMethod("ApplyPoison"))
			player.Call("ApplyPoison", BitePoisonTick, 0.5f, BitePoisonSeconds);
	}

	private async Task<bool> Retch()
	{
		TurnToTarget();
		if (!await PlayFrames("retch", 0, 1, 10f))
			return false;
		Bark("Gruñido", RetchTelegraph + 0.2f);
		Pose("retch", 2);
		FlashBody(RetchTelegraph);
		float elapsed = 0f;
		while (elapsed < RetchTelegraph)
		{
			Pose("retch", elapsed % 0.2f < 0.1f ? 2 : 3);
			if (!await Wait(0.05f))
				return false;
			elapsed += 0.05f;
		}
		ClearFlash();

		return await PlayFrames("retch", 4, 8, 10f, f =>
		{
			if (f == 5)
				Spit();
		});
	}

	private void Spit()
	{
		if (Target is not { } player || GetTree().CurrentScene is not Node scene)
			return;
		Vector2 mouth = Visual.ToGlobal(MouthLocal);
		Vector2 aim = player.GlobalPosition + new Vector2(Mathf.Clamp(player.Velocity.X * 0.25f, -50f, 50f), 10f);
		const float gravity = 700f;
		float t = GlobAirTime;
		var velocity = new Vector2((aim.X - mouth.X) / t, (aim.Y - mouth.Y - 0.5f * gravity * t * t) / t);
		var glob = new AcidGlob
		{
			Velocity = velocity,
			ThrowerStats = Stats,
			DamageMultiplier = GlobDamage,
			PuddleScene = PuddleScene,
			GravityScale = gravity,
			GlowColor = RotColor,
		};
		scene.AddChild(glob);
		glob.GlobalPosition = mouth;
		SpawnPuffs(mouth, 6, new Vector2(Forward, 0.2f), 25f, 40f, 90f, RotColor, 0.35f, 0.35f);
	}
}
