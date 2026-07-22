using Godot;

namespace Metroidvania.World;

// Horizontal sibling to LavaFall: the same splash-into-pool cap (BottomSplashRow +
// BottomSurfaceRow, see LavaTileSheet) used standalone as a lava floor/moat instead of as the
// landing under a fall. Those two rows are already seamless left-to-right on the sheet (unlike
// the vertical column tiles), so Length just repeats them at the sheet's native TileSize stride
// — no interlocking trick needed, only the same BottomSurfaceTopInset nudge LavaFall uses to
// join the two rows vertically with no gap.
// Same wipe-and-respawn idea as SpikeAccordion: set Length and get that many tiles, each
// starting its flow animation on a different frame so the strip doesn't flicker in lockstep.
// HasSplash/HasSurface (both on by default) crop away either half, same idea as LavaFall's
// HasTop/HasBottom — off leaves just the flat pool surface, off leaves just the jagged splash.
// Element switches to GandalfHardcore's separate Water Tiles sheet (same pack, pixel-identical
// layout — see LavaTileSheet) plus a blue glow instead of orange — purely visual.
// A single Hazard (InstantKill by default, Damage/KnockbackForce used when it's off) covers the
// whole strip.
// [Tool] so it (and its animation — Godot's editor previews AnimatedSprite2D live) shows before
// ever pressing Play.
[Tool]
public partial class LavaFloor : Node2D
{
	private int _length = 8;

	[Export(PropertyHint.Range, "1,60,1")]
	public int Length
	{
		get => _length;
		set { _length = Mathf.Max(1, value); Rebuild(); }
	}

	private bool _hasSplash = true;

	// Off = crops away the jagged top half, leaving just the flat pool surface.
	[Export]
	public bool HasSplash
	{
		get => _hasSplash;
		set { _hasSplash = value; Rebuild(); }
	}

	private bool _hasSurface = true;

	// Off = crops away the bottom half, leaving just the jagged splash row.
	[Export]
	public bool HasSurface
	{
		get => _hasSurface;
		set { _hasSurface = value; Rebuild(); }
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
		int inset = LavaTileSheet.BottomSurfaceTopInset;

		int phase = 0;

		if (_hasSplash)
		{
			SpriteFrames splashFrames = LavaTileSheet.BuildFrames(sheet, LavaTileSheet.BottomSplashRow, tileSize, _flowFps);
			for (int i = 0; i < _length; i++)
				LavaTileSheet.AddTile(this, $"Splash{i + 1}", splashFrames, i * tileSize, 0, phase++);
		}

		if (_hasSurface)
		{
			// Joined under a splash row, sit right below it (the usual look). Alone, shift up by
			// the row's own inset so its visible content starts flush at y=0 instead of leaving
			// that margin as wasted empty space at the top.
			float surfaceY = _hasSplash ? tileSize - inset : -inset;
			SpriteFrames surfaceFrames = LavaTileSheet.BuildFrames(sheet, LavaTileSheet.BottomSurfaceRow, tileSize, _flowFps);
			for (int i = 0; i < _length; i++)
				LavaTileSheet.AddTile(this, $"Surface{i + 1}", surfaceFrames, i * tileSize, surfaceY, phase++);
		}

		Hazard hazard = Hazard.CreateArea(this, _instantKill, _damage, _knockbackForce);

		// Each half contributes its own height independently — Surface's is shorter by `inset`
		// since it's the row that gets nudged up (see surfaceY above), whether or not Splash is
		// also present.
		float totalWidth = _length * tileSize;
		float totalHeight = (_hasSplash ? tileSize : 0) + (_hasSurface ? tileSize - inset : 0);
		var hazardShape = new CollisionShape2D
		{
			Name = "CollisionShape2D",
			Position = new Vector2(totalWidth / 2f, totalHeight / 2f),
			Shape = new RectangleShape2D { Size = new Vector2(totalWidth, totalHeight) },
		};
		hazard.AddChild(hazardShape);
		hazardShape.Owner = this;

		LavaTileSheet.AddGlow(this, new Vector2(totalWidth / 2f, totalHeight / 2f), _element);
	}
}
