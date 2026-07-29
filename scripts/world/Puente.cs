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
[Tool]
public partial class Puente : AnimatableBody2D
{
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
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		float deepestPlankSag = UpdatePlankSag(dt);

		// The single rigid collision plane chases the DEEPEST current plank sag, not a fixed
		// depth — that plank is always the one nearest the player (that's what makes it the
		// deepest), so the flat collision line stays close to whatever's actually drawn under
		// their feet instead of drifting away from it as they walk across. With no player around
		// (including in the editor, where this is what [Tool] previews), this settles to the idle
		// parabola's own peak.
		float target = RestDip + deepestPlankSag;

		// Explicit-Euler damped spring toward target — unconditionally well-behaved at this
		// stiffness/damping and a 60Hz-ish physics tick.
		float accel = SpringStiffness * (target - _dip) - SpringDamping * _dipVelocity;
		_dipVelocity += accel * dt;
		_dip += _dipVelocity * dt;

		// No rotation on the collision body at all — two different attempts at clamping/pivoting
		// it (capping the angle, capping how fast it could change, pivoting around the player
		// instead of the left edge) all still launched the rider. Rotating a body a character is
		// standing on is apparently just risky in Godot's kinematic system regardless of how
		// gently it's done; the visual curve (plain Sprite2D per-plank offsets, no physics) never
		// had this problem and stays exactly as it was. The collision now only ever translates.
		Position = _basePosition + new Vector2(0f, _dip);
	}

	private float UpdatePlankSag(float dt)
	{
		if (_plankSprites is null)
			return 0f;

		float deepest = 0f;
		for (int i = 0; i < Count; i++)
		{
			float plankLocalX = (i + 0.5f) * PlankSpacing;
			float worldX = _basePosition.X + plankLocalX; // base X never moves, only the whole body's Y dips

			// The two end planks are always treated as bolted to solid ground on either side —
			// zero sag no matter what, never influenced by the player either. Everything in
			// between distributes across the remaining span (t=0 at the plank right after the
			// left anchor, t=1 at the plank right before the right one), so the curve always
			// bottoms out somewhere in the unanchored middle, never at the anchors themselves.
			bool isAnchor = i == 0 || i == Count - 1;
			float target;
			if (isAnchor || Count <= 2)
			{
				target = 0f;
			}
			else
			{
				float t = (float)i / (Count - 1);
				float idleTarget = PlankIdleSagDepth * 4f * t * (1f - t); // parabola, 0 at both ends, peak at the middle
				target = Mathf.Min(MaxPlankSag, idleTarget + PlankPlayerSagAt(worldX));
			}

			float accel = PlankSpringStiffness * (target - _plankSag[i]) - PlankSpringDamping * _plankSagVelocity[i];
			_plankSagVelocity[i] += accel * dt;
			_plankSag[i] = Mathf.Clamp(_plankSag[i] + _plankSagVelocity[i] * dt, 0f, MaxPlankSag);

			_plankSprites[i].Position = new Vector2(plankLocalX, _plankSag[i]) + VisualOffset;
			deepest = Mathf.Max(deepest, _plankSag[i]);
		}

		return deepest;
	}

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
