using Godot;

namespace Metroidvania.World;

// Shared hover-and-hunt flight for airborne enemies (Bat, Mosquito). Bypasses Enemy's
// gravity/MoveAndSlide chase entirely — flight doesn't need a floor — and instead steers a
// flight anchor toward the player when in range (or back home otherwise), with a sine wingbeat
// bob layered on top as a pure visual offset so it never fights the steering. BatEnemy overrides
// OverrideSteering to take full manual control for its dive-attack; MosquitoEnemy uses the
// default hunt-and-hover as-is with only contact damage.
public partial class FlyingEnemy : Enemy
{
	[Export] public float HoverAmplitude = 14f;
	[Export] public float HoverSpeed = 2f;
	[Export] public float ChaseSpeed = 90f;
	[Export] public float ChaseAcceleration = 220f;

	private float _time;
	private Vector2 _homePosition;

	// The "real" flight position, steered smoothly toward the target; GlobalPosition is this
	// plus a purely cosmetic bob offset. Subclasses that take manual control (see
	// OverrideSteering) should keep this in sync with GlobalPosition so hover resumes cleanly.
	protected Vector2 Anchor;
	protected Vector2 FlightVelocity;
	protected bool Hunting;
	protected Node2D Player;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_homePosition = GlobalPosition;
		Anchor = GlobalPosition;
		_time = GD.Randf() * Mathf.Tau;
		Sprite?.Play("fly");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		_time += (float)delta;
		Player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (OverrideSteering(delta))
		{
			ApplyContactDamage();
			return;
		}

		Vector2 target = _homePosition;
		Hunting = false;

		if (Player is not null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= DetectionRange)
		{
			Hunting = true;
			target = Player.GlobalPosition;
			FacingRight = Player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}

		Vector2 toTarget = target - Anchor;
		Vector2 desiredVelocity = Hunting && toTarget.Length() > StopDistance
			? toTarget.Normalized() * ChaseSpeed
			: Vector2.Zero;

		FlightVelocity = FlightVelocity.MoveToward(desiredVelocity, ChaseAcceleration * (float)delta);
		Anchor += FlightVelocity * (float)delta;
		GlobalPosition = Anchor + new Vector2(0f, Mathf.Sin(_time * HoverSpeed) * HoverAmplitude);

		UpdateAnimation(FlightVelocity);
		ApplyContactDamage();
	}

	// Return true to fully take over this frame's movement (e.g. a dive-attack lunge). While
	// taking over, keep Anchor synced to GlobalPosition so normal hover/steering resumes cleanly
	// afterward instead of snapping back.
	protected virtual bool OverrideSteering(double delta) => false;

	// Flying enemies drive their own position directly (no MoveAndSlide), so the base's
	// velocity-based knockback would never actually move anything — nudge the anchor itself
	// instead and let the normal steering resume next frame.
	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (!KnockbackEnabled)
			return;

		Anchor += direction * force * 0.05f;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
	}
}
