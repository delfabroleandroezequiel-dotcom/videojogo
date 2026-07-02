using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Hazard : Area2D
{
	[Export] public bool InstantKill = true;
	[Export] public int Damage = 20;
	[Export] public float KnockbackForce = 300f;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
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
