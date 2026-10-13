using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Parry mirroring for enemy projectiles: one that touches the player inside the parry window turns
// around and flies back as the player's, hitting enemies instead. Each projectile keeps its own
// flight and visuals and only calls these at its contact/turnaround points.
public static class ProjectileReflect
{
	public const uint PlayerLayer = 2u;
	public const uint EnemyLayer = 4u;
	// Multiplied over the projectile's own colours so a returned shot reads as the player's (leans
	// towards the blue of the dash afterimages).
	public static readonly Color ReflectedModulate = new(0.55f, 0.85f, 1.6f, 1f);

	// True when body is the player and they parried (plays the parry FX).
	public static bool TryParry(Node2D body) =>
		body is Metroidvania.Player.Player player && player.TryParryProjectile();

	// Stops seeing the player and starts seeing enemies; any world bits stay, so a shot that stops
	// on walls still does. Deferred: this runs inside body_entered.
	public static void Retarget(Area2D projectile)
	{
		projectile.SetDeferred(CollisionObject2D.PropertyName.CollisionMask, (projectile.CollisionMask & ~PlayerLayer) | EnemyLayer);
		projectile.Modulate = ReflectedModulate;
	}

	// Contact check for a returned projectile: damages and shoves an enemy body. False when body
	// isn't a (vulnerable) enemy.
	public static bool TryHitEnemy(Node2D body, int damage, Vector2 direction, float knockback, DamageElement element = DamageElement.Normal)
	{
		if (!body.IsInGroup("enemy") || body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
			return false;
		stats.TakeDamage(damage, isProjectile: true, element: element);
		if (knockback > 0f && body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", direction, knockback);
		return true;
	}
}
