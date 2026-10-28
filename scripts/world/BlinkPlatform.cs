using Godot;

namespace Metroidvania.World;

// Timed platform that blinks in and out of existence: solid for OnTime, flickers for WarningTime,
// then gone (only a faint outline left) for OffTime. StartDelay offsets the cycle so a row of
// them can hand you along in sequence, Mega Man style. OneWay makes it a ledge you can jump up
// through. Drawn in code (dark slab + neon edge) like the rest of the neon kit; [Tool] so the
// size previews live in the editor.
[Tool]
public partial class BlinkPlatform : StaticBody2D
{
	private Vector2 _size = new(96f, 16f);

	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = new Vector2(Mathf.Max(16f, value.X), Mathf.Max(8f, value.Y)); QueueRedraw(); }
	}

	[Export] public bool OneWay = true;

	[ExportGroup("Timing")]
	[Export] public float OnTime = 1.6f;
	// The tail end of OnTime spent flickering, still solid, as the warning it's about to vanish.
	[Export] public float WarningTime = 0.5f;
	[Export] public float OffTime = 1.2f;
	// Seconds into the cycle this instance starts at.
	[Export] public float StartDelay;

	[ExportGroup("Look")]
	[Export] public Color NeonColor = new(0.55f, 0.75f, 1f, 1f);

	private CollisionShape2D _collision;
	private Node2D _visual;
	private float _cycleTime;
	private bool _solid = true;

	private float Cycle => Mathf.Max(0.05f, OnTime + OffTime);

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		Build();
		_cycleTime = Mathf.PosMod(StartDelay, Cycle);
		Refresh(force: true);
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		DrawRect(new Rect2(-_size / 2f, _size), NeonColor with { A = 0.5f });
		DrawRect(new Rect2(-_size / 2f, _size), NeonColor, false, 2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		_cycleTime = Mathf.PosMod(_cycleTime + (float)delta, Cycle);
		Refresh(force: false);
	}

	private void Refresh(bool force)
	{
		bool solid = _cycleTime < OnTime;
		if (solid != _solid || force)
		{
			_solid = solid;
			_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, !solid);
		}

		float alpha;
		if (!solid)
			alpha = 0.12f;
		else if (_cycleTime > OnTime - WarningTime)
			alpha = Mathf.Sin(_cycleTime * 40f) > 0f ? 1f : 0.3f;
		else
			alpha = 1f;
		_visual.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	private void Build()
	{
		CollisionLayer = OneWay ? 1u | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms : 1u;
		_collision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = _size },
			OneWayCollision = OneWay,
		};
		AddChild(_collision);

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		Vector2 h = _size / 2f;
		_visual = new Node2D();
		_visual.AddChild(new Polygon2D
		{
			Color = new Color(0.1f, 0.12f, 0.18f, 1f),
			Polygon = new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y) },
		});
		Vector2[] outline = { new(-h.X, -h.Y), new(h.X, -h.Y), new(h.X, h.Y), new(-h.X, h.Y), new(-h.X, -h.Y) };
		_visual.AddChild(new Line2D { Points = outline, Width = 6f, DefaultColor = NeonColor with { A = 0.25f }, Material = additive });
		_visual.AddChild(new Line2D { Points = outline, Width = 2f, DefaultColor = NeonColor, Material = additive });
		AddChild(_visual);
	}
}
