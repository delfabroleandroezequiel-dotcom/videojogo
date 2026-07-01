using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class LevelTransition : Area2D
{
	[Export] public string TargetScenePath = "";
	[Export] public Vector2 TargetSpawnPosition = Vector2.Zero;

	private bool _triggered;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || !body.IsInGroup("player") || string.IsNullOrEmpty(TargetScenePath))
			return;

		_triggered = true;
		SaveManager.Instance.PendingSpawnPosition = TargetSpawnPosition;
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, TargetScenePath);
	}
}
