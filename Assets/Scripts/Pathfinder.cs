using System.Collections.Generic;
using UnityEngine;

// Finds a walkable route between two points, going around walls.
//
// The map is already a grid of tiles, so we reuse it: every tile is one "cell",
// and a cell counts as blocked if a wall collider sits on it. Nothing to set up
// in the scene — this is a plain utility class, not a component you attach.
//
// The algorithm is A* ("A star"). It explores cells outward from the start,
// always continuing from whichever one looks most promising: the distance
// already walked to reach it, PLUS a straight-line guess of what's left to the
// goal. That guess is what keeps it heading toward the goal instead of
// spreading out blindly in every direction.
public static class Pathfinder
{
    private const float cellSize = 1f;         // one tile = one world unit
    private const int maxCellsSearched = 2000; // safety net, so a walled-off goal can't freeze the game

    private const float straightCost = 1f;
    private const float diagonalCost = 1.41421356f; // a diagonal step is longer (√2), so it costs more

    // One cell while it's being considered.
    private class Node
    {
        public Vector2Int cell;
        public float gCost;  // distance actually walked from the start
        public float fCost;  // gCost + the straight-line guess to the goal
        public Node parent;  // which node we arrived from — this is the trail we follow back
    }

    // Returns the world positions to walk through to get from `start` to `goal`
    // without crossing walls, or null if there's no route.
    public static List<Vector2> FindPath(Vector2 start, Vector2 goal, LayerMask obstacleMask)
    {
        Vector2Int startCell = WorldToCell(start);
        Vector2Int goalCell = WorldToCell(goal);

        if (startCell == goalCell)
            return new List<Vector2> { goal }; // already there, just step to the exact spot

        if (IsBlocked(goalCell, obstacleMask))
            return null; // the goal itself is inside a wall

        List<Node> open = new List<Node>();                     // found, but not examined yet
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();  // already examined, don't revisit
        Dictionary<Vector2Int, Node> openLookup = new Dictionary<Vector2Int, Node>();

        Node startNode = new Node { cell = startCell, gCost = 0f, fCost = Heuristic(startCell, goalCell) };
        open.Add(startNode);
        openLookup[startCell] = startNode;

        int searched = 0;

        while (open.Count > 0 && searched++ < maxCellsSearched)
        {
            Node current = PopLowestCost(open);
            openLookup.Remove(current.cell);
            closed.Add(current.cell);

            if (current.cell == goalCell)
                return BuildRoute(current, goal);

            foreach (Vector2Int neighbour in Neighbours(current.cell, obstacleMask))
            {
                if (closed.Contains(neighbour)) continue;

                bool isDiagonal = neighbour.x != current.cell.x && neighbour.y != current.cell.y;
                float newG = current.gCost + (isDiagonal ? diagonalCost : straightCost);

                if (openLookup.TryGetValue(neighbour, out Node existing))
                {
                    // Already queued from somewhere else — keep this route only if it's shorter.
                    if (newG < existing.gCost)
                    {
                        existing.gCost = newG;
                        existing.fCost = newG + Heuristic(neighbour, goalCell);
                        existing.parent = current;
                    }
                }
                else
                {
                    open.Add(new Node
                    {
                        cell = neighbour,
                        gCost = newG,
                        fCost = newG + Heuristic(neighbour, goalCell),
                        parent = current
                    });
                    openLookup[neighbour] = open[open.Count - 1];
                }
            }
        }

        return null; // walled off, or bigger than our search limit
    }

    // Takes out the most promising node — the lowest fCost.
    private static Node PopLowestCost(List<Node> open)
    {
        int best = 0;
        for (int i = 1; i < open.Count; i++)
            if (open[i].fCost < open[best].fCost)
                best = i;

        Node node = open[best];
        open.RemoveAt(best);
        return node;
    }

    // The straight-line guess of what's left. It's important that this never
    // OVERestimates — that's what guarantees A* returns the shortest route
    // instead of just some route that happens to work.
    private static float Heuristic(Vector2Int from, Vector2Int to)
    {
        return Vector2Int.Distance(from, to);
    }

    // The 8 surrounding cells, minus the blocked ones. A diagonal is only
    // allowed when both cells beside it are free — otherwise the enemy would
    // slip through the corner where two walls meet.
    private static IEnumerable<Vector2Int> Neighbours(Vector2Int cell, LayerMask obstacleMask)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // that's the cell itself

                Vector2Int neighbour = new Vector2Int(cell.x + dx, cell.y + dy);
                if (IsBlocked(neighbour, obstacleMask)) continue;

                if (dx != 0 && dy != 0)
                {
                    bool sideA = IsBlocked(new Vector2Int(cell.x + dx, cell.y), obstacleMask);
                    bool sideB = IsBlocked(new Vector2Int(cell.x, cell.y + dy), obstacleMask);
                    if (sideA || sideB) continue; // would cut through a corner
                }

                yield return neighbour;
            }
        }
    }

    // Blocked if any wall collider overlaps the cell. The box is shrunk a little
    // so it only catches walls actually on this cell, not the ones next door.
    private static bool IsBlocked(Vector2Int cell, LayerMask obstacleMask)
    {
        return Physics2D.OverlapBox(CellToWorld(cell), Vector2.one * (cellSize * 0.9f), 0f, obstacleMask) != null;
    }

    private static Vector2Int WorldToCell(Vector2 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x / cellSize), Mathf.FloorToInt(world.y / cellSize));
    }

    private static Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }

    // Follows the parent trail backwards from the goal to the start, then flips it
    // around so it reads start -> goal.
    private static List<Vector2> BuildRoute(Node goalNode, Vector2 exactGoal)
    {
        List<Vector2> route = new List<Vector2>();

        for (Node node = goalNode; node != null; node = node.parent)
            route.Add(CellToWorld(node.cell));

        route.Reverse();
        route.RemoveAt(0);                   // we're already standing on that first cell
        route[route.Count - 1] = exactGoal;  // finish at the real point, not the middle of its cell

        return route;
    }
}
