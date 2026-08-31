using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Circles around whatever it's parented to (the boss) at a fixed radius, forming a ring around the
// center — until LaunchAttack() detaches it into homing-missile mode: reparents to the scene root
// (so its Position is no longer relative to the still-moving boss) and steers toward the player's
// live position every frame — an actual chase, not a straight-line shot — then detonates like
// BurningSkull on contact or once MissileLifetime runs out. Consumed permanently once launched (see
// Detonate), so using this attack thins out the boss's own barrier — that trade-off is deliberate,
// not something to patch by respawning them.
//
// Not physically solid to the player (see collision_layer on the .tscn): a moving solid body can't
// block movement without also pushing whatever it touches, and that pushing occasionally wedged the
// player between a skull and the floor and ejected them through it. Only the HazardArea (heavy
// contact damage) and the player's attack hitbox still find it while orbiting. They do have their
// own Stats so the player can whittle the barrier down: a plain Hitbox.OnBodyEntered hit already
// finds that Stats child node and damages it same as any enemy, no special-casing needed here beyond
// queueing free on Stats.Died. Still AnimatableBody2D (not a plain Node2D) so the HazardArea child
// still gets a proper physics transform to overlap against each frame. Moves via local Position
// while orbiting (relative to its parent, so it automatically follows the boss without any explicit
// "track the center" code) and never touches Rotation itself, same reasoning as the AnimatableBody2D
// platform gotcha elsewhere in this project: only reposition, never rotate a body something else is
// pushed against.
public partial class OrbitingSkull : AnimatableBody2D
{
	[Export] public float OrbitRadius = 100f;
	[Export] public float OrbitSpeed = 0.6f;
	[Export] public float StartAngle;

	[Export] public PackedScene ExplosionScene;
	[Export] public float HomingSpeed = 260f;
	[Export] public float DetonateDistance = 30f;
	[Export] public float MissileLifetime = 4f;
	[Export] public int MissileDamage = 35;
	[Export] public float ExplosionRadius = 70f;

	// Fired on every death path (player kills it while orbiting, or it detonates as a launched
	// missile) so the boss can track how many of its skulls are actually still alive regardless of
	// which one is currently their parent — LaunchAttack() reparents a skull out of SkullBarrier
	// while it's mid-attack, so "SkullBarrier has no children" is not the same question as "are any
	// of my skulls still alive" (a full volley launch would otherwise read as an empty barrier and
	// trigger an immediate, wrong refill while those same skulls are still out there chasing).
	[Signal] public delegate void SkullDestroyedEventHandler();

	private float _angle;
	private bool _isAttacking;
	private float _missileTimer;
	private Node2D _player;

	public override void _Ready()
	{
		_angle = StartAngle;
		Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;

		Stats stats = GetNodeOrNull<Stats>("Stats");
		if (stats is not null)
			stats.Died += OnKilledByPlayer;
	}

	private void OnKilledByPlayer()
	{
		EmitSignal(SignalName.SkullDestroyed);
		QueueFree();
	}

	// Called by the boss (one at a time, staggered) to detach this skull from the barrier and send
	// it after the player.
	public void LaunchAttack()
	{
		if (_isAttacking)
			return;

		_isAttacking = true;
		_missileTimer = MissileLifetime;
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Reparent's own keepGlobalTransform wasn't reliable here — every skull after the first
		// one in a staggered volley landed near the scene root's origin instead of its actual orbit
		// position, which reads as "spawning at the edge of the map". Capturing GlobalPosition
		// ourselves before reparenting and restoring it after sidesteps whatever's going wrong with
		// the automatic version for an AnimatableBody2D with sync_to_physics on.
		Vector2 currentGlobalPosition = GlobalPosition;
		Reparent(GetTree().CurrentScene, false);
		GlobalPosition = currentGlobalPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isAttacking)
		{
			_missileTimer -= (float)delta;
			if (_player is null || _missileTimer <= 0f || GlobalPosition.DistanceTo(_player.GlobalPosition) <= DetonateDistance)
			{
				Detonate();
				return;
			}

			Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
			Position += direction * HomingSpeed * (float)delta;
			return;
		}

		_angle += OrbitSpeed * (float)delta;
		Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;
	}

	private void Detonate()
	{
		if (!IsInstanceValid(this))
			return;

		if (_player is not null && GlobalPosition.DistanceTo(_player.GlobalPosition) <= ExplosionRadius)
		{
			Stats targetStats = _player.GetNodeOrNull<Stats>("Stats");
			targetStats?.TakeDamage(MissileDamage);
		}

		if (ExplosionScene is not null)
		{
			Node explosionNode = ExplosionScene.Instantiate();
			GetTree().CurrentScene.AddChild(explosionNode);
			((Node2D)explosionNode).GlobalPosition = GlobalPosition;
		}

		EmitSignal(SignalName.SkullDestroyed);
		QueueFree();
	}
}
