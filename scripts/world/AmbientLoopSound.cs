using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Continuous ambience for decorations that are always "on" from the moment the scene loads (lit
// torches/braziers/lamps, falling waterfalls) — none of these have an ignite/start event to hook,
// so the fire-and-forget Sfx.Play/PlayAt helpers don't fit (they free their player after one
// playthrough). This owns its own AudioStreamPlayer2D and re-triggers itself on Finished instead.
// Category/Key follow the same Sfx folder convention (e.g. "World/Torch" + "Torch Loop",
// "World/Water" + "Waterfall Loop") — drop this node anywhere that needs a looping ambient cue.
public partial class AmbientLoopSound : Node2D
{
	[Export] public string Category = "World/Torch";
	[Export] public string Key = "Torch Loop";

	// Kept short on purpose — decorations like CuevaLantern get placed in dense clusters (~150px
	// apart in InteriorCuevaBosqueLobos1's 29-lantern corridor), and the old 500px default meant
	// standing anywhere in there overlapped 5-7 independent loops into a wash of noise instead of
	// each lantern reading as its own nearby crackle.
	[Export] public float MaxDistance = 220f;
	[Export] public float VolumeDb = -6f;

	public override void _Ready()
	{
		AudioStream stream = Sfx.Load(Category, Key);
		if (stream is null)
			return;

		var player = new AudioStreamPlayer2D
		{
			Stream = stream,
			MaxDistance = MaxDistance,
			VolumeDb = VolumeDb,
			// Slight per-instance offset so a cluster of nearby sources doesn't loop in lockstep.
			PitchScale = 1f + ((float)GD.Randf() * 0.1f - 0.05f),
		};
		AddChild(player);
		player.Finished += () => player.Play();
		player.Play();
	}
}
