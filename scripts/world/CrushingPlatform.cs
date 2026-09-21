using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum CrusherTrigger
{
	// Falls when the player walks into the sensor zone underneath it (thwomp-style).
	PlayerBelow,
	// Falls on its own every IdleDuration seconds — a pure read-the-rhythm trap.
	Timed,
}

// Solid block that hangs at its placed position, shakes as a warning, drops until it meets the
// floor below and crushes whatever is under it, then rises back up and repeats.
//   Idle -> (trigger) -> Telegraph (shakes) -> Falling -> Grounded -> Rising -> Idle
// The node is the CENTRE of the block: place it where the block should hang.
//
// How far it falls is not authored: when the fall starts a ShapeCast2D looks straight down from the
// block's underside against FloorMask and the block stops where it hits — so it conforms to whatever
// floor is really there, capped at MaxFallDistance. Damage goes through the same Hazard area every
// other trap uses, but only while the block is actually falling (see CrushZone): by default the whole
// rectangle hurts (CrushWholeBlock) except its top skin, so it is still safe to stand on it, and a
// resting block hurts nobody. Turn CrushWholeBlock off for a thin under-side strip only.
// InstantKill defaults to true because "crush" reads as lethal; turn it off to use Damage +
// KnockbackForce instead (note that a block that lands on a still-living player pushes them out
// sideways — it is meant to kill, so prefer InstantKill).
//
// Visual: Fill is only a greybox stand-in (visible in the editor, hidden in-game like every other
// greybox piece). Put the painted look — a TileMapLayer, sprites, whatever — under Skin: it moves and
// shakes with the block. Cloning this scene and painting into Skin is the intended way to blend it
// with a tileset; the width variants (CrushingPlatformSmall/Medium/Large) are inherited scenes of
// this one that only override Size.
//
// [Tool] so Size and the detection/drop guides live-update in the editor; the cycle itself only runs
// in-game. The _initialized guard keeps the property setters from touching child nodes while Godot
// is still restoring the scene's saved values (before _Ready).
[Tool]
public partial class CrushingPlatform : AnimatableBody2D
{
	private enum State { Idle, Telegraph, Falling, Grounded, Rising }

	// Fired the frame the block hits the floor — hook dust, sound, etc. onto this.
	[Signal] public delegate void LandedEventHandler();

	private Vector2 _size = new(192f, 64f);
	private float _detectionDepth = 400f;
	private float _detectionMargin = 24f;
	private float _maxFallDistance = 640f;

	// Full size of the block, centred on the node.
	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = new Vector2(Mathf.Max(8f, value.X), Mathf.Max(8f, value.Y)); Rebuild(); }
	}

	[Export] public CrusherTrigger Trigger = CrusherTrigger.PlayerBelow;

	// PlayerBelow: how far under the block the player is noticed.
	[Export]
	public float DetectionDepth
	{
		get => _detectionDepth;
		set { _detectionDepth = value; Rebuild(); }
	}

	// PlayerBelow: extra width on each side of the block that also counts as "below".
	[Export]
	public float DetectionMargin
	{
		get => _detectionMargin;
		set { _detectionMargin = value; Rebuild(); }
	}

	// Seconds it hangs at the top before it can trigger again (PlayerBelow) or falls (Timed).
	[Export] public float IdleDuration = 1.5f;

	// Delay before the very first cycle, to desynchronise several crushers.
	[Export] public float StartOffset = 0f;

	// The warning: seconds of shaking before the drop, and how far it jitters sideways.
	[Export] public float TelegraphDuration = 0.5f;
	[Export] public float TelegraphShake = 2.5f;

	[ExportGroup("Fall")]
	[Export] public float FallAcceleration = 2600f;
	[Export] public float MaxFallSpeed = 1300f;

	// Hard cap on the drop; the floor probe never looks further than this.
	[Export]
	public float MaxFallDistance
	{
		get => _maxFallDistance;
		set { _maxFallDistance = value; Rebuild(); QueueRedraw(); }
	}

	// Physics layers the block lands on. Default is the World layer only, so it falls straight
	// through the player and enemies (the crush zone deals with them) instead of stopping on them.
	[Export(PropertyHint.Layers2DPhysics)] public uint FloorMask = 1;

	// Seconds it rests on the floor before starting back up, and how fast it climbs.
	[Export] public float GroundedDuration = 0.8f;
	[Export] public float RiseSpeed = 90f;

	[ExportGroup("Impact")]
	[Export] public float ImpactShakeStrength = 6f;
	[Export] public float ImpactShakeDuration = 0.25f;

	// The camera only shakes if the player is within this distance of the block.
	[Export] public float ImpactShakeRange = 700f;

	[ExportGroup("Damage")]
	// On: while the block is falling, touching ANY part of it hurts (underside, flanks) except the
	// top skin (TopSafeHeight), so standing on it is still safe. Off: only a thin strip under the
	// block hurts, so brushing past its side is harmless.
	[Export] public bool CrushWholeBlock = true;

	// Height of the top edge that never hurts, so a player riding on top of the block isn't crushed by it.
	[Export] public float TopSafeHeight = 8f;

	[Export] public bool InstantKill = true;
	[Export] public int Damage = 60;
	[Export] public float KnockbackForce = 300f;

	[ExportGroup("Greybox")]
	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private const float CrushZoneHeight = 12f;
	private const float SideInset = 4f;
	private const float CrushMargin = 3f;

	private CollisionShape2D _bodyShape;
	private Polygon2D _fill;
	private Area2D _sensor;
	private CollisionShape2D _sensorShape;
	private Hazard _hazard;
	private Area2D _crushZone;
	private CollisionShape2D _crushShape;
	private ShapeCast2D _floorProbe;

	private State _state = State.Idle;
	private float _stateTimer;
	private float _fallSpeed;
	private float _dropDistance;
	private Vector2 _restPosition;
	private bool _initialized;

	public override void _Ready()
	{
		_bodyShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_fill = GetNode<Polygon2D>("Fill");
		_sensor = GetNode<Area2D>("Sensor");
		_sensorShape = GetNode<CollisionShape2D>("Sensor/CollisionShape2D");
		_crushZone = GetNode<Area2D>("CrushZone");
		_crushShape = GetNode<CollisionShape2D>("CrushZone/CollisionShape2D");
		_floorProbe = GetNode<ShapeCast2D>("FloorProbe");

		if (!Engine.IsEditorHint())
		{
			_hazard = GetNode<Hazard>("CrushZone");
			_hazard.InstantKill = InstantKill;
			_hazard.Damage = Damage;
			_hazard.KnockbackForce = KnockbackForce;
			_hazard.HitWhileOverlapping = true;
			_hazard.SetPhysicsProcess(true);

			_restPosition = Position;
			_stateTimer = IdleDuration + StartOffset;
			SetCrushZoneActive(false);
		}

		_initialized = true;
		Rebuild();
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		float dt = (float)delta;

		switch (_state)
		{
			case State.Idle:
				_stateTimer -= dt;
				if (_stateTimer <= 0f && (Trigger == CrusherTrigger.Timed || PlayerIsBelow()))
					StartTelegraph();
				break;

			case State.Telegraph:
				_stateTimer -= dt;
				Position = _restPosition + new Vector2((float)GD.RandRange(-TelegraphShake, TelegraphShake), 0f);
				if (_stateTimer <= 0f)
					StartFall();
				break;

			case State.Falling:
				_fallSpeed = Mathf.Min(_fallSpeed + FallAcceleration * dt, MaxFallSpeed);
				float landY = _restPosition.Y + _dropDistance;
				float nextY = Position.Y + _fallSpeed * dt;
				if (nextY >= landY)
				{
					Position = new Vector2(_restPosition.X, landY);
					Land();
				}
				else
				{
					Position = new Vector2(_restPosition.X, nextY);
				}
				break;

			case State.Grounded:
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
					_state = State.Rising;
				break;

			case State.Rising:
				Position = Position.MoveToward(_restPosition, RiseSpeed * dt);
				if (Position.IsEqualApprox(_restPosition))
				{
					Position = _restPosition;
					_state = State.Idle;
					_stateTimer = IdleDuration;
				}
				break;
		}
	}

	private bool PlayerIsBelow()
	{
		foreach (Node2D body in _sensor.GetOverlappingBodies())
		{
			if (body.IsInGroup("player"))
				return true;
		}

		return false;
	}

	private void StartTelegraph()
	{
		_state = State.Telegraph;
		_stateTimer = TelegraphDuration;
	}

	private void StartFall()
	{
		Position = _restPosition;

		// Measure the drop now, against the floor as it is at this moment. Fraction is how much of
		// the full-length cast is free before the probe touches something on FloorMask.
		_floorProbe.CollisionMask = FloorMask;
		_floorProbe.ForceShapecastUpdate();
		_dropDistance = _floorProbe.IsColliding()
			? MaxFallDistance * _floorProbe.GetClosestCollisionSafeFraction()
			: MaxFallDistance;

		if (_dropDistance < 1f)
		{
			// Already resting on something: nothing to fall through, try again after a pause.
			_state = State.Idle;
			_stateTimer = IdleDuration;
			return;
		}

		_state = State.Falling;
		_fallSpeed = 0f;
		SetCrushZoneActive(true);
	}

	private void Land()
	{
		SetCrushZoneActive(false);
		_state = State.Grounded;
		_stateTimer = GroundedDuration;

		if (ImpactShakeStrength > 0f
			&& GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player
			&& player.GlobalPosition.DistanceTo(GlobalPosition) <= ImpactShakeRange)
		{
			player.ShakeCamera(ImpactShakeStrength, ImpactShakeDuration);
		}

		EmitSignal(SignalName.Landed);
	}

	// Deferred: toggling a collision shape's Disabled flag in the middle of a physics step can be
	// rejected by the engine ("can't change this state while flushing queries").
	private void SetCrushZoneActive(bool active)
	{
		_crushShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !active);
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;

		Vector2 half = _size * 0.5f;

		// Where the player is noticed (PlayerBelow) — yellow.
		var detectColor = new Color(1f, 0.85f, 0.3f, 0.6f);
		float detectWidth = _size.X + _detectionMargin * 2f;
		DrawRect(new Rect2(-detectWidth * 0.5f, half.Y, detectWidth, _detectionDepth), detectColor, false, 1f);

		// The longest the block can fall — orange line down from the underside, with a tick.
		var dropColor = new Color(1f, 0.5f, 0.2f, 0.8f);
		Vector2 bottom = new(0f, half.Y);
		Vector2 landing = bottom + new Vector2(0f, _maxFallDistance);
		DrawLine(bottom, landing, dropColor, 1.5f);
		DrawLine(landing - new Vector2(half.X, 0f), landing + new Vector2(half.X, 0f), dropColor, 1.5f);
	}

	private void Rebuild()
	{
		if (!_initialized)
			return;

		Vector2 half = _size * 0.5f;
		float stripWidth = Mathf.Max(4f, _size.X - SideInset * 2f);

		// Always fresh shapes: the ones in the .tscn are shared by every instance (and by the width
		// variants), so resizing them in place would resize every crusher in the game.
		_bodyShape.Shape = new RectangleShape2D { Size = _size };

		_fill.Visible = GameConfig.GreyboxFillVisible;
		_fill.Color = FillColor;
		_fill.Polygon = new[]
		{
			new Vector2(-half.X, -half.Y), new Vector2(half.X, -half.Y),
			new Vector2(half.X, half.Y), new Vector2(-half.X, half.Y),
		};

		if (CrushWholeBlock)
		{
			// Whole rectangle, a few px proud of every side so a body pressed flat against the block
			// (which physically can't overlap it) still counts as touching it. The top TopSafeHeight
			// is left out so the block's own top surface stays safe to stand on.
			float safe = Mathf.Clamp(TopSafeHeight, 0f, _size.Y - 1f);
			float zoneHeight = _size.Y - safe + CrushMargin;
			_crushZone.Position = new Vector2(0f, -half.Y + safe + zoneHeight * 0.5f);
			_crushShape.Shape = new RectangleShape2D { Size = new Vector2(_size.X + CrushMargin * 2f, zoneHeight) };
		}
		else
		{
			// Crush strip: centred on the underside, slightly inset from the sides so brushing
			// against the flank of the block never counts as being crushed.
			_crushZone.Position = new Vector2(0f, half.Y);
			_crushShape.Shape = new RectangleShape2D { Size = new Vector2(stripWidth, CrushZoneHeight) };
		}

		// Floor probe: a 2px-tall sliver along the underside, cast straight down.
		_floorProbe.Position = new Vector2(0f, half.Y - 1f);
		_floorProbe.Shape = new RectangleShape2D { Size = new Vector2(stripWidth, 2f) };
		_floorProbe.TargetPosition = new Vector2(0f, _maxFallDistance);

		// Player sensor: a tall box hanging under the block.
		_sensor.Position = new Vector2(0f, half.Y + _detectionDepth * 0.5f);
		_sensorShape.Shape = new RectangleShape2D { Size = new Vector2(_size.X + _detectionMargin * 2f, _detectionDepth) };

		QueueRedraw();
	}
}
