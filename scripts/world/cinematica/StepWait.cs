using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

[GlobalClass]
public partial class StepWait : CinematicStep
{
	[Export] public float Seconds = 0.5f;

	public override Task Run(Cinematic cinematic) => cinematic.WaitSeconds(Seconds);
}
