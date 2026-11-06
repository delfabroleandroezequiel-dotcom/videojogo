using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Runs several steps at the same time: an NPC walks off while another one talks, the camera
// travels while a character turns. WaitForAll = false moves on as soon as the first one ends
// (the others keep going in the background).
[GlobalClass]
public partial class StepParallel : CinematicStep
{
	[Export] public Godot.Collections.Array<CinematicStep> Steps = new();
	[Export] public bool WaitForAll = true;

	public override async Task Run(Cinematic cinematic)
	{
		Task[] tasks = Steps.Where(step => step is not null).Select(step => step.Run(cinematic)).ToArray();
		if (tasks.Length == 0)
			return;
		if (WaitForAll)
			await Task.WhenAll(tasks);
		else
			await await Task.WhenAny(tasks);
	}

	public override void Skip(Cinematic cinematic)
	{
		foreach (CinematicStep step in Steps)
			step?.Skip(cinematic);
	}
}
