using Godot;

namespace Metroidvania.World;

public enum ElevatorSwitches
{
	None,
	AtBottom,
	AtTop,
	Both,
}

// [Tool] so RopeExtend's length previews live in the editor at rest — placed at _bottomPosition
// (its resting spot), the car is at its furthest from the shaft's anchor, so the rope should
// already read as fully extended while designing the level, not only after pressing Play.
[Tool]
public partial class Elevator : AnimatableBody2D
{
	private float _travelDistance = 230f;

	// Setter recomputes _topPosition and redraws the editor gizmo (see _Draw) live, so dragging
	// this value in the Inspector immediately shows where the car will land instead of only after
	// pressing Play — same "tune live" idea as MeasuringRuler's own Length.
	[Export]
	public float TravelDistance
	{
		get => _travelDistance;
		set
		{
			_travelDistance = value;
			RefreshTravelPoints();
			QueueRedraw();
		}
	}

	private float _drawnDistance;

	// Independent of TravelDistance: how far above _bottomPosition the rope should visually reach
	// when fully retracted (i.e. where RopeExtend stops growing) — Platform's own baked rope art
	// still needs an anchor point to read as "connected to", but that point doesn't have to be
	// where the car actually stops (a shaft can look taller than the car's real travel range).
	// 0 (default) means "not customized" and falls back to TravelDistance, so existing elevators
	// look exactly as before unless this is set explicitly.
	[Export]
	public float DrawnDistance
	{
		get => _drawnDistance;
		set
		{
			_drawnDistance = value;
			RefreshTravelPoints();
			QueueRedraw();
		}
	}

	[Export] public float Speed = 80f;

	// Where the car actually starts along its travel, 0 = _bottomPosition, 1 = _topPosition —
	// defaults to the middle so an elevator dropped into a level doesn't silently default to
	// "resting at the bottom" the way a bool StartAtTop would. The node is still always placed at
	// _bottomPosition in the editor (keeps TravelDistance/the rope gizmo meaningful — see class
	// doc); this only moves the car at runtime, after _bottomPosition/_topPosition are derived, so
	// the placed anchor is never overwritten.
	[Export(PropertyHint.Range, "0,1,0.01")] public float PointStart = 0.5f;

	// Opt-in (defaults to None): spawns Lever.tscn instances at runtime, already wired to
	// CallToBottom()/CallToTop() — no manual signal-connecting in the editor needed. A switch at
	// the bottom summons the car down, one at the top summons it up. Spawned as siblings
	// (children of this elevator's parent, not of the elevator itself) so they stay put at
	// their landing instead of riding along. Leave as None and wire a Lever by hand (via its
	// ElevatorPath, see Lever.cs) if a single lever should only ever call one direction, or if
	// it needs to sit somewhere these auto-placed defaults don't fit.
	[Export] public ElevatorSwitches Switches = ElevatorSwitches.None;

	// Local offset (relative to the bottom/top landing) so the lever sits beside the shaft
	// instead of inside it.
	[Export] public Vector2 SwitchOffset = new(40f, 0f);

	// On: the car shuttles between _bottomPosition/_topPosition on its own, no lever or player
	// contact needed — reverses direction every time it fully reaches an end. Doesn't disable the
	// TriggerZone/CallToTop/CallToBottom paths, so a lever can still cut in early if both are set up.
	[Export] public bool AutoTravel = false;

	// How long the car waits at each end before reversing, so it doesn't instantly snap back and
	// forth like a bouncing ball — 0 disables the pause entirely.
	[Export] public float AutoTravelPause = 1f;

	private float _autoTravelTimer;

	private Vector2 _bottomPosition;
	private Vector2 _topPosition;
	private Vector2 _target;

	// RopeExtend (ascensorsogaextiende.png) is the same rope/chain texture as the top of Platform
	// (ascensor.png) itself — Platform's own art already reaches the shaft's fixed anchor point
	// when the car sits at _topPosition, so no extension is needed there. As the car descends,
	// the gap between the anchor and the car grows; RopeExtend is grown to fill exactly that gap
	// every frame by reading Platform's own live Position/Scale (not a duplicated tunable), so
	// nudging Platform by hand in the editor keeps the rope seamed to it automatically.
	private Sprite2D _platformSprite;
	private Sprite2D _ropeExtend;

	// Platform's baked rope art always points toward the physically-higher endpoint (that's the
	// fixed shaft anchor, regardless of which side of _bottomPosition it falls on — a negative
	// TravelDistance/DrawnDistance flips which one that is, e.g. an elevator placed at the top of
	// its shaft that travels down). Derived from DrawnDistance (see its doc), not TravelDistance —
	// picking the smaller of the two candidate Y values keeps the math correct in both orientations.
	private float _anchorY;

	// Guards RefreshTravelPoints against the TravelDistance setter firing while Godot is still
	// restoring this node's saved values (before _Ready runs), when _bottomPosition isn't set yet —
	// same reentrancy gotcha the other [Tool] pieces (RopeAccordion, MovingPlatformAccordion) guard
	// against before touching state that only _Ready initializes.
	private bool _initialized;

	public override void _Ready()
	{
		_bottomPosition = Position;
		_topPosition = Position - new Vector2(0, TravelDistance);
		RecomputeAnchorY();
		_target = _bottomPosition;
		_initialized = true;

		_platformSprite = GetNode<Sprite2D>("Platform");
		_ropeExtend = GetNode<Sprite2D>("RopeExtend");
		UpdateRopeExtend();
		QueueRedraw();

		if (Engine.IsEditorHint())
			return;

		if (PointStart != 0f)
		{
			Vector2 startPosition = _bottomPosition.Lerp(_topPosition, Mathf.Clamp(PointStart, 0f, 1f));
			Position = startPosition;
			_target = startPosition;
			UpdateRopeExtend();
		}

		Area2D triggerZone = GetNode<Area2D>("TriggerZone");
		triggerZone.BodyEntered += OnTriggerEntered;

		if (Switches != ElevatorSwitches.None)
			SpawnSwitches();
	}

	// Re-derives _topPosition from the fixed _bottomPosition (the car's placed/resting spot, never
	// itself changed by TravelDistance) so editing TravelDistance in the Inspector always measures
	// from where the elevator is actually anchored, not from wherever Position happens to be mid-ride.
	private void RefreshTravelPoints()
	{
		if (!_initialized)
			return;

		_topPosition = _bottomPosition - new Vector2(0, _travelDistance);
		RecomputeAnchorY();

		if (_platformSprite is not null && _ropeExtend is not null)
			UpdateRopeExtend();
	}

	private void RecomputeAnchorY()
	{
		float effectiveDrawnDistance = _drawnDistance != 0f ? _drawnDistance : _travelDistance;
		Vector2 visualTopPosition = _bottomPosition - new Vector2(0, effectiveDrawnDistance);
		_anchorY = Mathf.Min(_bottomPosition.Y, visualTopPosition.Y);
	}

	private void SpawnSwitches()
	{
		Node parent = GetParent() ?? this;

		if (Switches is ElevatorSwitches.AtBottom or ElevatorSwitches.Both)
			LeverSpawner.SpawnAt(parent, _bottomPosition + SwitchOffset, CallToBottom);

		if (Switches is ElevatorSwitches.AtTop or ElevatorSwitches.Both)
			LeverSpawner.SpawnAt(parent, _topPosition + SwitchOffset, CallToTop);
	}

	private void OnTriggerEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_target = _target == _topPosition ? _bottomPosition : _topPosition;
	}

	public void CallToTop() => _target = _topPosition;
	public void CallToBottom() => _target = _bottomPosition;

	public override void _PhysicsProcess(double delta)
	{
		// Editor preview only needs the rope-length visual (see class doc) — moving the body here
		// would fight a manual drag in the 2D viewport, same reasoning as Puente's own editor branch.
		if (Engine.IsEditorHint())
		{
			UpdateRopeExtend();
			return;
		}

		Position = Position.MoveToward(_target, Speed * (float)delta);
		UpdateRopeExtend();

		if (!AutoTravel)
			return;

		if (Position != _target)
		{
			_autoTravelTimer = 0f;
			return;
		}

		_autoTravelTimer += (float)delta;
		if (_autoTravelTimer >= AutoTravelPause)
		{
			_autoTravelTimer = 0f;
			_target = _target == _topPosition ? _bottomPosition : _topPosition;
		}
	}

	// RopeExtend's region_rect is sampled with wraparound (texture_repeat = Enabled, set on the
	// node in the .tscn) starting from row 0 at its BOTTOM edge — row 0 of ascensorsogaextiende.png
	// is pixel-identical to row 0 of ascensor.png (it's literally the same cropped rope art), so the
	// seam against Platform's own rope never shows a jump no matter how tall the region grows.
	private void UpdateRopeExtend()
	{
		float scale = _platformSprite.Scale.Y;
		float gameHeight = Position.Y - _anchorY;

		if (gameHeight <= 0.5f)
		{
			_ropeExtend.Visible = false;
			return;
		}

		_ropeExtend.Visible = true;
		_ropeExtend.Scale = new Vector2(scale, scale);

		float pxHeight = gameHeight / scale;
		float texWidth = _ropeExtend.Texture.GetWidth();
		_ropeExtend.RegionRect = new Rect2(0f, -pxHeight, texWidth, pxHeight);
		_ropeExtend.Position = new Vector2(_platformSprite.Position.X, _platformSprite.Position.Y - gameHeight);
	}

	// Editor-only gizmo (same idea as MeasuringRuler): a line from the car's resting spot up to
	// where TravelDistance actually lands it, with a tick + px label at the top — lets TravelDistance
	// be tuned against the level geometry (e.g. lining the top tick up with a landing ledge) without
	// pressing Play. A second, dashed-looking line marks DrawnDistance whenever it's been customized
	// (non-zero and different from TravelDistance) — otherwise the rope silently reaches exactly as
	// far as the travel tick already shown, so drawing a duplicate line on top would just be clutter.
	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;

		var color = new Color(0.3f, 0.9f, 1f, 0.85f);
		Vector2 top = new(0f, -TravelDistance);
		const float tickHalfWidth = 45f; // matches the car's own collision half-width

		DrawLine(Vector2.Zero, top, color, 2f);
		DrawLine(top - new Vector2(tickHalfWidth, 0f), top + new Vector2(tickHalfWidth, 0f), color, 2f);
		DrawString(ThemeDB.FallbackFont, top + new Vector2(tickHalfWidth + 4f, 4f), $"{TravelDistance:0}px",
			HorizontalAlignment.Left, -1f, 14, color);

		if (_drawnDistance != 0f && !Mathf.IsEqualApprox(_drawnDistance, _travelDistance))
		{
			var ropeColor = new Color(1f, 0.7f, 0.2f, 0.85f);
			Vector2 ropeTop = new(0f, -_drawnDistance);
			const float ropeTickHalfWidth = 30f;

			DrawLine(Vector2.Zero, ropeTop, ropeColor, 1.5f);
			DrawLine(ropeTop - new Vector2(ropeTickHalfWidth, 0f), ropeTop + new Vector2(ropeTickHalfWidth, 0f), ropeColor, 1.5f);
			DrawString(ThemeDB.FallbackFont, ropeTop - new Vector2(ropeTickHalfWidth + 70f, -4f), $"soga {_drawnDistance:0}px",
				HorizontalAlignment.Left, -1f, 14, ropeColor);
		}
	}
}
