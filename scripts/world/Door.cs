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

	public async void Open()
	{
		if (_isOpen)
			return;

		_isOpen = true;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		string axis = Horizontal ? "position:x" : "position:y";
		float target = Horizontal ? _body.Position.X + SlideDistance : _body.Position.Y - SlideDistance;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_body, axis, target, SlideDistance / OpenSpeed);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
