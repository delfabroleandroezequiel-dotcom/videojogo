using Godot;

namespace Metroidvania.World;

// Reusable greybox hazard built from LavaTileSheet (see that file for the sheet/animation
// plumbing shared with the horizontal LavaFloor).
// Stacks BodyRow trunk tiles Height times to form a cascade of any length, capped at the top
// with the splashy "emerging" tile (row 3) and/or at the bottom with the splash-into-pool +
// pool-surface tiles (rows 6-7). Thickness fattens a single fall's girth by interlocking copies
// (see LavaTileSheet.ContentStride — no stretching, stays pixel-perfect); Width repeats that
// whole fall, gap and all, side by side for multiple separate streams. Same wipe-and-respawn
// idea as SpikeAccordion, applied to a 3D-feeling grid (width x thickness x height) instead of a
// row.
//   - HasTop off: skips the emerge cap, so the trunk starts flush at this node's own Position —
//     reads as coming down from an unseen ceiling above.
//   - HasBottom off: skips the splash+pool caps, so the trunk just stops — reads as falling into
//     an open pit. No special "infinite" collision is needed for that: FallDeathY on the player
//     (see Player.cs) already kills anything that drops past the bottom of the level.
// Every tile starts its animation on a different frame (its stack index), the same stagger
// trick SpikeAccordion uses via StartOffset, so the flow reads as a cascade instead of the
// whole stack flickering in lockstep.
// Element switches the reskin to Water (tint + blue glow, see LavaTileSheet) — purely visual.
// A single Hazard (InstantKill by default — lava kills on contact) covers the whole built
// stack; toggle InstantKill off and use Damage/KnockbackForce instead if a variant should just
// hurt (e.g. a shallow Water pool) rather than kill outright.
// [Tool] so the stack (and its animation — Godot's editor previews AnimatedSprite2D live)
// shows before ever pressing Play.
[Tool]
public partial class LavaFall : Node2D
{
	private int _width = 3;

	// How many separate falls sit side by side, each with its own gap — for "más de una caída".
	[Export(PropertyHint.Range, "1,20,1")]
	public int Width
	{
		get => _width;
		set { _width = Mathf.Max(1, value); Rebuild(); }
	}

	private int _thickness = 2;

	// How many copies are packed (interlocked, not stretched) into a single fall's girth — for
	// "el ancho/grosor" of one stream. 1 = the sheet's native thin stream.
	[Export(PropertyHint.Range, "1,8,1")]
	public int Thickness
	{
		get => _thickness;
		set { _thickness = Mathf.Max(1, value); Rebuild(); }
	}

	private int _height = 4;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Height
	{
		get => _height;
		set { _height = Mathf.Max(1, value); Rebuild(); }
	}

	private bool _hasTop = true;

	[Export]
	public bool HasTop
	{
		get => _hasTop;
		set { _hasTop = value; Rebuild(); }
	}

	private bool _hasBottom = true;

	// Off = the cascade falls into an open pit instead of landing in a pool — see the class
	// comment for why no extra "infinite" hazard collision is needed for that case.
	[Export]
	public bool HasBottom
	{
		get => _hasBottom;
		set { _hasBottom = value; Rebuild(); }
	}

	private bool _instantKill = true;

	[Export]
	public bool InstantKill
	{
		get => _instantKill;
		set { _instantKill = value; Rebuild(); }
	}

	private int _damage = 20;

	// Only used when InstantKill is off — how much damage a touch deals instead of killing outright.
	[Export]
	public int Damage
	{
		get => _damage;
		set { _damage = value; Rebuild(); }
	}

	private float _knockbackForce = 300f;

	// Only used when InstantKill is off — how hard the hit shoves the player back.
	[Export]
	public float KnockbackForce
	{
		get => _knockbackForce;
		set { _knockbackForce = value; Rebuild(); }
	}

	private float _flowFps = 8f;

	// How fast the lava cycles through the sheet's 20 frames — higher reads as a faster, more
	// turbulent flow.
	[Export]
	public float FlowFps
	{
		get => _flowFps;
		set { _flowFps = Mathf.Max(0.1f, value); Rebuild(); }
	}

	private LavaElement _element = LavaElement.Lava;

	// Swaps to GandalfHardcore's separate Water Tiles sheet (same pack, pixel-identical layout —
	// see LavaTileSheet) plus a blue glow instead of orange. Purely visual, doesn't change
	// InstantKill/Damage/KnockbackForce.
	[Export]
	public LavaElement Element
	{
		get => _element;
		set { _element = value; Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		Texture2D sheet = GD.Load<Texture2D>(LavaTileSheet.GetTilesetPath(_element));
		int tileSize = LavaTileSheet.TileSize;
		int contentStride = LavaTileSheet.ContentStride;

		SpriteFrames bodyFrames = LavaTileSheet.BuildFrames(sheet, LavaTileSheet.BodyRow, tileSize, _flowFps);
		SpriteFrames topFrames = _hasTop ? LavaTileSheet.BuildFrames(sheet, LavaTileSheet.TopCapRow, tileSize, _flowFps) : null;
		// BottomSplashRow/BottomSurfaceRow paint their FULL 32px cell (unlike Body/TopCap, which
		// only paint the right half) — cropped to ContentStride here so they place and gap
		// exactly like the rows above instead of touching across the fall boundary and erasing
		// the gap between separate falls.
		SpriteFrames bottomSplashFrames = _hasBottom ? LavaTileSheet.BuildFrames(sheet, LavaTileSheet.BottomSplashRow, contentStride, _flowFps) : null;
		SpriteFrames bottomSurfaceFrames = _hasBottom ? LavaTileSheet.BuildFrames(sheet, LavaTileSheet.BottomSurfaceRow, contentStride, _flowFps) : null;

		// Each fall is (Thickness-1) interlocked copies plus the last copy's own 32px cell; the
		// next fall starts a further ContentStride past that, which works out to a fixed 16px
		// gap between falls regardless of Thickness — same gap the sheet always had, just now
		// with a thick fall on each side of it instead of a single thin stream.
		int fallPitch = (_thickness - 1) * contentStride + tileSize;

		// Every column builds the same vertical sequence of rows, so the total stack height is
		// fixed regardless of Width — compute it once instead of overwriting it on each column.
		int stackHeight = (_hasTop ? tileSize : 0) + _height * tileSize + (_hasBottom ? 2 * tileSize : 0);

		int phase = 0;

		for (int cx = 0; cx < _width; cx++)
		{
			int fallX = cx * fallPitch;
			int y = 0;

			if (_hasTop)
			{
				for (int tx = 0; tx < _thickness; tx++)
					LavaTileSheet.AddTile(this, $"TopCap{cx + 1}_{tx + 1}", topFrames, fallX + tx * contentStride, y, phase++);
				y += tileSize;
			}

			for (int i = 0; i < _height; i++)
			{
				for (int tx = 0; tx < _thickness; tx++)
					LavaTileSheet.AddTile(this, $"Body{cx + 1}_{i + 1}_{tx + 1}", bodyFrames, fallX + tx * contentStride, y, phase++);
				y += tileSize;
			}

			if (_hasBottom)
			{
				for (int tx = 0; tx < _thickness; tx++)
					LavaTileSheet.AddTile(this, $"BottomSplash{cx + 1}_{tx + 1}", bottomSplashFrames, fallX + tx * contentStride, y, phase++);
				y += tileSize;

				for (int tx = 0; tx < _thickness; tx++)
					LavaTileSheet.AddTile(this, $"BottomSurface{cx + 1}_{tx + 1}", bottomSurfaceFrames, fallX + tx * contentStride, y - LavaTileSheet.BottomSurfaceTopInset, phase++);
				y += tileSize;
			}
		}

		Hazard hazard = Hazard.CreateArea(this, _instantKill, _damage, _knockbackForce);

		// One box per fall, sized to its actual painted content (contentStride..fallPitch, not
		// the full 0..fallPitch cell) — a single box spanning the whole fallPitch would hit-test
		// the empty left margin every tile carries and the visible gap between separate falls.
		float contentWidth = _thickness * contentStride;
		for (int cx = 0; cx < _width; cx++)
		{
			float fallX = cx * fallPitch;
			var hazardShape = new CollisionShape2D
			{
				Name = $"CollisionShape2D{cx + 1}",
				Position = new Vector2(fallX + contentStride + contentWidth / 2f, stackHeight / 2f),
				Shape = new RectangleShape2D { Size = new Vector2(contentWidth, stackHeight) },
			};
			hazard.AddChild(hazardShape);
			hazardShape.Owner = this;
		}

		float totalWidth = _width * fallPitch;
		ElementGlow.AddTo(this, new Vector2(totalWidth / 2f, stackHeight / 2f), _element);
	}
}
