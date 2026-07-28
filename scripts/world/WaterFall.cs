using Godot;

namespace Metroidvania.World;

// Vertical hazard built from the HEROES_FANTASY water tileset (a much richer painted-water
// source than GandalfHardcore's). The flow "animation" isn't a scroll or a shimmer — diffing
// the reference GIF this pack's demo scenes ship with (assets/sprites/deco/lkzv1r.gif) frame by
// frame, then directly comparing the sheet's 4 repeating 64px units pixel-for-pixel, showed each
// unit is a distinct hand-painted variant of the same wave motif (confirmed visually: the splash
// highlight at the bottom sits in a different spot in each one), not a single tile repeated.
// So this plays through those 4 variants as real AnimatedSprite2D frames — same idea as LavaFall,
// just with 4 explicit x-offsets instead of a uniform stride, since the sheet packs a wide and a
// narrow stream into every 64px unit with hard transparent cuts between them (VariantXOffsets
// below skips the narrow one and the gaps entirely).
// Element (default Water) can flip to Lava: same 4 frames recolored by luminance onto a
// shadow/mid/highlight gradient via FlowingLiquid.gdshader, instead of swapping to a different
// sheet — see that file for why a plain color multiply doesn't work here.
// Height/Width stack copies of the frame row — same "set a count, get that many tiles" idea as
// SpikeAccordion. Each tile starts on a different frame (its stack index, mod 4) so they don't
// all flip in lockstep, the same stagger trick LavaFall uses.
// Opacity multiplies on top of the sheet's own already-semi-transparent pixels; HasFrontLayer
// adds a second pass using the pack's water_front.png (same art, baked even more transparent)
// drawn above EVERYTHING in the scene (see the Z_INDEX comment below) — per the pack's own
// layer_guide.png, that's what the reference screenshots' translucent depth actually comes from.
// A single Hazard (InstantKill by default) covers the built stack, plus an ambient ElementGlow
// (same helper LavaFall/LavaFloor use) so the water reads brighter/richer instead of flat.
// [Tool] so the stack shows in the editor before ever pressing Play (the editor previews
// AnimatedSprite2D live, same as LavaFall).
[Tool]
public partial class WaterFall : Node2D
{
	private const string TexturePath = "res://assets/external/HEROES_FANTASY-Platformer_tileset/spritesheets/water.png";
	private const string FrontTexturePath = "res://assets/external/HEROES_FANTASY-Platformer_tileset/spritesheets/water_front.png";
	private const string ShaderPath = "res://resources/shaders/FlowingLiquid.gdshader";
	private const string AnimName = "default";

	// Measured directly on the sheet: 4 clean 64px repeat units (x=0-63, 64-127, 128-191,
	// 192-255), each with real transparent gap columns splitting it into a ~45px-wide stream and
	// a ~14px-wide one. Comparing the 4 wide-stream crops pixel-for-pixel confirmed they're
	// genuinely different hand-painted variants (the splash highlight sits in a different spot
	// in each), not the same tile repeated.
	// Each stream's own left edge (right after its gap) is a bright rim-light highlight, same
	// deal as the top-row brightness spike that caused the vertical seam — searching every
	// (start, width) pair for the closest left/right luminance match inside one stream's safe
	// x=1..46 band landed on a local offset of +4..+36 (33px, ~0 luminance gap at the seam).
	// These are that offset already applied to each stream's own start.
	private static readonly int[] VariantXOffsets = { 5, 69, 133, 197 };
	private const int VariantWidth = 33;

	// The full 48px-tall body (y=4..51) starts breaking up into the arch-shaped drip tips around
	// y=52+, so a height-48 crop that starts anywhere past y=4 ends up grabbing the top of those
	// arches — that's the "teeth" pattern that showed up repeating in-game. Shrinking the crop
	// and searching every (start, height) pair for the closest start/end luminance match, fully
	// inside the arch-free y=4..51 band, landed on y=13..45: a 33px band with a ~0 luminance gap
	// at the seam (vs. ~37 for the original 48px crop) and zero arch content.
	private const int VariantY = 13;
	private const int VariantHeight = 33;

	private int _height = 4;

	[Export(PropertyHint.Range, "1,30,1")]
	public int Height
	{
		get => _height;
		set { _height = Mathf.Max(1, value); Rebuild(); }
	}

	private int _width = 1;

	// Repeats the whole column sideways — adjacent copies read as one wider fall, not separate
	// streams.
	[Export(PropertyHint.Range, "1,20,1")]
	public int Width
	{
		get => _width;
		set { _width = Mathf.Max(1, value); Rebuild(); }
	}

	private float _opacity = 0.2f;

	// water.png's own pixels are already semi-transparent (~64% alpha, measured directly on the
	// sheet) — this multiplies on top of that. 0.2 (playtested against a dark cave background,
	// with ElementGlow) is what actually reads as misty/see-through instead of a flat teal wall;
	// 1 = use the sheet's own alpha as-is, noticeably more opaque than the reference look.
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Opacity
	{
		get => _opacity;
		set { _opacity = Mathf.Clamp(value, 0f, 1f); Rebuild(); }
	}

	private bool _hasFrontLayer = true;

	// Draws a second pass on top using water_front.png (the same art, pre-baked even more
	// transparent — measured ~51% alpha vs the back layer's ~64%) — this is what the reference
	// screenshots' layered depth comes from, not a single flat sprite.
	[Export]
	public bool HasFrontLayer
	{
		get => _hasFrontLayer;
		set { _hasFrontLayer = value; Rebuild(); }
	}

	private LavaElement _element = LavaElement.Water;

	[Export]
	public LavaElement Element
	{
		get => _element;
		set { _element = value; Rebuild(); }
	}

	private float _flowFps = 6.67f;

	// Frames per second the 4 wave variants cycle at — 6.67 (~150ms/frame) matches what the
	// reference GIF actually measured out to.
	[Export]
	public float FlowFps
	{
		get => _flowFps;
		set { _flowFps = Mathf.Max(0.1f, value); Rebuild(); }
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

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		SpriteFrames frames = BuildFrames(texture);
		ShaderMaterial material = BuildMaterial();

		float tileWidth = VariantWidth;
		float tileHeight = VariantHeight;
		int phase = 0;

		for (int cx = 0; cx < _width; cx++)
		{
			for (int i = 0; i < _height; i++)
			{
				AddTile($"Tile{cx + 1}_{i + 1}", frames, material, cx * tileWidth, i * tileHeight, phase++, zIndex: 0);
			}
		}

		if (_hasFrontLayer)
		{
			Texture2D frontTexture = GD.Load<Texture2D>(FrontTexturePath);
			SpriteFrames frontFrames = BuildFrames(frontTexture);
			ShaderMaterial frontMaterial = BuildMaterial();

			// Per the pack's own layer_guide.png, "FRONT WATERFALL" draws above everything —
			// player, trees, foreground decoration, not just this node's own back layer — so it
			// needs an explicit Z_INDEX rather than relying on sibling draw order. No established
			// z-index convention in this project yet, so this just picks a value high enough to
			// sit above anything reasonable in a level.
			for (int cx = 0; cx < _width; cx++)
			{
				for (int i = 0; i < _height; i++)
				{
					AddTile($"Front{cx + 1}_{i + 1}", frontFrames, frontMaterial, cx * tileWidth, i * tileHeight, phase++, zIndex: 100);
				}
			}
		}

		Hazard hazard = Hazard.CreateArea(this, _instantKill, _damage, _knockbackForce);

		float totalWidth = _width * tileWidth;
		float totalHeight = _height * tileHeight;
		var hazardShape = new CollisionShape2D
		{
			Name = "CollisionShape2D",
			Position = new Vector2(totalWidth / 2f, totalHeight / 2f),
			Shape = new RectangleShape2D { Size = new Vector2(totalWidth, totalHeight) },
		};
		hazard.AddChild(hazardShape);
		hazardShape.Owner = this;

		ElementGlow.AddTo(this, new Vector2(totalWidth / 2f, totalHeight / 2f), _element);
	}

	private SpriteFrames BuildFrames(Texture2D texture)
	{
		var frames = new SpriteFrames();
		frames.SetAnimationLoopMode(AnimName, SpriteFrames.LoopMode.Linear);
		frames.SetAnimationSpeed(AnimName, _flowFps);

		foreach (int x in VariantXOffsets)
		{
			var atlas = new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(x, VariantY, VariantWidth, VariantHeight),
			};
			frames.AddFrame(AnimName, atlas);
		}

		return frames;
	}

	private ShaderMaterial BuildMaterial()
	{
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		material.SetShaderParameter("is_lava", _element == LavaElement.Lava);
		material.SetShaderParameter("opacity_multiplier", _opacity);
		return material;
	}

	private void AddTile(string name, SpriteFrames frames, ShaderMaterial material, float x, float y, int phase, int zIndex)
	{
		var sprite = new AnimatedSprite2D
		{
			Name = name,
			SpriteFrames = frames,
			Animation = AnimName,
			Centered = false,
			Position = new Vector2(x, y),
			Frame = phase % VariantXOffsets.Length,
			Material = material,
			ZIndex = zIndex,
		};
		AddChild(sprite);
		sprite.Owner = this;
		sprite.Play(AnimName);
	}
}
