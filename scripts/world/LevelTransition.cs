using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// [Tool] so Width/Height preview live in the editor — same "tune live" idea as Door's own Width.
[Tool]
public partial class LevelTransition : Area2D
{
	[Export] public string TargetScenePath = "";
	[Export] public Vector2 TargetSpawnPosition = Vector2.Zero;
	[Export] public bool RequireInteract = false;
	[Export] public bool RememberOriginForReturn = false;
	[Export] public bool UseStoredReturnPosition = false;

	private float _width = 32f;
	private float _height = 106f;

	// How wide/tall the trigger zone is — lets one instance span a wide open gap and another a
	// narrow corridor without hand-editing the shared CollisionShape2D (which would resize every
	// other LevelTransition instance still pointing at that same resource).
	[Export]
	public float Width
	{
		get => _width;
		set
		{
			_width = value;
			RefreshShape();
		}
	}

	[Export]
	public float Height
	{
		get => _height;
		set
		{
			_height = value;
			RefreshShape();
		}
	}

	private bool _triggered;
	private bool _playerInRange;
	private Node2D _playerBody;
	private Label _interactPrompt;
	private CollisionShape2D _collision;
	private Polygon2D _visual;
	private bool _initialized;

	public override void _Ready()
	{
		_collision = GetNode<CollisionShape2D>("CollisionShape2D");
		_visual = GetNode<Polygon2D>("Visual");
		_initialized = true;
		RefreshShape();

		if (Engine.IsEditorHint())
			return;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		_interactPrompt = GetNodeOrNull<Label>("InteractPrompt");
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	// New RectangleShape2D/polygon each call, never mutating the existing Shape2D in place — that
	// would resize it on every other LevelTransition instance still sharing the same resource.
	private void RefreshShape()
	{
		if (!_initialized)
			return;

		_collision.Shape = new RectangleShape2D { Size = new Vector2(_width, _height) };

		float halfWidth = _width / 2f;
		float halfHeight = _height / 2f;
		_visual.Scale = Vector2.One;
		_visual.Polygon = new[]
		{
			new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
			new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
		};
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!RequireInteract || !_playerInRange || _triggered)
			return;

		if (@event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			Trigger(_playerBody);
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || !body.IsInGroup("player") || string.IsNullOrEmpty(TargetScenePath))
			return;

		if (RequireInteract)
		{
			_playerInRange = true;
			_playerBody = body;
			if (_interactPrompt is not null)
				_interactPrompt.Visible = true;
			return;
		}

		Trigger(body);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_playerBody = null;
		if (_interactPrompt is not null)
			_interactPrompt.Visible = false;
	}

	private void Trigger(Node2D body)
	{
		_triggered = true;

		if (RememberOriginForReturn)
			SaveManager.Instance.PendingReturnPosition = GlobalPosition;

		SaveManager.Instance.PendingSpawnPosition =
			UseStoredReturnPosition && SaveManager.Instance.PendingReturnPosition.HasValue
				? SaveManager.Instance.PendingReturnPosition
				: TargetSpawnPosition;

		SceneFader.ChangeSceneWithFade(GetTree(), TargetScenePath);
	}
}
