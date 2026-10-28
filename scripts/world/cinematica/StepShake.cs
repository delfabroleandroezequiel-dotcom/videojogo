using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

[GlobalClass]
public partial class StepShake : CinematicStep
{
	[Export] public float Strength = 4f;
	[Export] public float Duration = 0.4f;
	[Export] public bool WaitForEnd;

	public override async Task Run(Cinematic cinematic)
	{
		cinematic.Player?.ShakeCamera(Strength, Duration);
		if (WaitForEnd)
			await cinematic.WaitSeconds(Duration);
	}
}
