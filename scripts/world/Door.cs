using Godot;

namespace Metroidvania.World;

// [Tool] so Width previews live in the editor (same "tune live" idea as Elevator's TravelDistance)
// instead of only after pressing Play.
[Tool]
public partial class Door : Node2D
{
	[Export] public string RequiredKeyId = "";
	[Export] public float OpenSpeed = 150f;
	[Export] public float SlideDistance = 200f;

	// When true, the door never opens on its own from the player just walking up to it — it
	// only opens when something calls Open() directly, e.g. a Lever's Activated signal wired
	// to this node in the editor (Node > Signals). Use this for lever/switch-gated doors.
	[Export] public bool RequiresLever;

	// False (default) = vertical gate blocking a horizontal corridor, slides up to open.
	// True = horizontal gate/hatch blocking a vertical shaft, slides sideways to open.
	// Same script either way — only the slide axis changes; give the Body a wide/short shape
	// instead of narrow/tall for the horizontal variant.
	[Export] public bool Horizontal;

	// Set it to remember the door as open for this save once it opens (shortcut doors: opened once
	// from the far side, open forever after) — same OpenedGates record Gate and CrumbleFloor use.
	// Empty (default) = not remembered, closed again every time the scene loads.
	[Export] public string PersistenceId = "";

	// Starts slid open (passable); Close() brings it down. CloseSpeed is the slam, faster than opening.
	[Export] public bool StartsOpen;
	[Export] public float CloseSpeed = 700f;

	private float _width = 200f;

	// The span across the passage the door blocks (200 matches the original hardcoded shapes,
	// so existing Door/DoorHorizontal instances look unchanged unless this is set explicitly).
	// Runs along Y for a vertical door, X for a horizontal one — see Horizontal.
	[Export]
	public float Width
	{
		get => _width;
		set
		{
			_width = value;
			RefreshSize();
		}
	}

	private const float BodyThickness = 32f;
	private const float DetectorThickness = 64f;

	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Area2D _detector;
	private CollisionShape2D _detectorCollision;
	private Polygon2D _visual;
	private bool _isOpen;
	private bool _initialized;

	public override void _Ready()
	{
		_body = GetNode<StaticBody2D>("Body");
		_collision = GetNode<CollisionShape2D>("Body/CollisionShape2D");
		_visual = GetNode<Polygon2D>("Body/Visual");
		_detector = GetNode<Area2D>("Detector");
		_detectorCollision = GetNode<CollisionShape2D>("Detector/CollisionShape2D");
		_initialized = true;
		RefreshSize();

		if (Engine.IsEditorHint())
			return;

		_detector.BodyEntered += OnBodyEntered;

		if (StartsOpen)
		{
			_isOpen = true;
			_collision.Disabled = true;
			_body.Position += Horizontal ? new Vector2(SlideDistance, 0f) : new Vector2(0f, -SlideDistance);
		}
		else if (!string.IsNullOrEmpty(PersistenceId) && Metroidvania.Save.SaveManager.Instance.IsGateOpened(PersistenceId))
		{
			// Already opened earlier in this save: start open, no slide.
			_isOpen = true;
			_collision.Disabled = true;
			_body.Position += Horizontal ? new Vector2(SlideDistance, 0f) : new Vector2(0f, -SlideDistance);
		}
	}

	// New RectangleShape2D/polygon instances every call, never mutating the existing Shape2D in
	// place — that would resize it on every other Door instance still sharing the same resource.
	private void RefreshSize()
	{
		if (!_initialized)
			return;

		Vector2 bodySize = Horizontal ? new Vector2(_width, BodyThickness) : new Vector2(BodyThickness, _width);
		Vector2 detectorSize = Horizontal
			? new Vector2(_width, DetectorThickness)
			: new Vector2(DetectorThickness, _width);

		_collision.Shape = new RectangleShape2D { Size = bodySize };
		_detectorCollision.Shape = new RectangleShape2D { Size = detectorSize };

		float halfWidth = _width / 2f;
		const float halfThickness = BodyThickness / 2f;
		_visual.Polygon = Horizontal
			? new[]
			{
				new Vector2(-halfWidth, -halfThickness), new Vector2(halfWidth, -halfThickness),
				new Vector2(halfWidth, halfThickness), new Vector2(-halfWidth, halfThickness),
			}
			: new[]
			{
				new Vector2(-halfThickness, -halfWidth), new Vector2(halfThickness, -halfWidth),
				new Vector2(halfThickness, halfWidth), new Vector2(-halfThickness, halfWidth),
			};
	}

	private void OnBodyEntered(Node2D body)
	{
		if (RequiresLever || _isOpen || !body.IsInGroup("player"))
			return;

		// TODO: once a player key/lever inventory exists, check it here instead of always failing.
		if (!string.IsNullOrEmpty(RequiredKeyId))
			return;

		Open();
	}

	// Mechanism hook (PressurePlate's default TargetMethod): toggles — slams an open door, opens a closed one.
	public void Activate()
	{
		if (_isOpen)
			Close();
		else
			Open();
	}

	// Slams shut (an ambush: a pressure plate closing the way back). Starts open with StartsOpen.
	// The collision comes back once the slide has finished, so it never closes onto the player.
	public async void Close()
	{
		if (!_isOpen)
			return;
		_isOpen = false;
		string axis = Horizontal ? "position:x" : "position:y";
		float target = Horizontal ? _body.Position.X - SlideDistance : _body.Position.Y + SlideDistance;
		Metroidvania.Shared.Sfx.PlayAt(this, "World/Doors", "Portcullis Gate");
		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_body, axis, target, SlideDistance / CloseSpeed).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		await ToSignal(tween, Tween.SignalName.Finished);
		if (IsInstanceValid(this))
			_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
	}

	public async void Open()
	{
		if (_isOpen)
			return;

		_isOpen = true;
		if (!string.IsNullOrEmpty(PersistenceId))
			Metroidvania.Save.SaveManager.Instance.MarkGateOpened(PersistenceId);
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		string axis = Horizontal ? "position:x" : "position:y";
		float target = Horizontal ? _body.Position.X + SlideDistance : _body.Position.Y - SlideDistance;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_body, axis, target, SlideDistance / OpenSpeed);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
