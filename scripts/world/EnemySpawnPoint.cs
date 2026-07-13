using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum EnemyCategory
{
	Ground,
	Flying,
}

// Placed in a level instead of a specific enemy scene wherever that spot should take part in
// the enemy randomizer. With the randomizer off, it just spawns DefaultEnemy — the encounter
// the level was actually designed around — so authoring a level never depends on the mode.
public partial class EnemySpawnPoint : Node2D
{
	[Export] public EnemyCategory Category = EnemyCategory.Ground;
	[Export] public PackedScene DefaultEnemy;

	public override void _Ready()
	{
		PackedScene enemyScene = SaveManager.Instance.EnemyRandomizerEnabled
			? PickRandomEnemy()
			: DefaultEnemy;

		if (enemyScene is null)
		{
			QueueFree();
			return;
		}

		Node enemy = enemyScene.Instantiate();
		GetParent().AddChild(enemy);
		if (enemy is Node2D enemyNode2D)
			enemyNode2D.GlobalPosition = GlobalPosition;

		QueueFree();
	}

	// Seeded from this save's RandomizerSeed XORed with this spawn point's own stable path, so
	// the pick is deterministic per spawn point: reloading the same save reproduces the exact
	// same shuffle, while a different save (different seed) or a different spawn point (different
	// path) gets a different result.
	private PackedScene PickRandomEnemy()
	{
		PackedScene[] pool = Category == EnemyCategory.Flying
			? GameConfig.Instance.FlyingEnemyPool
			: GameConfig.Instance.GroundEnemyPool;

		if (pool.Length == 0)
			return DefaultEnemy;

		uint seed = (uint)SaveManager.Instance.RandomizerSeed ^ (uint)GetPath().ToString().Hash();
		var rng = new RandomNumberGenerator { Seed = seed };
		return pool[rng.RandiRange(0, pool.Length - 1)];
	}
}
