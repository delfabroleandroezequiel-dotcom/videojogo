using Godot;

namespace Metroidvania.World;

// A solid obstacle that physically blocks the way until a specific enemy dies (typically the
// boss guarding whatever is behind it), then explodes and frees itself -- same
// reference-an-enemy-by-NodePath idea as CaveCollapseOverlay, but reacting to death instead of
// detection. The StaticBody2D/CollisionShape2D siblings do the actual blocking; this script only
// owns the "boss died -> boom -> gone" transition, so as long as it's still alive there's no way
// around it other than the fight.
public partial class RockslideBlocker : Node2D
{
	[Export] public NodePath TriggerEnemyPath;
	[Export] public PackedScene ExplosionScene;
	[Export] public float ExplosionScale = 1f;

	public override void _Ready()
	{
		if (TriggerEnemyPath is null || TriggerEnemyPath.IsEmpty)
			return;

		if (GetNodeOrNull<Enemy>(TriggerEnemyPath) is Enemy enemy)
			enemy.Stats.Died += OnTriggerDied;
	}

	private void OnTriggerDied()
	{
		if (ExplosionScene is not null)
		{
			Node explosionNode = ExplosionScene.Instantiate();
			if (explosionNode is Explosion explosion)
				explosion.TargetScale = ExplosionScale;

			GetTree().CurrentScene.AddChild(explosionNode);
			((Node2D)explosionNode).GlobalPosition = GlobalPosition;
		}

		QueueFree();
	}
}
