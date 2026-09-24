using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// SecretWall's sibling: a blocking wall that opens when enemies die instead of when it's hit.
// Kill every enemy listed in Enemies (a boss, or "clear these three bandits") → an explosion plays
// at the wall, its collision turns off and its OverlayLayer (the TileMapLayer painted over the
// passage to hide it) fades out — same reveal as SecretWall. Opened for good for this save
// (SaveManager's opened-gates set, like Gate.cs): common enemies respawn after resting, the
// passage doesn't close again. On load, an already-opened wall just never appears.
// [Tool] so Width/Height/ShowFill preview live in the editor, like SecretWall.
[Tool]
public partial class DefeatWall : StaticBody2D
{
	private const string ExplosionScenePath = "res://scenes/world/Explosion.tscn";

	// Enemies (Enemy and subclasses — bosses too) that all have to die for the wall to open.
	[Export] public Godot.Collections.Array<NodePath> Enemies = new();

	private float _width = 64f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(8f, value); Rebuild(); }
	}

	private float _height = 96f;

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(8f, value); Rebuild(); }
	}

	// The TileMapLayer painted over the passage to hide it — removed when the wall opens.
	[Export] public TileMapLayer OverlayLayer;

	// Off = this wall's own rectangle doesn't block anything (e.g. the OverlayLayer's tile physics is
	// what blocks, or it's purely a visual reveal).
	private bool _useOwnCollision = true;

	[Export]
	public bool UseOwnCollision
	{
		get => _useOwnCollision;
		set { _useOwnCollision = value; Rebuild(); }
	}

	// Optional collision elsewhere in the map (any CollisionShape2D) that also gets switched off when
	// the wall opens — for a blocker you've already built in the scene.
	[Export] public CollisionShape2D AttachedCollision;

	[Export] public float OverlayFadeDuration = 0.25f;
	// Pause between the last kill and the wall blowing open (lets the boss's own death play first).
	[Export] public float OpenDelay = 0.6f;
	[Export] public float ExplosionScale = 1f;

	// Identified by its node path unless set — set it if the wall gets renamed/moved after a save
	// already knows it.
	[Export] public string CustomPersistenceId = "";

	[Export] public Color FillColor = new(0.55f, 0.45f, 0.5f, 0.6f);

	private bool _showFill;

	[Export]
	public bool ShowFill
	{
		get => _showFill;
		set { _showFill = value; Rebuild(); }
	}

	private string _persistenceId;
	private int _remaining;
	private bool _opening;

	public override void _Ready()
	{
		Rebuild();

		if (Engine.IsEditorHint())
			return;

		_persistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;
		if (SaveManager.Instance.IsGateOpened(_persistenceId))
		{
			RemoveSilently();
			return;
		}

		// Deferred: the listed enemies may sit later in the tree and haven't run their own _Ready
		// (which wires up Stats, or frees an already-defeated one) yet.
		CallDeferred(MethodName.WatchEnemies);
	}

	private void WatchEnemies()
	{
		_remaining = 0;
		foreach (NodePath path in Enemies)
		{
			Enemy enemy = GetNodeOrNull<Enemy>(path);
			// Missing or already on its way out (defeated earlier) → counts as dead.
			if (enemy is null || enemy.IsQueuedForDeletion() || enemy.Stats is null)
				continue;

			_remaining++;
			enemy.Stats.Died += OnEnemyDied;
		}

		if (_remaining == 0)
			Open();
	}

	private void OnEnemyDied()
	{
		_remaining--;
		if (_remaining <= 0)
			Open();
	}

	private async void Open()
	{
		if (_opening)
			return;
		_opening = true;
		SaveManager.Instance.MarkGateOpened(_persistenceId);

		if (OpenDelay > 0f)
			await ToSignal(GetTree().CreateTimer(OpenDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		DisableCollisions();
		Visible = false;

		var explosion = GD.Load<PackedScene>(ExplosionScenePath).Instantiate<Node2D>();
		if (explosion is Explosion boom)
			boom.TargetScale = ExplosionScale;
		GetTree().CurrentScene.AddChild(explosion);
		explosion.GlobalPosition = GlobalPosition;

		RevealBehindOverlay();
		QueueFree();
	}

	private void DisableCollisions()
	{
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
			collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
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

		// Tween lives on the layer so it keeps running after this wall frees itself.
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
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
		{
			collision.Shape = new RectangleShape2D { Size = size };
			collision.Disabled = !_useOwnCollision;
		}

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
