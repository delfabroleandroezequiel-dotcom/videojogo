using Godot;

namespace Metroidvania.World;

// A walkable wooden bridge — single AnimatableBody2D (sync_to_physics = true, the exact pattern
// MovingPlatform.cs already uses and is proven to move reliably every frame) carries the actual
// collision, dipping AND tilting as one rigid unit toward whatever the deepest nearby plank is
// doing. The CURVE look comes from a separate, purely visual per-plank sag layered on top: each
// Tabla sprite is a plain Sprite2D (not a physics body), so nudging its own local Position every
// frame is completely safe — no sync_to_physics involved, no risk of the earlier bug where looping
// position updates across many independently-created AnimatableBody2D didn't reliably stick.
// [Tool] so Count/PlankWidth/PlankSpacing/VisualOffset preview live in the editor at rest (no
// player, so everything settles to the idle sag curve) — the plank alignment against the collision
// needed several rounds of guessing before this existed; tune it live instead of round-tripping
// through Play every time.
// Flammable (off by default): the bridge burns like a WoodenScaffold — a barrel blast, a bandit's
// ember, Activate() (lights the first plank) or a neighbouring fire catches a plank, fire creeps
// plank to plank, each plank chars and drops out after BurnTime, and the collision is rebuilt as
// runs of the planks still standing, so a burnt plank is a real gap you fall through.
[Tool]
public partial class Puente : AnimatableBody2D, IFlammable
{
	[Signal] public delegate void IgnitedEventHandler();
	[Signal] public delegate void BurnedOutEventHandler();

	private int _count = 6;

	// How many plank sprites tile across the bridge — also drives the total Width (Count *
	// PlankSpacing), so there's one number to bump instead of keeping Count and Width in sync by hand.
	[Export]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _plankWidth = 70f;

	// How big each plank sprite is drawn (uniform scale, from tabla.png's own width).
	[Export]
	public float PlankWidth
	{
		get => _plankWidth;
		set { _plankWidth = Mathf.Max(1f, value); Rebuild(); }
	}

	private float _plankSpacing = 18f;

	// How far apart consecutive planks are PLACED — independent from PlankWidth on purpose:
	// tabla.png has a lot of transparent margin around the actual wood, so placing planks
	// PlankWidth apart (the sprite's full nominal size) leaves a visible gap between the drawn
	// wood even though the invisible bounding boxes touch. Shrink this below PlankWidth to close
	// that gap — tune live in the editor until the wood actually touches.
	[Export]
	public float PlankSpacing
	{
		get => _plankSpacing;
		set { _plankSpacing = Mathf.Max(1f, value); Rebuild(); }
	}

	private float _thickness = 20f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = Mathf.Max(1f, value); Rebuild(); }
	}

	// Tiny constant dip even with nobody on it, so the whole bridge reads as "hanging" rather than
	// perfectly rigid — 0 disables it. Applies to the single collision body, on top of whatever
	// the deepest plank's own sag contributes.
	[Export] public float RestDip = 4f;

	// How fast the whole-body dip chases its target, and how damped that chase is.
	[Export] public float SpringStiffness = 40f;
	[Export] public float SpringDamping = 12f;

	// Extra per-plank visual sag on top of the whole-body dip — this is what makes it read as a
	// curve instead of a flat board dropping straight down. Same idle-parabola + local
	// player-proximity idea, just driving sprite Position instead of a physics body. Also what
	// the idle preview (no player, in-editor) settles into — the idle parabola alone.
	[Export] public float PlankIdleSagDepth = 5f;
	[Export] public float PlankPlayerSagDepth = 16f;
	[Export] public float PlankInfluenceRadius = 90f;
	[Export] public float PlankSpringStiffness = 45f;
	[Export] public float PlankSpringDamping = 13f;
	[Export] public float MaxPlankSag = 26f;

	// The whole collision dips with the deepest plank, even with the player at an end — so walking
	// off onto level ground meets a lip as tall as the dip (the Player has no step-up) and gets
	// stuck. On, the dip (RestDip included) fades to 0 near either end, so the ends meet the
	// ground flush. Off by default to keep existing bridges exactly as they were tuned.
	[Export] public bool EaseDipAtEnds;

	[ExportGroup("Fire")]
	[Export] public bool Flammable;
	[Export] public float SpreadDelay = 0.2f;
	[Export] public float BurnTime = 1.4f;
	[Export] public int BurnDamage = 6;
	[Export] public float BurnTickInterval = 0.5f;
	// How far fire jumps from a plank to another flammable, or to a barrel.
	[Export] public float SpreadReach = 22f;
	[ExportGroup("")]

	private Texture2D _plankTexture;

	[Export]
	public Texture2D PlankTexture
	{
		get => _plankTexture;
		set { _plankTexture = value; Rebuild(); }
	}

	private Color _tintColor = new(0.6f, 0.6f, 0.6f, 1f);

	// Darkened by default — the raw art reads too bright/warm against a dark cave, same reasoning
	// as the parallax background layers' own tint.
	[Export]
	public Color TintColor
	{
		get => _tintColor;
		set { _tintColor = value; Rebuild(); }
	}

	private Vector2 _visualOffset = new(0f, -30f);

	// Nudges every plank sprite relative to the walkable surface (this node's own origin, at the
	// LEFT edge — same "origin = start, extends right" convention as CorridorBlock/MiniCorridor).
	// tabla.png has empty margin above the plank itself; -30 confirmed against the live editor
	// preview (see [Tool] above) lines the drawn plank surface up with the collision top. Re-tune
	// this the same way if you ever swap in a differently-cropped plank texture.
	[Export]
	public Vector2 VisualOffset
	{
		get => _visualOffset;
		set { _visualOffset = value; Rebuild(); }
	}

	private float Width => Count * PlankSpacing;

	private Vector2 _basePosition;
	private float _dip;
	private float _dipVelocity;
	private Node2D _player;

	private Sprite2D[] _plankSprites;
	private float[] _plankSag;
	private float[] _plankSagVelocity;

	public override void _Ready()
	{
		CollisionLayer = 1;
		CollisionMask = 0;
		SyncToPhysics = true;

		// PlankWidth/Thickness are the only intended way to size this — if this node's own Scale
		// ever got changed by accident (e.g. dragging its resize gizmo in the 2D viewport), reset
		// it here so it can't quietly shrink/stretch everything. Same gotcha ProceduralWater and
		// Cascada already guard against.
		Scale = Vector2.One;
		_basePosition = Position;

		Rebuild();

		if (Flammable && !Engine.IsEditorHint())
			SetUpFire();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// Editor preview only needs the per-plank visual curve (see class doc) — moving the body
		// itself here fought every manual drag (this ran every physics tick, snapping Position
		// back to _basePosition + dip before the drag could stick) and, since the body kept
		// re-overlapping nearby Area2D hazards, spuriously toggled their monitoring mid-signal
		// ("Function blocked during in/out signal" errors). No player exists in-editor anyway, so
		// there's nothing for the body's own collision to matter for.
		if (Engine.IsEditorHint())
		{
			UpdatePlankSag(dt);
			return;
		}

		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		float deepestPlankSag = UpdatePlankSag(dt);

		// The single rigid collision plane chases the DEEPEST current plank sag, not a fixed
		// depth — that plank is always the one nearest the player (that's what makes it the
		// deepest), so the flat collision line stays close to whatever's actually drawn under
		// their feet instead of drifting away from it as they walk across. With no player around
		// (including in the editor, where this is what [Tool] previews), this settles to the idle
		// parabola's own peak.
		float target = (RestDip + deepestPlankSag) * EndEase();

		// Explicit-Euler damped spring toward target — unconditionally well-behaved at this
		// stiffness/damping and a 60Hz-ish physics tick.
		float accel = SpringStiffness * (target - _dip) - SpringDamping * _dipVelocity;
		_dipVelocity += accel * dt;
		_dip += _dipVelocity * dt;
		// Near an end the eased target is a hard ceiling, not just where the spring heads: waiting
		// for the spring left a lip the player stalled against for most of a second.
		if (EaseDipAtEnds && _dip > target && target < RestDip + deepestPlankSag)
		{
			_dip = target;
			_dipVelocity = Mathf.Min(_dipVelocity, 0f);
		}

		// No rotation on the collision body at all — two different attempts at clamping/pivoting
		// it (capping the angle, capping how fast it could change, pivoting around the player
		// instead of the left edge) all still launched the rider. Rotating a body a character is
		// standing on is apparently just risky in Godot's kinematic system regardless of how
		// gently it's done; the visual curve (plain Sprite2D per-plank offsets, no physics) never
		// had this problem and stays exactly as it was. The collision now only ever translates.
		Position = _basePosition + new Vector2(0f, _dip);

		if (_fires is not null)
			UpdateFire(dt);
	}

	private float UpdatePlankSag(float dt)
	{
		// Snapshot into locals and size the loop off the arrays themselves rather than the Count
		// property: an [Export] setter (Inspector edits, or property reassignment during a tool
		// script reload) can call Rebuild() — which reassigns these fields and briefly frees the
		// old plank sprites — while this is running, and Count can already reflect a value the
		// arrays don't match yet. Reading the field once up front avoids a null field slipping in
        // between the "is null" check and the indexing below.
		Sprite2D[] sprites = _plankSprites;
		float[] sag = _plankSag;
		float[] sagVelocity = _plankSagVelocity;
		if (sprites is null || sag is null || sagVelocity is null)
			return 0f;

		int count = Mathf.Min(sprites.Length, Mathf.Min(sag.Length, sagVelocity.Length));
		float deepest = 0f;
		for (int i = 0; i < count; i++)
		{
			if (!IsInstanceValid(sprites[i]))
				continue;

			float plankLocalX = (i + 0.5f) * PlankSpacing;
			float worldX = _basePosition.X + plankLocalX; // base X never moves, only the whole body's Y dips

			// The two end planks are always treated as bolted to solid ground on either side —
			// zero sag no matter what, never influenced by the player either. Everything in
			// between distributes across the remaining span (t=0 at the plank right after the
			// left anchor, t=1 at the plank right before the right one), so the curve always
			// bottoms out somewhere in the unanchored middle, never at the anchors themselves.
			bool isAnchor = i == 0 || i == count - 1;
			float target;
			if (isAnchor || count <= 2)
			{
				target = 0f;
			}
			else
			{
				float t = (float)i / (count - 1);
				float idleTarget = PlankIdleSagDepth * 4f * t * (1f - t); // parabola, 0 at both ends, peak at the middle
				target = Mathf.Min(MaxPlankSag, idleTarget + PlankPlayerSagAt(worldX));
			}

			float accel = PlankSpringStiffness * (target - sag[i]) - PlankSpringDamping * sagVelocity[i];
			sagVelocity[i] += accel * dt;
			sag[i] = Mathf.Clamp(sag[i] + sagVelocity[i] * dt, 0f, MaxPlankSag);

			sprites[i].Position = new Vector2(plankLocalX, sag[i]) + VisualOffset;
			deepest = Mathf.Max(deepest, sag[i]);
		}

		return deepest;
	}

	private const float EndFlatZone = 30f;

	private float EndEase()
	{
		if (!EaseDipAtEnds || !IsPlayerOnBridge())
			return 1f;
		float localX = _player.GlobalPosition.X - GlobalPosition.X;
		float toEnd = Mathf.Min(localX, Width - localX);
		// Flat for the last EndFlatZone px, easing back to the full dip over PlankInfluenceRadius.
		return Mathf.Clamp((toEnd - EndFlatZone) / PlankInfluenceRadius, 0f, 1f);
	}

	// ── Fire ────────────────────────────────────────────────────────────────────────────────────

	private enum PlankState { Intact, Burning, Gone }

	private const float FlameHeight = 26f;
	private PlankState[] _plankState;
	private float[] _burnTimer;
	private bool[] _hasSpread;
	private Node2D[] _fires;
	private readonly System.Collections.Generic.Dictionary<ulong, ulong> _nextBurnTick = new();
	private readonly RandomNumberGenerator _fireRng = new();
	private bool _burning;
	private float _fireTime;

	private void SetUpFire()
	{
		AddToGroup(PlankFire.FlammableGroup);
		_fireRng.Randomize();
		_plankState = new PlankState[Count];
		_burnTimer = new float[Count];
		_hasSpread = new bool[Count];
		_fires = new Node2D[Count];
		var seeded = new RandomNumberGenerator { Seed = 9157 };
		for (int i = 0; i < Count; i++)
		{
			_fires[i] = PlankFire.Build(PlankRect(i, 0f), false, FlameHeight, seeded);
			AddChild(_fires[i]);
		}
	}

	// Plate / lever hook: lights the first (left) plank.
	public void Activate() => IgnitePlank(0);

	public void IgniteAt(Vector2 globalPoint, float radius)
	{
		if (_fires is null)
			return;
		Vector2 local = ToLocal(globalPoint);
		for (int i = 0; i < _fires.Length; i++)
		{
			if (PlankFire.DistanceToRect(local, PlankRect(i, SagOf(i) + ArtShift)) <= radius)
				IgnitePlank(i);
		}
	}

	private void IgnitePlank(int i)
	{
		if (_fires is null || i < 0 || i >= _fires.Length || _plankState[i] != PlankState.Intact)
			return;
		_plankState[i] = PlankState.Burning;
		_burnTimer[i] = 0f;
		PlankFire.SetLit(_fires[i], true);
		if (!_burning)
		{
			_burning = true;
			EmitSignal(SignalName.Ignited);
			Metroidvania.Shared.Sfx.PlayAt(this, "Magic", "Fireball");
		}
	}

	private void UpdateFire(float dt)
	{
		PlankFire.CatchGroundFire(GetTree(), this);
		if (!_burning)
			return;
		_fireTime += dt;
		bool anyLeft = false;
		bool collapsed = false;
		for (int i = 0; i < _fires.Length; i++)
		{
			if (_plankState[i] != PlankState.Burning)
			{
				anyLeft |= _plankState[i] == PlankState.Intact;
				continue;
			}
			anyLeft = true;
			_burnTimer[i] += dt;
			float t = Mathf.Clamp(_burnTimer[i] / BurnTime, 0f, 1f);
			// Fire rides the plank's own sag; the board chars as it burns.
			_fires[i].Position = new Vector2(0f, SagOf(i) + ArtShift);
			PlankFire.Animate(_fires[i], _fireTime, Mathf.Clamp(_burnTimer[i] / 0.25f, 0f, 1f), i * 1.7f);
			if (_plankSprites is not null && i < _plankSprites.Length && IsInstanceValid(_plankSprites[i]))
				_plankSprites[i].Modulate = TintColor.Lerp(PlankFire.Char, t);
			Rect2 global = new(ToGlobal(PlankRect(i, SagOf(i) + ArtShift).Position), PlankRect(i, 0f).Size);
			if (!_hasSpread[i] && _burnTimer[i] >= SpreadDelay)
			{
				_hasSpread[i] = true;
				IgnitePlank(i - 1);
				IgnitePlank(i + 1);
				PlankFire.SpreadFrom(this, global.Grow(SpreadReach));
			}
			if (_burnTimer[i] >= 0.2f)
				PlankFire.HurtBodies(GetTree(), PlankFire.FlameArea(global, false, FlameHeight), BurnDamage, BurnTickInterval, _nextBurnTick);
			if (_burnTimer[i] >= BurnTime)
			{
				CollapsePlank(i, global);
				collapsed = true;
			}
		}
		if (collapsed)
			CallDeferred(MethodName.RebuildCollisionRuns);
		if (!anyLeft)
		{
			_burning = false;
			EmitSignal(SignalName.BurnedOut);
		}
	}

	private void CollapsePlank(int i, Rect2 global)
	{
		_plankState[i] = PlankState.Gone;
		PlankFire.SetLit(_fires[i], false);
		Metroidvania.Shared.Sfx.PlayAt(this, "World/Mining", "mine");
		PlankFire.Sparks(this, global.GetCenter());
		if (_plankSprites is null || i >= _plankSprites.Length || !IsInstanceValid(_plankSprites[i]))
			return;
		// A charred copy of the plank tumbles away; the real sprite just hides.
		Sprite2D sprite = _plankSprites[i];
		if (GetTree().CurrentScene is Node scene)
		{
			var falling = new Sprite2D { Texture = sprite.Texture, Scale = sprite.GlobalScale, Modulate = PlankFire.Char.Lightened(0.15f), ZIndex = 2 };
			scene.AddChild(falling);
			falling.GlobalPosition = sprite.GlobalPosition;
			PlankFire.Tumble(falling, _fireRng);
		}
		sprite.Visible = false;
	}

	// One collision shape per run of planks still standing — a single shape across the whole span
	// while intact (same as before), split around every burnt-out gap.
	private void RebuildCollisionRuns()
	{
		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape2D old)
			{
				old.Disabled = true;
				old.QueueFree();
			}
		}
		int start = -1;
		for (int i = 0; i <= _plankState.Length; i++)
		{
			bool standing = i < _plankState.Length && _plankState[i] != PlankState.Gone;
			if (standing && start < 0)
				start = i;
			else if (!standing && start >= 0)
			{
				float from = start * PlankSpacing;
				float to = i * PlankSpacing;
				AddChild(new CollisionShape2D
				{
					Position = new Vector2((from + to) / 2f, 0f),
					Shape = new RectangleShape2D { Size = new Vector2(to - from, Thickness) },
				});
				start = -1;
			}
		}
	}

	// Plank i's slice of the collision box, local, lowered by its visual sag.
	private Rect2 PlankRect(int i, float sag) => new(i * PlankSpacing, -Thickness / 2f + sag, PlankSpacing, Thickness);

	private float SagOf(int i) => _plankSag is not null && i < _plankSag.Length ? _plankSag[i] : 0f;

	// Fire follows the DRAWN wood, not the collision: VisualOffset lifts the planks above the
	// walkable line (tuned per map, e.g. InteriorCueva5), and tabla.png's wood band sits ~3 px below
	// the sprite centre, so the wood top lines up with the collision top at VisualOffset.Y = -3.
	private float ArtShift => VisualOffset.Y + 3f;

	// Falls off linearly to 0 past PlankInfluenceRadius, so only planks genuinely near the
	// player's feet get extra sag.
	private float PlankPlayerSagAt(float worldX)
	{
		if (_player is null)
			return 0f;

		if (!IsPlayerOnBridge())
			return 0f;

		float dx = Mathf.Abs(_player.GlobalPosition.X - worldX);
		if (dx > PlankInfluenceRadius)
			return 0f;

		return PlankPlayerSagDepth * (1f - dx / PlankInfluenceRadius);
	}

	private bool IsPlayerOnBridge()
	{
		if (_player is null)
			return false;

		float centerX = GlobalPosition.X + Width / 2f;
		float dx = Mathf.Abs(_player.GlobalPosition.X - centerX);
		float dy = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		return dx <= Width / 2f && dy <= 80f;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		var collision = new CollisionShape2D
		{
			Name = "CollisionShape2D",
			Position = new Vector2(Width / 2f, 0f),
			Shape = new RectangleShape2D { Size = new Vector2(Width, Thickness) },
		};
		AddChild(collision);
		collision.Owner = this;

		_fires = null;
		_plankSag = new float[Count];
		_plankSagVelocity = new float[Count];

		if (PlankTexture is null)
		{
			_plankSprites = null;
			return;
		}

		// Uniform scale (same factor both axes) so the source image's own proportions are kept
		// as-is — PlankWidth alone decides each plank's size, no separate height multiplier.
		float scale = PlankWidth / PlankTexture.GetSize().X;
		_plankSprites = new Sprite2D[Count];
		for (int i = 0; i < Count; i++)
		{
			var sprite = new Sprite2D
			{
				Name = $"Tabla{i + 1}",
				Texture = PlankTexture,
				Modulate = TintColor,
				Position = new Vector2((i + 0.5f) * PlankSpacing, 0f) + VisualOffset,
				Scale = Vector2.One * scale,
			};
			AddChild(sprite);
			sprite.Owner = this;
			_plankSprites[i] = sprite;
		}
	}
}
