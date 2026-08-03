using Godot;

namespace Metroidvania.World;

// Its own script (not bare Enemy.cs) so the bite animation trigger is specific to this enemy —
// same reasoning as OrcEnemy: independent per-enemy logic, not a shared-template tweak. RatEnemy
// has no separate attack hitbox/cooldown (see Enemy.ApplyContactDamage) — it just needs to show
// "attack" instead of idle/run whenever it's actually in biting contact with the player.
public partial class RatEnemy : Enemy
{
	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;

		bool isBiting = false;
		foreach (Node body in ContactArea.GetOverlappingBodies())
		{
			if (body is Node2D node && node.IsInGroup("player"))
			{
				isBiting = true;
				break;
			}
		}

		string anim = isBiting ? "attack" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}
}
