using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// DefeatWall's sibling for floors, triggered by contact: this node is just a detection zone
// (Width x Height) — the actual floor is whatever you attach: OverlayLayer (the TileMapLayer painted
// as the floor, its tile physics included) and/or AttachedCollision (a CollisionShape2D in the map).
// The player enters the zone → the floor shakes for TriggerDelay (the tell, time to jump off) → an
// explosion plays, AttachedCollision turns off and OverlayLayer loses its collision and fades out.
// Persistent (default): broken for good for this save, like a one-way drop/shortcut. Off: it's back
// every time the scene loads.
// [Tool] so Width/Height/ShowFill preview live in the editor.
[Tool]
public partial class CrumbleFloor : Area2D
{
	private const string ExplosionScenePath = "res://scenes/world/Explosion.tscn";

	private float _width = 96f;

	// Detection zone — place it over (just above) the floor's surface.
	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(8f, value); Rebuild(); }
	}

	private float _height = 16f;

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(4f, value); Rebuild(); }
	}

	// The TileMapLayer painted as this floor — its collision turns off and it fades out when it breaks.
	[Export] public TileMapLayer OverlayLayer;

	// The collision that actually holds the player up (if it isn't the OverlayLayer's tiles) — switched
	// off when it breaks.
	[Export] public CollisionShape2D AttachedCollision;

	// Time between entering the zone and the collapse, shaking meanwhile. 0 = breaks instantly.
	[Export] public float TriggerDelay = 0.5f;
	[Export] public float ShakeAmplitude = 2f;
	[Export] public float OverlayFadeDuration = 0.25f;
	[Export] public float ExplosionScale = 1f;

	[Export] public bool Persistent = true;
	// Identified by its node path unless set — set it if the floor gets renamed/moved after a save
	// already knows it.
	[Export] public string CustomPersistenceId = "";

	[Export] public Color FillColor = new(0.5f, 0.4f, 0.3f, 0.6f);

	private bool _showFill;

	[Export]
	public bool ShowFill
	{
		get => _showFill;
		set { _showFill = value; Rebuild(); }
	}

	private string _persistenceId;
	private bool _triggered;

	public override void _Ready()
	{
		Rebuild();

		if (Engine.IsEditorHint())
			return;

		_persistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;
		if (Persistent && SaveManager.Instance.IsGateOpened(_persistenceId))
		{
			RemoveSilently();
			return;
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || !body.IsInGroup("player"))
			return;
		_triggered = true;
		Crumble();
	}

	private async void Crumble()
	{
		if (Persistent)
			SaveManager.Instance.MarkGateOpened(_persistenceId);

		if (TriggerDelay > 0f)
		{
			Shake(TriggerDelay);
			await ToSignal(GetTree().CreateTimer(TriggerDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || !IsInsideTree())
				return;
		}

		DisableCollisions();

		var explosion = GD.Load<PackedScene>(ExplosionScenePath).Instantiate<Node2D>();
		if (explosion is Explosion boom)
			boom.TargetScale = ExplosionScale;
		GetTree().CurrentScene.AddChild(explosion);
		explosion.GlobalPosition = GlobalPosition;

		RevealBehindOverlay();
		QueueFree();
	}

	// Jitters the painted floor (and the debug fill) around its rest position until the collapse.
	private void Shake(float duration)
	{
		const float step = 0.04f;
		int steps = Mathf.Max(1, Mathf.RoundToInt(duration / step));
		if (IsInstanceValid(OverlayLayer))
			ShakeNode(OverlayLayer, steps, step);
		if (GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
			ShakeNode(fill, steps, step);
	}

	private void ShakeNode(Node2D node, int steps, float step)
	{
		Vector2 rest = node.Position;
		Tween tween = node.CreateTween();
		for (int i = 0; i < steps; i++)
		{
			Vector2 offset = new((float)GD.RandRange(-ShakeAmplitude, ShakeAmplitude), (float)GD.RandRange(-ShakeAmplitude, ShakeAmplitude) * 0.5f);
			tween.TweenProperty(node, "position", rest + offset, step);
		}
		tween.TweenProperty(node, "position", rest, step);
	}

	private void DisableCollisions()
	{
		if (IsInstanceValid(AttachedCollision))
			AttachedCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
	}

	private void RemoveSilently()
	{
		DisableCollisions();
		if (IsInstanceValid(OverlayLayer))
			OverlayLayer.QueueFree();
		QueueFree();
	}

	private void RevealBehindOverlay()
	{
		if (!IsInstanceValid(OverlayLayer))
			return;

		OverlayLayer.CollisionEnabled = false;
		if (OverlayFadeDuration <= 0f)
		{
			OverlayLayer.QueueFree();
			return;
		}

		// Tween lives on the layer so it keeps running after this node frees itself.
		Tween tween = OverlayLayer.CreateTween();
		tween.TweenProperty(OverlayLayer, "modulate:a", 0f, OverlayFadeDuration);
		tween.TweenCallback(Callable.From(OverlayLayer.QueueFree));
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		Vector2 size = new(_width, _height);
		// Fresh shape every time — the .tscn's RectangleShape2D is shared between instances.
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D zone)
			zone.Shape = new RectangleShape2D { Size = size };

		if (GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
		{
			fill.Visible = _showFill;
			fill.Color = FillColor;
			Vector2 half = size / 2f;
			fill.Polygon = new[]
			{
				new Vector2(-half.X, -half.Y),
				new Vector2(half.X, -half.Y),
				new Vector2(half.X, half.Y),
				new Vector2(-half.X, half.Y),
			};
		}
	}
}
