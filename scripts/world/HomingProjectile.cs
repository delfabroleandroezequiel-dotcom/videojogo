using Godot;

namespace Metroidvania.World;

// Steers toward the player's live position for HomingDuration seconds, then flies straight
// onward in whatever direction it was last facing — same launch/hit/lifetime plumbing as the
// base Projectile, just with a steering phase bolted on top instead of a fixed direction.
public partial class HomingProjectile : Projectile
{
	[Export] public float HomingDuration = 1.5f;
	[Export] public float TurnSpeed = 4f;

	private float _homingTimer;
	private Node2D _target;
	private Vector2 _direction = Vector2.Right;
	private bool _directionInitialized;

	public override void _Ready()
	{
		base._Ready();
		_homingTimer = HomingDuration;
		_target = GetTree().GetFirstNodeInGroup("player") as Node2D;
	}

	// Launch() (base class, not overridable — see Projectile.cs) sets Rotation to the fired
	// direction before this ever runs a physics frame, so reading it back here on the first tick
	// gets the real launch direction without needing a virtual/override hook into Launch() itself.
	// Always moves via this class's own _direction (never the base's) — base._PhysicsProcess is
	// never called, since its private _direction would stay frozen at the launch angle and the
	// projectile would snap back to it the instant homing ends otherwise.
	public override void _PhysicsProcess(double delta)
	{
		if (!_directionInitialized)
		{
			_direction = Vector2.Right.Rotated(Rotation);
			_directionInitialized = true;
		}

		if (_homingTimer > 0f && _target is not null)
		{
			_homingTimer -= (float)delta;
			Vector2 toTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			_direction = _direction.Slerp(toTarget, Mathf.Clamp(TurnSpeed * (float)delta, 0f, 1f)).Normalized();
			Rotation = _direction.Angle();
		}

		Position += _direction * Speed * (float)delta;
	}
}
