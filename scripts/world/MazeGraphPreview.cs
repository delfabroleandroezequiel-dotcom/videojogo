using Godot;

namespace Metroidvania.World;

// Editor-only viewer for MazeGraphGenerator — draws the graph (main path, dead-end branches,
// shortcut branches, and the shortcut's seal edge all color-coded) and prints each shortcut's
// seal position to the Output panel, so it's actually inspectable without writing throwaway code
// every time. Toggle Reroll on to draw a fresh layout; it flips itself back off automatically (the
// checkbox is just a button in disguise — see the getter always returning false).
// Not meant to end up in a shipped scene: it produces no CorridorBlock/Mini* pieces and no
// collision, only a MazeGraph in memory plus this preview drawing. Drop it into an empty scene,
// reroll until a layout looks right, then use it as a positioning reference while placing real
// pieces by hand — same role MeasuringRuler plays for distances.
[Tool]
public partial class MazeGraphPreview : Node2D
{
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

	// How many grid steps from Start (or from one of the first NearStartWindow main-path nodes)
	// still counts as "close enough" for a shortcut to dock without landing exactly on it.
	[Export(PropertyHint.Range, "0,5,1")]
	public int NearStartRadius
	{
		get => _nearStartRadius;
		set { _nearStartRadius = Mathf.Max(0, value); Regenerate(); }
	}

	private int _nearStartWindow = 2;

	// How many main-path nodes past Start also count as valid shortcut docks — 0 means a shortcut
	// can only ever reconnect to the exact Start node.
	[Export(PropertyHint.Range, "0,10,1")]
	public int NearStartWindow
	{
		get => _nearStartWindow;
		set { _nearStartWindow = Mathf.Max(0, value); Regenerate(); }
	}

	private float _stepDistance = 768f;

	// Pixel distance per grid step — defaults to CorridorBlock's Medium length preset so the
	// layout reads at roughly real map scale.
	[Export]
	public float StepDistance
	{
		get => _stepDistance;
		set { _stepDistance = Mathf.Max(8f, value); Regenerate(); }
	}

	// Always reads back false — this is a button, not a persistent flag. Setting it true draws a
	// brand new random layout (or replays Seed, if it's nonzero, for a reproducible one).
	[Export]
	public bool Reroll
	{
		get => false;
		set { if (value) DoReroll(); }
	}

	private MazeGraph _graph;
	private int _lastSeedUsed;

	public override void _Ready() => DoReroll();

	private void DoReroll()
	{
		_lastSeedUsed = Seed != 0 ? Seed : (int)GD.Randi();
		Regenerate();
	}

	private void Regenerate()
	{
		if (!IsInsideTree())
			return;

		_graph = MazeGraphGenerator.Generate(
			_lastSeedUsed, _mainPathLength, _shortcutCount, _branchCount, _maxBranchLength,
			_nearStartRadius, _nearStartWindow, _stepDistance);

		QueueRedraw();
		PrintSummary();
	}

	private void PrintSummary()
	{
		GD.Print($"[MazeGraphPreview] seed={_lastSeedUsed} nodes={_graph.Nodes.Count} connections={_graph.Connections.Count}");
		foreach (MazeConnection connection in _graph.Connections)
		{
			if (!connection.IsShortcutSeal)
				continue;

			Vector2 sealPosition = _graph.Nodes[connection.ToId].Position;
			GD.Print($"[MazeGraphPreview]   atajo: nodo {connection.FromId} -> nodo {connection.ToId} en {sealPosition} (poné la puerta/lever acá)");
		}
	}

	public override void _Draw()
	{
		if (_graph == null)
			return;

		foreach (MazeConnection connection in _graph.Connections)
		{
			Vector2 from = _graph.Nodes[connection.FromId].Position;
			Vector2 to = _graph.Nodes[connection.ToId].Position;
			Color color = connection.IsShortcutSeal ? new Color(1f, 0.55f, 0.15f) : Colors.White;
			float width = connection.IsShortcutSeal ? 5f : 2f;
			DrawLine(from, to, color, width);
		}

		foreach (MazeNode node in _graph.Nodes)
		{
			Color color = node.Id == _graph.StartId ? new Color(0.3f, 1f, 0.4f)
				: node.Id == _graph.EndId ? new Color(1f, 0.25f, 0.25f)
				: node.IsShortcutBranch ? new Color(1f, 0.75f, 0.3f)
				: node.IsMainPath ? Colors.White
				: new Color(0.6f, 0.6f, 0.6f);
			float radius = node.Id == _graph.StartId || node.Id == _graph.EndId ? 14f : 8f;
			DrawCircle(node.Position, radius, color);
		}
	}
}
