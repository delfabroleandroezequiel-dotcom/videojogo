using System;
using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// A step that runs code, for cinematics built from a script instead of the Inspector (an enemy's
// phase change: talk, then its own roar, all while the player is still held). Not authorable.
public partial class StepCode : CinematicStep
{
	public Func<Task> Action;

	public override Task Run(Cinematic cinematic) => Action?.Invoke() ?? Task.CompletedTask;
}
