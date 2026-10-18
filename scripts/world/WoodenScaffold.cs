using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandit-camp wooden scaffold: platforming under fire. A plank walkway (or, with Vertical, a plank
// barricade) built from PlankLength segments, each with its own collision. Fire catches a segment
// from an ExplosiveBarrel blast (IgniteAt), a bandit's GroundFire landing on it, Activate() (a
// pressure plate — lights the first segment) or a neighbour: it creeps plank to plank every
// SpreadDelay, chars for BurnTime and then that plank breaks and falls — so a burning bridge
// opens gaps one plank at a time. Flames hurt whoever stands in them (player and enemies), reach
// barrels (lighting their fuse) and jump to any other flammable touching an end (PlankFire).
// Clumsy: blow a barrel on your own bridge and the way is gone. Clever: burn the barricade, drop
// the bandits off their walkway. Nothing comes back until the scene reloads.
// Node origin = left end of the top surface (horizontal) or top end (vertical, extends down).
// Everything is drawn in code. [Tool] so the planks preview in the editor.
[Tool]
public partial class WoodenScaffold : Node2D, IFlammable
{
	[Signal] public delegate void IgnitedEventHandler();
	[Signal] public delegate void BurnedOutEventHandler();

	private float _length = 192f;
	[Export]
	public float Length { get => _length; set { _length = Mathf.Max(PlankLength, value); Rebuild(); } }

	// False = walkway you stand on (one-way: jump up through it, drop down through it).
	// True = solid barricade wall blocking a passage.
	private bool _vertical;
	[Export]
	public bool Vertical { get => _vertical; set { _vertical = value; Rebuild(); } }

	private bool _oneWay = true;
	[Export]
	public bool OneWay { get => _oneWay; set { _oneWay = value; Rebuild(); } }

	// Support legs hanging under a walkway (purely visual; they burn and fall with their plank).
	private float _legLength = 60f;
	[Export]
	public float LegLength { get => _legLength; set { _legLength = Mathf.Max(0f, value); Rebuild(); } }

	private Color _woodColor = new(0.5f, 0.34f, 0.2f, 1f);
	[Export]
	public Color WoodColor { get => _woodColor; set { _woodColor = value; Rebuild(); } }

	[ExportGroup("Fire")]
	[Export] public float SpreadDelay = 0.16f;
	[Export] public float BurnTime = 1.3f;
	[Export] public int BurnDamage = 6;
	[Export] public float BurnTickInterval = 0.5f;
	// How far fire jumps from a plank to another flammable, or to a barrel.
	[Export] public float SpreadReach = 22f;

	private const float PlankLength = 24f;
	private const float Thickness = 12f;
	private const float FlameHeight = 26f;
	private const uint GraspableLayer = 1u | PhysicsLayers.ClimbableWalls;

	private enum PlankState { Intact, Burning, Gone }

	private sealed class Plank
	{
		public Rect2 Rect;              // local
		public PlankState State;
		public float Timer;
		public bool Spread;
		public CollisionShape2D Shape;
		public Node2D Root;
		public Polygon2D Board;
		public Line2D Embers;
		public Node2D Fire;
		public Line2D Leg;
	}

	private readonly List<Plank> _planks = new();
	private readonly Dictionary<ulong, ulong> _nextBurnTick = new();
	private StaticBody2D _body;
	private Node2D _visual;
	private Line2D _rail;
	private bool _ignited;
	private float _time;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		Rebuild();
		if (!Engine.IsEditorHint())
			AddToGroup(PlankFire.FlammableGroup);
	}

	// ── Ignition API ────────────────────────────────────────────────────────────────────────────

	// Plate / lever hook: lights the first plank (left end, or top of a barricade).
	public void Activate() => IgnitePlank(0);

	// Lights every plank within radius of a global point (barrel blasts call this).
	public void IgniteAt(Vector2 globalPoint, float radius)
	{
		Vector2 local = ToLocal(globalPoint);
		for (int i = 0; i < _planks.Count; i++)
		{
			if (PlankFire.DistanceToRect(local, _planks[i].Rect) <= radius)
				IgnitePlank(i);
		}
	}

	private void IgnitePlank(int index)
	{
		if (index < 0 || index >= _planks.Count || _planks[index].State != PlankState.Intact)
			return;
		Plank plank = _planks[index];
		plank.State = PlankState.Burning;
		plank.Timer = 0f;
		plank.Embers.Visible = true;
		PlankFire.SetLit(plank.Fire, true);
		if (!_ignited)
		{
			_ignited = true;
			EmitSignal(SignalName.Ignited);
			Sfx.PlayAt(this, "Magic", "Fireball");
		}
	}

	// ── Burning ─────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		PlankFire.CatchGroundFire(GetTree(), this);
		if (!_ignited)
			return;

		bool anyLeft = false;
		for (int i = 0; i < _planks.Count; i++)
		{
			Plank plank = _planks[i];
			if (plank.State != PlankState.Burning)
			{
				anyLeft |= plank.State == PlankState.Intact;
				continue;
			}
			anyLeft = true;
			plank.Timer += dt;
			float t = Mathf.Clamp(plank.Timer / BurnTime, 0f, 1f);
			plank.Board.Color = _woodColor.Lerp(PlankFire.Char, t);
			plank.Embers.DefaultColor = new Color(1f, 0.45f, 0.1f, 0.3f + 0.6f * t);
			if (!plank.Spread && plank.Timer >= SpreadDelay)
			{
				plank.Spread = true;
				IgnitePlank(i - 1);
				IgnitePlank(i + 1);
				PlankFire.SpreadFrom(this, GlobalRect(plank).Grow(SpreadReach));
			}
			if (plank.Timer >= 0.2f)
				PlankFire.HurtBodies(GetTree(), PlankFire.FlameArea(GlobalRect(plank), _vertical, FlameHeight), BurnDamage, BurnTickInterval, _nextBurnTick);
			if (plank.Timer >= BurnTime)
				Collapse(plank);
		}
		if (!anyLeft)
		{
			_ignited = false;
			_rail.Visible = false;
			EmitSignal(SignalName.BurnedOut);
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !_ignited)
			return;
		_time += (float)delta;
		foreach (Plank plank in _planks)
		{
			if (plank.State == PlankState.Burning)
				PlankFire.Animate(plank.Fire, _time, Mathf.Clamp(plank.Timer / 0.25f, 0f, 1f), plank.Rect.Position.X + plank.Rect.Position.Y);
		}
	}

	private void Collapse(Plank plank)
	{
		plank.State = PlankState.Gone;
		plank.Shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		plank.Root.Visible = false;
		PlankFire.SetLit(plank.Fire, false);
		Sfx.PlayAt(this, "World/Mining", "mine");
		SpawnFallingBoard(plank);
	}

	private void SpawnFallingBoard(Plank plank)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		Rect2 rect = GlobalRect(plank);
		Vector2 half = _vertical ? new Vector2(Thickness / 2f, PlankLength / 4f) : new Vector2(PlankLength / 4f, Thickness / 2f);
		for (int p = 0; p < 2; p++)
		{
			var board = new Polygon2D
			{
				Polygon = new[] { -half, new Vector2(half.X, -half.Y), half, new Vector2(-half.X, half.Y) },
				Color = PlankFire.Char.Lightened(_rng.RandfRange(0f, 0.12f)),
				ZIndex = 2,
			};
			board.AddChild(new Line2D { Points = new[] { new Vector2(-half.X + 1f, 0f), new Vector2(half.X - 1f, 0f) }, Width = 1.5f, DefaultColor = new Color(1f, 0.45f, 0.1f, 0.85f), Material = PlankFire.Additive });
			scene.AddChild(board);
			board.GlobalPosition = _vertical
				? new Vector2(rect.GetCenter().X, rect.Position.Y + rect.Size.Y * (0.25f + 0.5f * p))
				: new Vector2(rect.Position.X + rect.Size.X * (0.25f + 0.5f * p), rect.GetCenter().Y);
			PlankFire.Tumble(board, _rng);
		}
		if (plank.Leg is { Visible: true } leg)
		{
			var fallingLeg = new Line2D { Points = leg.Points, Width = leg.Width, DefaultColor = PlankFire.Char, ZIndex = 1 };
			scene.AddChild(fallingLeg);
			fallingLeg.GlobalPosition = leg.GlobalPosition;
			PlankFire.Tumble(fallingLeg, _rng);
		}
		PlankFire.Sparks(this, rect.GetCenter());
	}

	private Rect2 GlobalRect(Plank plank) => new(ToGlobal(plank.Rect.Position), plank.Rect.Size);

	// ── Build ───────────────────────────────────────────────────────────────────────────────────

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;
		_body?.QueueFree();
		_visual?.QueueFree();
		_planks.Clear();

		_body = new StaticBody2D
		{
			Name = "Body",
			CollisionLayer = _vertical ? 1u : (_oneWay ? GraspableLayer | PhysicsLayers.OneWayPlatforms : GraspableLayer),
			CollisionMask = 0,
		};
		AddChild(_body);
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);

		int count = Mathf.Max(1, Mathf.RoundToInt(_length / PlankLength));
		float step = _length / count;
		var seeded = new RandomNumberGenerator { Seed = 7331 };
		Color dark = _woodColor.Darkened(0.5f);

		// Rope lashing / rail drawn behind the planks.
		if (!_vertical)
			_rail = new Line2D { Points = new[] { new Vector2(0f, Thickness * 0.5f), new Vector2(_length, Thickness * 0.5f) }, Width = 2f, DefaultColor = dark };
		else
			_rail = new Line2D { Points = new[] { new Vector2(0f, 0f), new Vector2(0f, _length) }, Width = 3f, DefaultColor = dark };
		_visual.AddChild(_rail);

		for (int i = 0; i < count; i++)
		{
			var rect = _vertical
				? new Rect2(-Thickness / 2f, i * step, Thickness, step)
				: new Rect2(i * step, 0f, step, Thickness);
			var plank = new Plank { Rect = rect };

			var shape = new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = rect.Size },
				Position = rect.GetCenter(),
				OneWayCollision = !_vertical && _oneWay,
			};
			_body.AddChild(shape);
			plank.Shape = shape;

			var root = new Node2D();
			_visual.AddChild(root);
			plank.Root = root;
			// Each board slightly uneven: a 1-2 px jitter and its own shade.
			float jitter = seeded.RandfRange(-1f, 1f);
			Vector2 a = rect.Position + new Vector2(0.5f, 0.5f);
			Vector2 b = rect.End - new Vector2(0.5f, 0.5f);
			plank.Board = new Polygon2D
			{
				Polygon = _vertical
					? new[] { new Vector2(a.X + jitter, a.Y), new Vector2(b.X + jitter, a.Y), new Vector2(b.X, b.Y), new Vector2(a.X, b.Y) }
					: new[] { new Vector2(a.X, a.Y + jitter), new Vector2(b.X, a.Y), new Vector2(b.X, b.Y), new Vector2(a.X, b.Y + jitter) },
				Color = _woodColor.Darkened(seeded.RandfRange(-0.08f, 0.15f)),
			};
			root.AddChild(plank.Board);
			// Grain line and nail heads.
			root.AddChild(_vertical
				? new Line2D { Points = new[] { new Vector2(rect.GetCenter().X, a.Y + 3f), new Vector2(rect.GetCenter().X + 1f, b.Y - 3f) }, Width = 1f, DefaultColor = dark with { A = 0.6f } }
				: new Line2D { Points = new[] { new Vector2(a.X + 3f, rect.GetCenter().Y), new Vector2(b.X - 3f, rect.GetCenter().Y + 1f) }, Width = 1f, DefaultColor = dark with { A = 0.6f } });
			Vector2 nail = _vertical ? new Vector2(rect.GetCenter().X, a.Y + 3f) : new Vector2(a.X + 3f, a.Y + 3f);
			root.AddChild(new Polygon2D { Polygon = new[] { nail, nail + new Vector2(1.5f, 0f), nail + new Vector2(1.5f, 1.5f), nail + new Vector2(0f, 1.5f) }, Color = new Color(0.3f, 0.3f, 0.32f) });
			// Glowing cracks, shown once it burns.
			plank.Embers = new Line2D
			{
				Points = _vertical
					? new[] { new Vector2(a.X + 2f, a.Y + 4f), new Vector2(b.X - 3f, rect.GetCenter().Y), new Vector2(a.X + 3f, b.Y - 3f) }
					: new[] { new Vector2(a.X + 4f, b.Y - 2f), new Vector2(rect.GetCenter().X, a.Y + 3f), new Vector2(b.X - 3f, b.Y - 3f) },
				Width = 1.2f,
				DefaultColor = new Color(1f, 0.45f, 0.1f, 0f),
				Material = PlankFire.Additive,
				Visible = false,
			};
			root.AddChild(plank.Embers);

			// Support legs every ~4 planks under a walkway.
			if (!_vertical && _legLength > 0f && (i == 0 || i == count - 1 || i % 4 == 2))
			{
				float x = i == count - 1 ? rect.End.X - 4f : rect.Position.X + 4f;
				plank.Leg = new Line2D { Points = new[] { new Vector2(x, Thickness), new Vector2(x + seeded.RandfRange(-3f, 3f), Thickness + _legLength) }, Width = 5f, DefaultColor = _woodColor.Darkened(0.25f) };
				root.AddChild(plank.Leg);
			}

			plank.Fire = PlankFire.Build(rect, _vertical, FlameHeight, seeded);
			_visual.AddChild(plank.Fire);
			_planks.Add(plank);
		}
	}
}
