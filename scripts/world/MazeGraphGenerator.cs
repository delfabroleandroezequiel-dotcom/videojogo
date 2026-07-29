using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Random-walk generator for a MazeGraph: a main path from Start to End, some dead-end side
// branches for flavor, and ShortcutCount branches that always loop back to the Start node or
// close to it — never to some other random point in the maze. That's enforced structurally, not
// just by bias: a shortcut walk can only ever terminate by landing on the Start node or one of the
// first NearStartWindow main-path nodes after it (exactly or within NearStartRadius grid steps),
// everything else it might bump into is treated as a wall to route around. The resulting seal edge
// (MazeConnection.IsShortcutSeal) is where a Door/Lever gets hand-placed later.
// Grid-based (4 directions, one MazeNode per step) rather than free pixel placement — simpler to
// keep corridors non-overlapping and easy to reason about, at the cost of every corridor reading
// the same "length" (StepDistance) rather than varied CorridorBlock presets; this is a reference
// layout to eyeball or trace over by hand, not a finished level.
public static class MazeGraphGenerator
{
	// Right, Left, Down, Up — paired so index d's opposite is d^1 (0<->1, 2<->3), used to forbid
	// immediate U-turns without a lookup table.
	private static readonly Vector2I[] Directions =
	{
		new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
	};

	public static MazeGraph Generate(
		int seed,
		int mainPathLength = 16,
		int shortcutCount = 1,
		int branchCount = 3,
		int maxBranchLength = 4,
		int nearStartRadius = 1,
		int nearStartWindow = 2,
		float stepDistance = 768f)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)seed };
		var graph = new MazeGraph();
		var nodeGrid = new Dictionary<int, Vector2I>();
		var occupied = new Dictionary<Vector2I, int>();

		int startId = AddNode(graph, nodeGrid, occupied, Vector2I.Zero, stepDistance, isMainPath: true);
		graph.StartId = startId;

		var mainPathIds = new List<int> { startId };
		Vector2I gridPos = Vector2I.Zero;
		int prevDir = -1;
		for (int i = 1; i < mainPathLength; i++)
		{
			int dir = PickDirection(rng, prevDir, gridPos, occupied);
			gridPos += Directions[dir];
			int nodeId = AddNode(graph, nodeGrid, occupied, gridPos, stepDistance, isMainPath: true);
			Connect(graph, mainPathIds[^1], nodeId);
			mainPathIds.Add(nodeId);
			prevDir = dir;
		}
		graph.EndId = mainPathIds[^1];

		for (int b = 0; b < branchCount; b++)
		{
			int fromIndex = rng.RandiRange(1, Mathf.Max(1, mainPathLength - 2));
			GrowBranch(graph, rng, nodeGrid, occupied, mainPathIds[fromIndex], stepDistance, rng.RandiRange(1, Mathf.Max(1, maxBranchLength)));
		}

		// nearStartIds relies on ids 0..mainPathLength-1 being exactly the main path in order —
		// true because the main path above is the only thing generated before this point.
		var nearStartIds = new List<int> { startId };
		for (int i = 1; i <= nearStartWindow && i < mainPathIds.Count; i++)
			nearStartIds.Add(mainPathIds[i]);

		int lateStart = Mathf.Max(1, Mathf.RoundToInt(mainPathLength * 0.6f));
		for (int s = 0; s < shortcutCount; s++)
		{
			int fromIndex = rng.RandiRange(lateStart, mainPathLength - 1);
			GrowShortcut(graph, rng, nodeGrid, occupied, mainPathIds[fromIndex], stepDistance, nearStartIds, nearStartRadius);
		}

		return graph;
	}

	private static void GrowBranch(MazeGraph graph, RandomNumberGenerator rng, Dictionary<int, Vector2I> nodeGrid,
		Dictionary<Vector2I, int> occupied, int fromId, float stepDistance, int length)
	{
		int currentId = fromId;
		Vector2I currentGrid = nodeGrid[fromId];
		int prevDir = -1;

		for (int i = 0; i < length; i++)
		{
			List<int> candidates = CollectDirections(prevDir, currentGrid, occupied, allowUTurn: false, allowCross: false);
			if (candidates.Count == 0)
				return; // boxed in — end the dead end here rather than crossing an existing corridor

			int dir = candidates[rng.RandiRange(0, candidates.Count - 1)];
			Vector2I nextGrid = currentGrid + Directions[dir];
			int nodeId = AddNode(graph, nodeGrid, occupied, nextGrid, stepDistance, isMainPath: false);
			Connect(graph, currentId, nodeId);
			currentId = nodeId;
			currentGrid = nextGrid;
			prevDir = dir;
		}
	}

	private static void GrowShortcut(MazeGraph graph, RandomNumberGenerator rng, Dictionary<int, Vector2I> nodeGrid,
		Dictionary<Vector2I, int> occupied, int fromId, float stepDistance, List<int> nearStartIds, int nearStartRadius)
	{
		Vector2I target = nodeGrid[graph.StartId];
		Vector2I currentGrid = nodeGrid[fromId];
		int currentId = fromId;
		int prevDir = -1;
		const int maxSteps = 200; // generous safety cap — real walks end long before this at sane map sizes

		for (int step = 0; step < maxSteps; step++)
		{
			int dir = PickSteeredDirection(rng, prevDir, currentGrid, target, occupied, nearStartIds, nodeGrid);
			Vector2I nextGrid = currentGrid + Directions[dir];

			int dock = FindExactDock(nearStartIds, nodeGrid, nextGrid);
			if (dock >= 0)
			{
				Connect(graph, currentId, dock, isShortcutSeal: true);
				return;
			}

			int nodeId = AddNode(graph, nodeGrid, occupied, nextGrid, stepDistance, isMainPath: false, isShortcutBranch: true);
			Connect(graph, currentId, nodeId);
			currentId = nodeId;
			currentGrid = nextGrid;
			prevDir = dir;

			int nearbyDock = FindNearbyDock(nearStartIds, nodeGrid, currentGrid, nearStartRadius);
			if (nearbyDock >= 0)
			{
				Connect(graph, currentId, nearbyDock, isShortcutSeal: true);
				return;
			}
		}

		// Only reachable with a pathological combination of settings (tiny map, huge shortcut
		// count) — seal to Start directly rather than leaving the branch dangling unconnected.
		Connect(graph, currentId, graph.StartId, isShortcutSeal: true);
	}

	// Same legality rules as PickDirection (no U-turn, no crossing an existing corridor) PLUS: the
	// only occupied cell it's ever allowed to step onto is a near-start dock, which it's then
	// strongly weighted toward taking immediately. This is what makes "always ends at/near Start"
	// a structural guarantee instead of just a statistical tendency.
	private static int PickSteeredDirection(RandomNumberGenerator rng, int prevDir, Vector2I from, Vector2I target,
		Dictionary<Vector2I, int> occupied, List<int> nearStartIds, Dictionary<int, Vector2I> nodeGrid)
	{
		var candidates = new List<(int dir, float weight)>();
		Vector2I toTarget = target - from;

		for (int d = 0; d < 4; d++)
		{
			if (prevDir >= 0 && d == (prevDir ^ 1))
				continue;

			Vector2I next = from + Directions[d];
			bool isDock = FindExactDock(nearStartIds, nodeGrid, next) >= 0;
			if (occupied.ContainsKey(next) && !isDock)
				continue;

			float dot = Directions[d].X * toTarget.X + Directions[d].Y * toTarget.Y;
			// A small floor (not 0) on the homeward weight keeps the walk looking organic instead
			// of a laser-straight line back — occasional sideways steps are rare, not impossible.
			float weight = isDock ? 100f : Mathf.Max(0.15f, dot);
			candidates.Add((d, weight));
		}

		if (candidates.Count == 0)
		{
			// Fully boxed in by unrelated corridors — extremely unlikely at sane map sizes; allow
			// crossing rather than deadlocking generation.
			for (int d = 0; d < 4; d++)
				candidates.Add((d, 1f));
		}

		float total = 0f;
		foreach (var c in candidates)
			total += c.weight;

		float roll = rng.Randf() * total;
		float acc = 0f;
		foreach (var c in candidates)
		{
			acc += c.weight;
			if (roll <= acc)
				return c.dir;
		}
		return candidates[^1].dir;
	}

	private static int FindExactDock(List<int> nearStartIds, Dictionary<int, Vector2I> nodeGrid, Vector2I cell)
	{
		foreach (int id in nearStartIds)
			if (nodeGrid[id] == cell)
				return id;
		return -1;
	}

	private static int FindNearbyDock(List<int> nearStartIds, Dictionary<int, Vector2I> nodeGrid, Vector2I cell, int radius)
	{
		int bestId = -1;
		int bestDist = int.MaxValue;
		foreach (int id in nearStartIds)
		{
			Vector2I pos = nodeGrid[id];
			int dist = Mathf.Abs(pos.X - cell.X) + Mathf.Abs(pos.Y - cell.Y);
			if (dist <= radius && dist < bestDist)
			{
				bestDist = dist;
				bestId = id;
			}
		}
		return bestId;
	}

	private static int PickDirection(RandomNumberGenerator rng, int prevDir, Vector2I from, Dictionary<Vector2I, int> occupied)
	{
		List<int> candidates = CollectDirections(prevDir, from, occupied, allowUTurn: false, allowCross: false);
		if (candidates.Count == 0)
			candidates = CollectDirections(prevDir, from, occupied, allowUTurn: true, allowCross: false);
		if (candidates.Count == 0)
			candidates = CollectDirections(prevDir, from, occupied, allowUTurn: true, allowCross: true);

		// Bias toward continuing the same direction so the path doesn't zigzag every single step.
		if (prevDir >= 0 && candidates.Contains(prevDir) && rng.Randf() < 0.55f)
			return prevDir;

		return candidates[rng.RandiRange(0, candidates.Count - 1)];
	}

	private static List<int> CollectDirections(int prevDir, Vector2I from, Dictionary<Vector2I, int> occupied, bool allowUTurn, bool allowCross)
	{
		var list = new List<int>();
		for (int d = 0; d < 4; d++)
		{
			if (!allowUTurn && prevDir >= 0 && d == (prevDir ^ 1))
				continue;
			if (!allowCross && occupied.ContainsKey(from + Directions[d]))
				continue;
			list.Add(d);
		}
		return list;
	}

	private static int AddNode(MazeGraph graph, Dictionary<int, Vector2I> nodeGrid, Dictionary<Vector2I, int> occupied,
		Vector2I gridPos, float stepDistance, bool isMainPath, bool isShortcutBranch = false)
	{
		int id = graph.Nodes.Count;
		graph.Nodes.Add(new MazeNode
		{
			Id = id,
			GridPosition = gridPos,
			Position = new Vector2(gridPos.X, gridPos.Y) * stepDistance,
			IsMainPath = isMainPath,
			IsShortcutBranch = isShortcutBranch,
		});
		nodeGrid[id] = gridPos;
		occupied[gridPos] = id;
		return id;
	}

	private static void Connect(MazeGraph graph, int fromId, int toId, bool isShortcutSeal = false) =>
		graph.Connections.Add(new MazeConnection { FromId = fromId, ToId = toId, IsShortcutSeal = isShortcutSeal });
}
