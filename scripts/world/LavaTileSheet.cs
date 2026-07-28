using Godot;

namespace Metroidvania.World;

public enum LavaElement
{
	Lava,
	Water,
}

// Shared plumbing for the GandalfHardcore Lava Tiles sheet (32x32 tiles, 20 columns x 11 rows),
// used by both LavaFall (vertical) and LavaFloor (horizontal). The sheet comes from an .ase
// (Aseprite) source and its 20 columns are an animation strip, not static variants, so every row
// gets turned into a looping SpriteFrames (built in code, no separate .tres) and played on an
// AnimatedSprite2D — that's what makes the lava actually flow instead of sitting frozen.
// Water isn't a recolor of the lava art: GandalfHardcore's separate "Animated Water Tiles" sheet
// (same pack) is pixel-for-pixel the same 32x32/20x11 layout — same insets at the same rows,
// verified directly on both sheets — just painted teal instead of orange. So Element just swaps
// which sheet GetTilesetPath points at; every row constant/offset below applies to both as-is.
public static class LavaTileSheet
{
	public const string LavaTilesetPath = "res://assets/external/gandalfhardcore/GandalfHardcore Hell Asset Pack 32x32/GandalfHardcore Lava Tiles.png";
	public const string WaterTilesetPath = "res://assets/external/gandalfhardcore/platformer/GandalfHardcore FREE Platformer Assets/Animated Sprites/GandalfHardcore Animated Water Tiles.png";
	public const int TileSize = 32;
	public const int ColumnVariants = 20;

	public const int TopCapRow = 3;
	public const int BodyRow = 4;
	public const int BottomSplashRow = 6;
	public const int BottomSurfaceRow = 7;

	// Measured directly on the sheet: TopCapRow/BodyRow only paint the right half of their 32px
	// cell (x=16..31) — tiling at the full TileSize leaves a 16px gap (the sheet's native
	// "separate thin stream" look); packing copies at this stride instead interlocks them into a
	// solid, un-stretched mass.
	public const int ContentStride = 16;

	// BottomSurfaceRow's own art leaves the top 4px of its 32px cell transparent (unlike
	// BottomSplashRow, which fills its cell edge to edge) — placed flush right after a splash
	// row, that shows as a gap. Nudge it up by this to close it.
	public const int BottomSurfaceTopInset = 4;

	private const string AnimName = "default";

	public static string GetTilesetPath(LavaElement element) =>
		element == LavaElement.Water ? WaterTilesetPath : LavaTilesetPath;

	public static SpriteFrames BuildFrames(Texture2D sheet, int row, int regionWidth, float fps)
	{
		var frames = new SpriteFrames();
		frames.SetAnimationLoopMode(AnimName, SpriteFrames.LoopMode.Linear);
		frames.SetAnimationSpeed(AnimName, fps);

		for (int column = 0; column < ColumnVariants; column++)
		{
			var atlas = new AtlasTexture
			{
				Atlas = sheet,
				Region = new Rect2(column * TileSize, row * TileSize, regionWidth, TileSize),
			};
			frames.AddFrame(AnimName, atlas);
		}

		return frames;
	}

	public static AnimatedSprite2D AddTile(Node2D parent, string name, SpriteFrames frames, float x, float y, int phase)
	{
		var sprite = new AnimatedSprite2D
		{
			Name = name,
			SpriteFrames = frames,
			Animation = AnimName,
			Centered = false,
			Position = new Vector2(x, y),
			Frame = phase % ColumnVariants,
		};
		parent.AddChild(sprite);
		sprite.Owner = parent;
		sprite.Play(AnimName);
		return sprite;
	}
}
