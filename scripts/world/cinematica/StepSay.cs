using System.Threading.Tasks;
using Godot;
using Metroidvania.UI;

namespace Metroidvania.World;

// Dialogue lines (translation keys), one speaker per line or one for all. The world keeps running;
// the player advances with Interact/Attack, or it moves on by itself after AutoAdvanceSeconds.
[GlobalClass]
public partial class StepSay : CinematicStep
{
	[Export] public string[] SpeakerKeys = System.Array.Empty<string>();
	[Export] public string[] LineKeys = System.Array.Empty<string>();
	[Export] public float AutoAdvanceSeconds = 0f;

	public override async Task Run(Cinematic cinematic)
	{
		if (DialogueBox.Instance is null || LineKeys.Length == 0 || SpeakerKeys.Length == 0)
			return;
		var done = new TaskCompletionSource();
		DialogueBox.Instance.Show(SpeakerKeys, LineKeys, () => done.TrySetResult(), pauseWorld: false);
		if (AutoAdvanceSeconds > 0f)
		{
			while (!done.Task.IsCompleted && GodotObject.IsInstanceValid(cinematic))
			{
				await cinematic.WaitSeconds(AutoAdvanceSeconds);
				if (!done.Task.IsCompleted)
					DialogueBox.Instance.AdvanceLine();
			}
		}
		await done.Task;
	}
}
