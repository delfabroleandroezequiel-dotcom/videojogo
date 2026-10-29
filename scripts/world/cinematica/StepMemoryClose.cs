using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Ends a remembered scene: the picture drains to warm sepia and blurs (RecuerdoCierre.gdshader).
// Usually followed by a fade to black and the narrator.
[GlobalClass]
public partial class StepMemoryClose : CinematicStep
{
	[Export] public float Duration = 2.5f;
	[Export] public bool WaitForEnd = true;

	public override async Task Run(Cinematic cinematic)
	{
		var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/RecuerdoCierre.gdshader") };
		material.SetShaderParameter("amount", 0f);
		var layer = new CanvasLayer { Layer = 4, Name = "CierreRecuerdo" };
		var rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(rect);
		cinematic.AddChild(layer);

		Tween tween = cinematic.CreateTween();
		tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("amount", v)), 0f, 1f, Mathf.Max(0.01f, Duration))
			.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
		if (WaitForEnd)
			await cinematic.ToSignal(tween, Tween.SignalName.Finished);
	}
}
