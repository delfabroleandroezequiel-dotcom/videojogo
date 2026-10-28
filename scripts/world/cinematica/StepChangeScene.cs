using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Goes to another scene (usually after a fade to black). Empty ScenePath = stay (not built yet).
[GlobalClass]
public partial class StepChangeScene : CinematicStep
{
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath = "";

	public override Task Run(Cinematic cinematic)
	{
		if (!string.IsNullOrEmpty(ScenePath))
			cinematic.GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ScenePath);
		return Task.CompletedTask;
	}
}
