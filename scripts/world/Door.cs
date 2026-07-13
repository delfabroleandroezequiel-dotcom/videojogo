using Godot;

namespace Metroidvania.World;

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

	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Area2D _detector;
	private bool _isOpen;

	public override void _Ready()
	{
		_body = GetNode<StaticBody2D>("Body");
		_collision = GetNode<CollisionShape2D>("Body/CollisionShape2D");
		_detector = GetNode<Area2D>("Detector");
		_detector.BodyEntered += OnBodyEntered;
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
