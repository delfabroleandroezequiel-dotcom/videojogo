using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Drops one burning rock (ruina/Meteor) at OffsetX from the player, on the ground below: the same
// warning mark, fall and blast as in play, but harmless by default, for an impact the cutscene
// times itself. Optionally waits until it hits.
[GlobalClass]
public partial class StepMeteor : CinematicStep
{
	[Export] public float OffsetX = 90f;
	[Export] public bool FromRight = true;
	[Export] public bool IsRock;
	[Export] public int Damage;
	[Export] public PackedScene FireScene;
	[Export] public float WarningTime = 0.5f;
	[Export] public bool WaitForImpact = true;

	public override async Task Run(Cinematic cinematic)
	{
		if (cinematic.Player is not { } player)
			return;
		Vector2 from = player.GlobalPosition + new Vector2(OffsetX, -300f);
		if (BurningMap.GroundBelow(player, from) is not Vector2 ground)
			return;
		var meteor = new Meteor
		{
			Target = ground,
			IsRock = IsRock,
			Damage = Damage,
			FireScene = IsRock ? null : FireScene,
			FromRight = FromRight,
			WarningTime = WarningTime,
		};
		cinematic.GetTree().CurrentScene.AddChild(meteor);
		if (WaitForImpact)
			await cinematic.WaitSeconds(meteor.WarningTime + meteor.FallTime);
	}
}
