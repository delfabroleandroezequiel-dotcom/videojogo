using Godot;

namespace Metroidvania.World;

public enum PlatformSwitches
{
	None,
	AtPointA,
	AtPointB,
	Both,
}

// Reusable greybox piece: a platform that travels a straight line between the position it's
// placed at (Point A) and PointBOffset away from it (Point B) — lateral, vertical or diagonal,
// whatever offset is set. AutoPatrol picks which of the two behaviors the level needs:
//   - on (default): ping-pongs A<->B forever on its own, optionally pausing WaitTimeAtPoints
//     seconds at each end. This is the "va y vuelve" patrol platform.
//   - off: starts parked at A and only moves when called (by a spawned switch, see Switches
//     below, or externally via CallToA()/CallToB()).
// JumpHeight adds the optional "salto en el medio": 0 keeps a flat glide, >0 arcs the platform
// upward at the midpoint of whichever leg it's currently travelling, like a hop.
// Switches is opt-in (defaults to None) and spawns Lever.tscn instances at runtime, already
// wired to CallToA()/CallToB() — no manual signal-connecting in the editor needed. A switch at
// Point A summons the platform to A, a switch at Point B summons it to B, so a player standing
// at either end can call it over. Spawned as siblings (children of this platform's parent, not
// of the platform itself) so they stay put at their point instead of riding along.
// AnimatableBody2D + sync_to_physics (see the .tscn) carries the player automatically, same
// setup as Elevator.
public partial class MovingPlatform : AnimatableBody2D
{
	[Export] public Vector2 PointBOffset = new(200f, 0f);
	[Export] public float Speed = 80f;
	[Export] public bool AutoPatrol = true;

	// Only used while AutoPatrol is on — how long the platform sits at each endpoint before
	// reversing. Ignored in switch-controlled mode: there, arriving just means "parked until
	// called again", no timer needed.
	[Export] public float WaitTimeAtPoints;

	// 0 = straight line between the two points. >0 = the platform rises this many pixels above
	// the straight line at the midpoint of the current leg, easing back down by the far end —
	// a hop rather than a flat glide.
	[Export] public float JumpHeight;

	[Export] public PlatformSwitches Switches = PlatformSwitches.None;

	// Local offset (relative to whichever point a switch is placed at) so the lever sits beside
	// the platform's rest position instead of on top of it.
	[Export] public Vector2 SwitchOffset = new(0f, -32f);

	// Fast-forwards the patrol by this many seconds right at _Ready, same idea as SpikeTrap's
	// StartOffset — only matters with AutoPatrol on. Staggering a row of platforms by half their
	// leg's travel time (_legDistance / Speed) puts neighbors in anti-phase, so one is approaching
	// while the next is pulling away, instead of every platform moving in lockstep.
	[Export] public float StartOffset;

	private Vector2 _pointA;
	private Vector2 _pointB;
	private float _legDistance;

	// Progress (0..1) along the leg currently being travelled, from the leg's start to its end.
	private float _progress;
	private bool _movingToB;
	private bool _moving;
	private bool _waiting;
	private float _waitTimer;

	public override void _Ready()
	{
		_pointA = Position;
		_pointB = Position + PointBOffset;
		_legDistance = _pointA.DistanceTo(_pointB);

		_movingToB = true;
		_moving = AutoPatrol;

		if (AutoPatrol && StartOffset > 0f)
			SimulateForward(StartOffset);

		if (Switches != PlatformSwitches.None)
			SpawnSwitches();
	}

	// Closed-form replay of _PhysicsProcess's leg/wait stepping, so StartOffset lands exactly
	// where the real loop would be after that many seconds — including crossing multiple legs and
	// waits if StartOffset is large enough, not just a single partial leg.
	private void SimulateForward(float seconds)
	{
		if (Speed <= 0f || _legDistance <= 0f)
			return;

		float travelTime = _legDistance / Speed;
		float remaining = seconds;

		while (remaining > 0f)
		{
			float timeLeftOnLeg = (1f - _progress) * travelTime;
			if (remaining < timeLeftOnLeg)
			{
				_progress += remaining / travelTime;
				return;
			}

			remaining -= timeLeftOnLeg;
			_progress = 1f;

			if (WaitTimeAtPoints > 0f)
			{
				if (remaining < WaitTimeAtPoints)
				{
					_waiting = true;
					_waitTimer = remaining;
					return;
				}

				remaining -= WaitTimeAtPoints;
			}

			_movingToB = !_movingToB;
			_progress = 0f;
		}
	}

	private void SpawnSwitches()
	{
		Node parent = GetParent() ?? this;

		if (Switches is PlatformSwitches.AtPointA or PlatformSwitches.Both)
			LeverSpawner.SpawnAt(parent, _pointA + SwitchOffset, CallToA);

		if (Switches is PlatformSwitches.AtPointB or PlatformSwitches.Both)
			LeverSpawner.SpawnAt(parent, _pointB + SwitchOffset, CallToB);
	}

	public void CallToA()
	{
		if (_movingToB)
		{
			// Mirror progress instead of resetting it to 0 so the platform (and its arc, if any)
			// reverses smoothly from wherever it currently is instead of snapping backwards.
			_progress = 1f - _progress;
			_movingToB = false;
		}

		_waiting = false;
		_moving = true;
	}

	public void CallToB()
	{
		if (!_movingToB)
		{
			_progress = 1f - _progress;
			_movingToB = true;
		}

		_waiting = false;
		_moving = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_moving)
			return;

		float dt = (float)delta;

		if (_waiting)
		{
			_waitTimer += dt;
			if (_waitTimer >= WaitTimeAtPoints)
			{
				_waiting = false;
				_movingToB = !_movingToB;
				_progress = 0f;
			}

			return;
		}

		_progress = _legDistance > 0f
			? Mathf.Min(_progress + Speed * dt / _legDistance, 1f)
			: 1f;

		Vector2 from = _movingToB ? _pointA : _pointB;
		Vector2 to = _movingToB ? _pointB : _pointA;
		Vector2 basePosition = from.Lerp(to, _progress);

		float arc = JumpHeight > 0f ? -JumpHeight * Mathf.Sin(_progress * Mathf.Pi) : 0f;
		Position = basePosition + new Vector2(0f, arc);

		if (_progress >= 1f && AutoPatrol)
		{
			if (WaitTimeAtPoints > 0f)
			{
				_waiting = true;
				_waitTimer = 0f;
			}
			else
			{
				_movingToB = !_movingToB;
				_progress = 0f;
			}
		}
	}
}
