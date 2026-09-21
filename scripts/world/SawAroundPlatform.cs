using Godot;

namespace Metroidvania.World;

// One or more saws riding a rectangular rail all the way around — the "saw circling a platform"
// trap. The node sits at the CENTRE of the rectangle (PlatformSize), so drop it on top of the
// platform it wraps and size it to match; PathOffset pushes the rail outwards (positive) or inwards
// (negative) from that rectangle, i.e. how far the centre of the blade rides from the platform's
// edge — 0 means the blade centre runs along the edge itself.
//
// The blades are plain SawTrap scenes (spin, sizing, Hazard damage all live there); this node only
// moves them, so the child "Saw" is left with TravelOffset = zero. SawCount > 1 spreads that many
// saws evenly around the loop; the extras are duplicates of the child made at runtime only, so the
// editor shows a single blade at the start position plus the rail.
//
// Nothing follows level geometry — the rail is a fixed rectangle, so match it to the platform in the
// editor (the platform area is drawn in the editor only, the rail both in the editor and in-game).
// [Tool] so the rail and blade preview live-update; movement only runs in-game. The _initialized
// guard keeps the property setters from touching child nodes while Godot is still restoring the
// scene's saved values (before _Ready).
[Tool]
public partial class SawAroundPlatform : Node2D
{
	private Vector2 _platformSize = new(192f, 64f);
	private float _pathOffset = 0f;
	private float _startProgress = 0f;
	private float _sawDiameter = 64f;
	private bool _showRail = true;

	// Size of the platform being wrapped; the node is its centre.
	[Export]
	public Vector2 PlatformSize
	{
		get => _platformSize;
		set { _platformSize = new Vector2(Mathf.Max(1f, value.X), Mathf.Max(1f, value.Y)); Refresh(); }
	}

	// How far outside the platform edge the blade centre rides (negative = inside).
	[Export]
	public float PathOffset
	{
		get => _pathOffset;
		set { _pathOffset = value; Refresh(); }
	}

	// Pixels per second along the rail.
	[Export] public float TravelSpeed = 140f;

	[Export] public bool Clockwise = true;

	// 0..1 position of the first saw along the loop at start.
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float StartProgress
	{
		get => _startProgress;
		set { _startProgress = value; Refresh(); }
	}

	// Saws spread evenly around the loop, starting at StartProgress. Extras only exist in-game.
	[Export(PropertyHint.Range, "1,8,1")] public int SawCount = 1;

	[ExportGroup("Saw")]
	[Export]
	public float SawDiameter
	{
		get => _sawDiameter;
		set { _sawDiameter = value; Refresh(); }
	}

	[Export] public float SpinSpeed = 12f;
	[Export] public bool InstantKill = false;
	[Export] public int Damage = 25;
	[Export] public float KnockbackForce = 350f;

	[ExportGroup("Rail")]
	[Export]
	public bool ShowRail
	{
		get => _showRail;
		set { _showRail = value; QueueRedraw(); }
	}

	[Export] public Color RailColor = new(0.06f, 0.06f, 0.07f, 0.9f);
	[Export] public float RailWidth = 6f;

	private SawTrap _saw;
	private readonly System.Collections.Generic.List<Node2D> _saws = new();
	private float _distance;
	private bool _initialized;

	// Runs parent-first, before the child SawTrap's own _Ready, so its damage/spin/size are already
	// set when it copies them into its Hazard.
	public override void _EnterTree()
	{
		_saw = GetNodeOrNull<SawTrap>("Saw");
		ForwardSawSettings(_saw);
	}

	public override void _Ready()
	{
		_saws.Clear();
		if (_saw is not null)
			_saws.Add(_saw);

		if (!Engine.IsEditorHint() && _saw is not null)
		{
			for (int i = 1; i < SawCount; i++)
			{
				var extra = (SawTrap)_saw.Duplicate();
				AddChild(extra);
				_saws.Add(extra);
			}
		}

		_initialized = true;
		Refresh();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		_distance += (Clockwise ? 1f : -1f) * TravelSpeed * (float)delta;
		PlaceSaws();
	}

	public override void _Draw()
	{
		Vector2 half = _platformSize * 0.5f;

		// Platform area, editor only, so the rectangle can be lined up with the real platform.
		if (Engine.IsEditorHint())
			DrawRect(new Rect2(-half, _platformSize), new Color(0.4f, 0.75f, 1f, 0.5f), false, 1f);

		if (!ShowRail)
			return;

		Vector2 rail = RailHalfExtents();
		DrawPolyline(new[]
		{
			new Vector2(-rail.X, -rail.Y), new Vector2(rail.X, -rail.Y),
			new Vector2(rail.X, rail.Y), new Vector2(-rail.X, rail.Y),
			new Vector2(-rail.X, -rail.Y),
		}, RailColor, RailWidth);
	}

	private void Refresh()
	{
		if (!_initialized)
			return;

		if (_saw is not null)
			ForwardSawSettings(_saw);

		_distance = _startProgress * PerimeterLength();
		PlaceSaws();
		QueueRedraw();
	}

	private void ForwardSawSettings(SawTrap saw)
	{
		if (saw is null)
			return;

		saw.SawDiameter = SawDiameter;
		saw.SpinSpeed = SpinSpeed;
		saw.InstantKill = InstantKill;
		saw.Damage = Damage;
		saw.KnockbackForce = KnockbackForce;
	}

	private void PlaceSaws()
	{
		float perimeter = PerimeterLength();
		for (int i = 0; i < _saws.Count; i++)
		{
			if (!IsInstanceValid(_saws[i]))
				continue;

			_saws[i].Position = PointOnRail(_distance + i * perimeter / _saws.Count);
		}
	}

	// Half-size of the rail rectangle; kept at least 1px so a big negative PathOffset can't flip it.
	private Vector2 RailHalfExtents()
	{
		Vector2 half = _platformSize * 0.5f + new Vector2(_pathOffset, _pathOffset);
		return new Vector2(Mathf.Max(1f, half.X), Mathf.Max(1f, half.Y));
	}

	private float PerimeterLength()
	{
		Vector2 rail = RailHalfExtents();
		return 4f * (rail.X + rail.Y);
	}

	// Point at [distance] along the loop, measured clockwise (on screen) from the top-left corner:
	// top edge going right, right edge going down, bottom edge going left, left edge going up.
	private Vector2 PointOnRail(float distance)
	{
		Vector2 rail = RailHalfExtents();
		float width = rail.X * 2f;
		float height = rail.Y * 2f;
		float d = Mathf.PosMod(distance, 2f * (width + height));

		if (d < width)
			return new Vector2(-rail.X + d, -rail.Y);
		d -= width;

		if (d < height)
			return new Vector2(rail.X, -rail.Y + d);
		d -= height;

		if (d < width)
			return new Vector2(rail.X - d, rail.Y);
		d -= width;

		return new Vector2(-rail.X, rail.Y - d);
	}
}
