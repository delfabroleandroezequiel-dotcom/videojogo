using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

public partial class AbilityPickup : Area2D
{
	[Export] public string AbilityId = PlayerAbilities.Dash;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		PlayerAbilities abilities = body.GetNodeOrNull<PlayerAbilities>("Abilities");
		if (abilities is null)
			return;

		abilities.Unlock(AbilityId);
		QueueFree();
	}
}
