using Godot;

namespace Metroidvania.World;

// Shared "read the player" helpers for the smarter enemies (ElfArcher, the casters): where to aim
// so a projectile meets the player instead of trailing behind them, and whether this is a good
// moment to let a shot go. Pure queries — each enemy still decides for itself what to do with them.
public static class PlayerReads
{
	public static Metroidvania.Player.Player Find(Node context) =>
		context.GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;

	// Direction from `origin` that intercepts the player for a projectile flying at `speed`: two
	// refinement passes of (distance / speed), leading horizontally by `leadAccuracy` (1 = perfect,
	// 0 = shoot where they are) and vertically only by `verticalLead` (a jump's vertical velocity
	// flips under gravity, so fully leading it overshoots). Dash velocity is ignored — it ends long
	// before the projectile arrives.
	public static Vector2 PredictAimDirection(Vector2 origin, Metroidvania.Player.Player player, float speed,
		float leadAccuracy, float verticalLead, Vector2 aimOffset)
	{
		Vector2 target = player.GlobalPosition + aimOffset;
		Vector2 velocity = player.IsDashing ? Vector2.Zero : player.Velocity;
		Vector2 predicted = target;
		for (int i = 0; i < 2; i++)
		{
			float travelTime = origin.DistanceTo(predicted) / Mathf.Max(1f, speed);
			predicted = target + new Vector2(velocity.X * travelTime * leadAccuracy, velocity.Y * travelTime * verticalLead);
		}
		return (predicted - origin).Normalized();
	}

	// Player is blocking and facing the shooter at `fromX` (a shot now would just get blocked).
	public static bool IsBlockingToward(Metroidvania.Player.Player player, float fromX) =>
		player.IsBlocking && player.IsFacingRight == (fromX > player.GlobalPosition.X);

	// Player is closing the distance toward `fromX` faster than `minSpeed`.
	public static bool IsClosingIn(Metroidvania.Player.Player player, float fromX, float minSpeed = 60f) =>
		player.Velocity.X * Mathf.Sign(fromX - player.GlobalPosition.X) > minSpeed;
}

// Tracks the end of the player's dashes so a shooter can hold its release through a dash and let
// go right as the dash recovery starts. One per enemy; call Update every physics frame.
public sealed class DashWatcher
{
	public float SinceDashEnded { get; private set; } = 999f;
	private bool _wasDashing;

	public void Update(Metroidvania.Player.Player player, float delta)
	{
		SinceDashEnded += delta;
		if (player is null)
			return;
		if (_wasDashing && !player.IsDashing)
			SinceDashEnded = 0f;
		_wasDashing = player.IsDashing;
	}

	// Not mid-dash, past the post-dash grace, and not blocking toward the shooter.
	public bool GoodMomentToRelease(Metroidvania.Player.Player player, float fromX, float postDashGrace) =>
		!player.IsDashing && SinceDashEnded >= postDashGrace && !PlayerReads.IsBlockingToward(player, fromX);
}

// Detects a whiffed swing: the player started an attack and it ended without this enemy taking a
// hit — the classic opening to punish. Call NotifyHitTaken from the enemy's HitTaken handler and
// Update every physics frame; JustWhiffed stays true for a short window after the swing ends.
public sealed class WhiffWatcher
{
	public bool JustWhiffed => _window > 0f;
	public bool PlayerStartedSwing { get; private set; }

	private const float PunishWindow = 0.4f;
	private bool _wasAttacking;
	private bool _hitDuringSwing;
	private float _window;

	public void NotifyHitTaken() => _hitDuringSwing = true;

	public void Update(Metroidvania.Player.Player player, float delta)
	{
		_window -= delta;
		PlayerStartedSwing = false;
		if (player is null)
			return;

		if (player.IsAttacking && !_wasAttacking)
		{
			_hitDuringSwing = false;
			PlayerStartedSwing = true;
		}
		else if (!player.IsAttacking && _wasAttacking && !_hitDuringSwing)
		{
			_window = PunishWindow;
		}
		_wasAttacking = player.IsAttacking;
	}

	public void Consume() => _window = 0f;
}