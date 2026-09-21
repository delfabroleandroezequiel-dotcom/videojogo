using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Hazard : Area2D
{
	[Export] public bool InstantKill = true;
	[Export] public int Damage = 20;
	[Export] public float KnockbackForce = 300f;

	// BodyEntered only fires once per overlap, so a hazard that keeps sitting on the player (a saw
	// sliding into them, a pendulum resting on them) stops hurting as soon as the post-hit
	// invulnerability window ends. Turn this on for hazards that must keep hurting for as long as
	// they overlap; off by default so lava/spikes behave exactly as before.
	[Export] public bool HitWhileOverlapping = false;

	// Runtime-built pieces (LavaFall/LavaFloor/ProceduralWater, etc.) all want this same
	// InstantKill/Damage/KnockbackForce Area2D — build it here once instead of each kit
	// hand-rolling its own copy.
	public static Hazard CreateArea(Node2D parent, bool instantKill, int damage, float knockbackForce)
	{
		var hazard = new Hazard
		{
			Name = "HazardArea",
			CollisionLayer = 0,
			CollisionMask = 2,
			InstantKill = instantKill,
			Damage = damage,
			KnockbackForce = knockbackForce,
		};
		parent.AddChild(hazard);
		hazard.Owner = parent;
		return hazard;
	}

	public override void _Ready()
	{
		BodyEntered += TryHit;
		SetPhysicsProcess(HitWhileOverlapping);
	}

	public override void _PhysicsProcess(double delta)
	{
		foreach (Node2D body in GetOverlappingBodies())
			TryHit(body);
	}

	private void TryHit(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;

		if (InstantKill)
		{
			stats.Kill();
			return;
		}

		stats.TakeDamage(Damage);

		if (body.HasMethod("ApplyKnockback"))
		{
			Vector2 knockDirection = body.GlobalPosition - GlobalPosition;
			knockDirection = knockDirection == Vector2.Zero ? Vector2.Up : knockDirection.Normalized();
			body.Call("ApplyKnockback", knockDirection, KnockbackForce);
		}
	}
}
