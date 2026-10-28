using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Blurs the screen (dazed, about to pass out) toward Strength over Duration; Strength 0 clears it.
[GlobalClass]
public partial class StepBlur : CinematicStep
{
	[Export] public float Strength = 0.6f;
	[Export] public float Duration = 1f;
	[Export] public bool WaitForEnd;

	public override async Task Run(Cinematic cinematic)
	{
		ShaderMaterial material = cinematic.BlurMaterial();
		Tween tween = cinematic.CreateTween();
		tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("strength", v)),
			(float)material.GetShaderParameter("strength"), Strength, Mathf.Max(0.01f, Duration));
		if (WaitForEnd)
			await cinematic.ToSignal(tween, Tween.SignalName.Finished);
	}
}
