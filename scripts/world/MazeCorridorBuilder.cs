using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Turns a MazeGraph into real CorridorBlock geometry — but as one square CELL per node, not one
// elongated piece per connection. CorridorBlock.cs/.tscn are untouched; this only ever sets the
// same 4 independent Has* flags it already exposes (Floor/Ceiling/LeftWall/RightWall — all
// togglable regardless of Orientation), just derived per-cell from the graph's real adjacency
// instead of a blanket "Lateral = floor+ceiling, Vertical = walls" rule per edge.
// Each node becomes a CellSize x CellSize room. A side opens (wall off) only if that node has an
// actual MazeConnection to whatever's sitting in the neighboring grid cell — not just because a
// neighbor happens to be there, which matters for a random walk that curls back near an unrelated
// corridor without an edge to it. Two connected cells end up with both facing walls open, forming
// one continuous room with no seam; a dead end (only 1 connection) gets exactly 1 open side and 3
// closed automatically, with no separate end-cap step needed — sealing falls straight out of the
// per-cell derivation instead of being something to remember per edge.
// The shortcut's seal connection still gets a Marker2D (named "ShortcutSeal", printed to the
// Output panel) at the boundary between the two cells it joins, for hand-placing the door/lever.
// [Tool] so the layout previews live in the editor, before ever pressing Play.
[Tool]
public partial class MazeCorridorBuilder : Node2D
{
	private const string CorridorBlockScenePath = "res://scenes/world/Rehusables/CorridorBlock.tscn";

	// Right, Left, Down, Up — same order/pairing as MazeGraphGenerator's own Directions.
	private static readonly Vector2I[] Directions =
	{
		new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
	};

	[Export] public int Seed;

	private int _mainPathLength = 16;

	[Export(PropertyHint.Range, "4,60,1")]
	public int MainPathLength
	{
		get => _mainPathLength;
		set { _mainPathLength = Mathf.Max(4, value); Regenerate(); }
	}

	private int _shortcutCount = 1;

	[Export(PropertyHint.Range, "0,10,1")]
	public int ShortcutCount
	{
		get => _shortcutCount;
		set { _shortcutCount = Mathf.Max(0, value); Regenerate(); }
	}

	private int _branchCount = 3;

	[Export(PropertyHint.Range, "0,10,1")]
	public int BranchCount
	{
		get => _branchCount;
		set { _branchCount = Mathf.Max(0, value); Regenerate(); }
	}

	private int _maxBranchLength = 4;

	[Export(PropertyHint.Range, "1,10,1")]
	public int MaxBranchLength
	{
		get => _maxBranchLength;
		set { _maxBranchLength = Mathf.Max(1, value); Regenerate(); }
	}

	private int _nearStartRadius = 1;

	[Export(PropertyHint.Range, "0,5,1")]
	public int NearStartRadius
	{
		get => _nearStartRadius;
		set { _nearStartRadius = Mathf.Max(0, value); Regenerate(); }
	}

	private int _nearStartWindow = 2;

	[Export(PropertyHint.Range, "0,10,1")]
	public int NearStartWindow
	{
		get => _nearStartWindow;
		set { _nearStartWindow = Mathf.Max(0, value); Regenerate(); }
	}

	private float _cellSize = GameConfig.CorridorCrossSizePresets[1];

	// Both the grid spacing the generator walks on AND each cell's physical size — the two have to
	// match for neighboring cells to sit exactly edge-to-edge with no gap and no overlap.
	[Export]
	public float CellSize
	{
		get => _cellSize;
		set { _cellSize = Mathf.Max(32f, value); Regenerate(); }
	}

	private float _wallThickness = 32f;

	[Export]
	public float WallThickness
	{
		get => _wallThickness;
		set { _wallThickness = Mathf.Max(4f, value); Regenerate(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);
	[Export] public Color CeilingFillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	// Always reads back false — a button, not a persistent flag. Setting it true (re)builds the
	// whole cell layout from scratch, either replaying Seed or drawing a fresh random one.
	[Export]
	public bool Reroll
	{
		get => false;
		set { if (value) DoReroll(); }
	}

	private int _lastSeedUsed;
	private bool _initialized;

	public override void _Ready()
	{
		_initialized = true;
		DoReroll();
	}

	private void DoReroll()
	{
		_lastSeedUsed = Seed != 0 ? Seed : (int)GD.Randi();
		Regenerate();
	}

	private void Regenerate()
	{
		// See RopeAccordion.Rebuild for why _initialized gates this — property setters restoring
		// this node's saved state before _Ready runs can otherwise reenter Instantiate<CorridorBlock>()
		// mid scene-instantiation and throw a spurious InvalidCastException.
		if (!_initialized || !IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		MazeGraph graph = MazeGraphGenerator.Generate(
			_lastSeedUsed, _mainPathLength, _shortcutCount, _branchCount, _maxBranchLength,
			_nearStartRadius, _nearStartWindow, _cellSize);

		GD.Print($"[MazeCorridorBuilder] seed={_lastSeedUsed} nodes={graph.Nodes.Count} connections={graph.Connections.Count}");

		var adjacency = new Dictionary<int, HashSet<int>>();
		foreach (MazeNode node in graph.Nodes)
			adjacency[node.Id] = new HashSet<int>();
		foreach (MazeConnection connection in graph.Connections)
		{
			adjacency[connection.FromId].Add(connection.ToId);
			adjacency[connection.ToId].Add(connection.FromId);
		}

		var occupied = new Dictionary<Vector2I, int>();
		foreach (MazeNode node in graph.Nodes)
			occupied[node.GridPosition] = node.Id;

		PackedScene corridorScene = GD.Load<PackedScene>(CorridorBlockScenePath);
		foreach (MazeNode node in graph.Nodes)
			BuildCell(corridorScene, node, adjacency[node.Id], occupied);

		int sealIndex = 0;
		foreach (MazeConnection connection in graph.Connections)
		{
			if (!connection.IsShortcutSeal)
				continue;

			Vector2 sealPosition = (CellCenter(graph.Nodes[connection.FromId]) + CellCenter(graph.Nodes[connection.ToId])) / 2f;
			var seal = new Marker2D { Name = $"ShortcutSeal{++sealIndex}", Position = sealPosition };
			AddChild(seal);
			seal.Owner = this;
			GD.Print($"[MazeCorridorBuilder]   atajo: nodo {connection.FromId} -> nodo {connection.ToId} en {sealPosition} (poné la puerta/lever acá)");
		}
	}

	private Vector2 CellCenter(MazeNode node) =>
		new((node.GridPosition.X + 0.5f) * _cellSize, (node.GridPosition.Y + 0.5f) * _cellSize);

	private void BuildCell(PackedScene corridorScene, MazeNode node, HashSet<int> neighbors, Dictionary<Vector2I, int> occupied)
	{
		bool HasOpenSide(Vector2I dir) =>
			occupied.TryGetValue(node.GridPosition + dir, out int neighborId) && neighbors.Contains(neighborId);

		bool openRight = HasOpenSide(Directions[0]);
		bool openLeft = HasOpenSide(Directions[1]);
		bool openDown = HasOpenSide(Directions[2]);
		bool openUp = HasOpenSide(Directions[3]);

		CorridorBlock cell = corridorScene.Instantiate<CorridorBlock>();
		cell.Name = $"MazeCell{node.Id}";
		cell.Orientation = CorridorOrientation.Lateral;
		cell.Length = _cellSize;
		cell.CrossSize = _cellSize;
		cell.WallThickness = _wallThickness;
		cell.FillColor = FillColor;
		cell.CeilingFillColor = CeilingFillColor;
		cell.HasFloor = !openDown;
		cell.HasCeiling = !openUp;
		cell.HasLeftWall = !openLeft;
		cell.HasRightWall = !openRight;

		// Lateral's origin sits at the walkable floor line with the room interior above it (see
		// CorridorBlock.Rebuild) — so a cell at grid row gy needs its origin at the BOTTOM of that
		// row, (gy+1)*CellSize, for the interior to land exactly on [gy*CellSize, (gy+1)*CellSize].
		cell.Position = new Vector2(node.GridPosition.X * _cellSize, (node.GridPosition.Y + 1) * _cellSize);
		AddChild(cell);
		cell.Owner = this;
	}
}
