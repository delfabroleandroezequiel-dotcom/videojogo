using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

public class MazeNode
{
	public int Id;
	public Vector2I GridPosition;
	public Vector2 Position;
	public bool IsMainPath;
	public bool IsShortcutBranch;
}

public class MazeConnection
{
	public int FromId;
	public int ToId;

	// True on the single edge that loops a shortcut branch back to (or near) the start — this is
	// the spot a Door/Lever gets hand-placed later to seal the shortcut until the player finds it.
	public bool IsShortcutSeal;
}

// Pure data — no scene nodes, no CorridorBlock/Mini* instancing. A reference layout to eyeball
// against a paper sketch or use as a positioning guide while placing real pieces by hand; see
// MazeGraphPreview for a way to actually look at one without writing a viewer yourself.
public class MazeGraph
{
	public int StartId;
	public int EndId;
	public List<MazeNode> Nodes = new();
	public List<MazeConnection> Connections = new();
}
