using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// Dialogue lines (translation keys), one speaker per line or one for all. The world keeps running.
// How it moves on (cinematics set the pace, not the player — that's the default):
//   * AdvanceWithVoice = false: the player advances with Interact/Attack (or it goes by itself after
//     AutoAdvanceSeconds);
//   * AdvanceWithVoice: each line waits for its voice clip to finish, then GapAfterVoice, then the
//     next — a narration that plays out naturally. A line without a recording waits its reading
//     time instead. AllowSkip = false keeps the player from cutting it short.
[GlobalClass]
public partial class StepSay : CinematicStep
{
	[Export] public string[] SpeakerKeys = System.Array.Empty<string>();
	[Export] public string[] LineKeys = System.Array.Empty<string>();
	[Export] public float AutoAdvanceSeconds = 0f;
	[Export] public bool AdvanceWithVoice = true;
	[Export] public bool AllowSkip = false;
	[Export] public float GapAfterVoice = 0.7f;
	// Extra silence after each line (index = line), on top of GapAfterVoice: room for a line to land
	// before a change of tone.
	[Export] public float[] ExtraPauses = System.Array.Empty<float>();
	// Reading speed used when a line has no voice (characters per second).
	[Export] public float ReadingSpeed = 15f;

	public override async Task Run(Cinematic cinematic)
	{
		DialogueBox box = DialogueBox.Instance;
		if (box is null || LineKeys.Length == 0 || SpeakerKeys.Length == 0)
			return;
		var done = new TaskCompletionSource();
		box.Show(SpeakerKeys, LineKeys, () => done.TrySetResult(), pauseWorld: false);
		box.InputLocked = !AllowSkip;
		try
		{
			if (AdvanceWithVoice)
				await PaceWithVoice(cinematic, box, done.Task);
			else if (AutoAdvanceSeconds > 0f)
			{
				while (!done.Task.IsCompleted && GodotObject.IsInstanceValid(cinematic))
				{
					await cinematic.WaitSeconds(AutoAdvanceSeconds);
					if (!done.Task.IsCompleted)
						box.AdvanceLine();
				}
			}
			await done.Task;
		}
		finally
		{
			box.InputLocked = false;
		}
	}

	private async Task PaceWithVoice(Cinematic cinematic, DialogueBox box, Task finished)
	{
		for (int line = 0; line < LineKeys.Length && !finished.IsCompleted; line++)
		{
			// The box starts the line's voice as it shows it; wait it out (or the reading time).
			await cinematic.WaitPhysicsFrame();
			if (Sfx.IsVoicePlaying)
			{
				while (Sfx.IsVoicePlaying && GodotObject.IsInstanceValid(cinematic) && !finished.IsCompleted)
					await cinematic.WaitPhysicsFrame();
			}
			else
			{
				string text = TranslationServer.Translate(LineKeys[line]);
				await cinematic.WaitSeconds(Mathf.Max(1.8f, text.Length / Mathf.Max(1f, ReadingSpeed) + 0.6f));
			}
			if (!GodotObject.IsInstanceValid(cinematic) || finished.IsCompleted)
				return;
			await cinematic.WaitSeconds(GapAfterVoice + (line < ExtraPauses.Length ? ExtraPauses[line] : 0f));
			if (!finished.IsCompleted)
				box.AdvanceLine();
		}
	}
}
