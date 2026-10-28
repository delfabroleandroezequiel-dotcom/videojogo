using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Fades the screen to black (ToBlack) or back from it. Duration 0 = instant (e.g. start in black).
[GlobalClass]
public partial class StepFade : CinematicStep
{
	[Export] public bool ToBlack = true;
	[Export] public float Duration = 0.8f;

	public override async Task Run(Cinematic cinematic)
	{
		ColorRect rect = cinematic.FadeRect();
		float alpha = ToBlack ? 1f : 0f;
		if (Duration <= 0f)
		{
			rect.Color = new Color(0f, 0f, 0f, alpha);
			return;
		}
		Tween tween = rect.CreateTween();
		tween.TweenProperty(rect, "color:a", alpha, Duration);
		await cinematic.ToSignal(tween, Tween.SignalName.Finished);
	}
}
