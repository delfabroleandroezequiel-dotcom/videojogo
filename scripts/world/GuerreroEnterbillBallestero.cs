using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Ballestero de Enterbill — heavy crossbow, one bolt then a long crank to reload.
// - Aims with a flashing hold, leading the shot toward where you're going (PredictAimDirection).
// - Disciplined: it won't loose into an open parry; it keeps the aim until the parry drops (or
//   runs out of patience and shoots anyway).
// - The reload is the window: it cranks for a long beat and can't shoot. Rush it then.
// - Too close: it hops back if the landing is safe, otherwise backs off at a walk.
public partial class GuerreroEnterbillBallestero : GuerreroReino
{
	[Export] public PackedScene BoltScene;
	[Export] public float BoltSpeed = 560f;
	[Export] public float BoltDamageMultiplier = 1.5f;
	[Export] public Vector2 BoltOrigin = new(30f, -8f);
	[Export] public float AimDuration = 0.6f;
	[Export] public float MaxParryHold = 1f;
	[Export] public float ReloadExtra = 1.1f;
	[Export] public float PreferredDistance = 210f;
	[Export] public float TooCloseDistance = 120f;
	[Export] public float MaxAimAngleDegrees = 25f;

	// attack: 0-1 aimed, 2 bolt leaves (recoil), 3-8 lowering and cranking.
	[Export] public int AimFrame = 1;
	[Export] public int FireFrame = 2;

	protected override async Task<bool> Kit()
	{
		if (!await Telegraph("attack", AimFrame, AimDuration * WindupScale))
			return false;
		WindupScale = 1f;
		float held = 0f;
		while (Target is { } p && PlayerReads.IsBlockingToward(p, GlobalPosition.X) && held < MaxParryHold)
		{
			if (!await Wait(0.05f))
				return false;
			held += 0.05f;
		}
		TurnToTarget();
		Pose("attack", FireFrame);
		Fire();
		Shake(1.5f, 0.08f);
		ExtraCooldown = ReloadExtra;
		for (int f = FireFrame + 1; f <= 8; f++)
		{
			if (!await Wait(1.6f / AttackFps))
				return false;
			Pose("attack", f);
		}
		return await Wait(0.1f);
	}

	private void Fire()
	{
		if (BoltScene is null)
			return;
		Vector2 origin = GlobalPosition + new Vector2(BoltOrigin.X * Forward, BoltOrigin.Y);
		Vector2 direction = new(Forward, 0f);
		if (Target is { } p)
		{
			Vector2 aim = PlayerReads.PredictAimDirection(origin, p, BoltSpeed, 0.8f, 0.2f, new Vector2(0f, -10f));
			float angle = Mathf.Clamp(Mathf.Atan2(aim.Y, aim.X * Forward), -Mathf.DegToRad(MaxAimAngleDegrees), Mathf.DegToRad(MaxAimAngleDegrees));
			direction = new Vector2(Mathf.Cos(angle) * Forward, Mathf.Sin(angle));
		}
		var bolt = BoltScene.Instantiate<ElfArrow>();
		bolt.Speed = BoltSpeed;
		bolt.DamageMultiplier = BoltDamageMultiplier;
		bolt.TrailColor = TrailGlowColor;
		bolt.TrailWidth = 4f;
		GetTree().CurrentScene.AddChild(bolt);
		bolt.GlobalPosition = origin;
		bolt.Launch(direction, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (!Attacking && Mathf.Abs(distanceX) < TooCloseDistance * 0.6f && IsOnFloor())
		{
			float away = -Mathf.Sign(distanceX);
			if (TryHop(new Vector2(away * 220f, -260f)))
				return away * 220f;
		}
		return SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance);
	}
}
