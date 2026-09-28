using Godot;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// EnemyTestRoom exists to fight and tune one enemy at a time — at their normal HP most die in a
// hit or two, which isn't enough time to actually observe a kit (windup, reposition, etc.) in
// action, and Enemy.cs hides the health bar by default (fine for real levels, useless here where
// watching HP drop in real time is the whole point). Applies to whatever's in the "enemy" group at
// the time this room loads, so swapping in a different enemy to test next doesn't need either of
// these re-tuned by hand.
public partial class EnemyTestRoomHealthBuff : Node2D
{
	[Export] public float HealthMultiplier = 5f;

	// Deferred: enemies placed after this node in the tree haven't run their own _Ready yet (which is
	// where they join "enemy" and load their profile HP), so a direct pass here missed them.
	public override void _Ready() => CallDeferred(MethodName.ApplyBuff);

	private void ApplyBuff()
	{
		foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
		{
			Stats stats = enemy.GetNodeOrNull<Stats>("Stats");
			if (stats is not null)
			{
				stats.MaxHealth = Mathf.RoundToInt(stats.MaxHealth * HealthMultiplier);
				stats.ResetToFull();
			}

			StatBar healthBar = enemy.GetNodeOrNull<StatBar>("HealthBar");
			if (healthBar is not null)
				healthBar.Visible = true;
		}
	}
}
