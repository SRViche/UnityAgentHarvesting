using System.Collections.Generic;
using UnityEngine;

namespace FarmSim.Core
{
    /// <summary>Result of a nearest-target search: which target was reached and the path to it.</summary>
    public class PathResult
    {
        public Vector2Int Target;
        public List<Vector2Int> Path;
    }

    /// <summary>
    /// Grid pathfinding helpers shared by WorldModel and the agents.
    /// Equivalent to pathfinding.py: multi-source BFS (used to split the field between
    /// harvesters) and a Dijkstra-based shortest path / nearest-target search that
    /// accounts for the penalty of stepping onto an already-harvested cell.
    /// </summary>
    public static class Pathfinding
    {
        private static readonly Vector2Int[] Neighbors4 =
        {
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0)
        };

        public static bool IsWalkable(WorldModel model, int x, int y)
        {
            return model.InBounds(x, y) && model.Matrix[y, x] != CellType.Obstacle;
        }

        public static int StepCost(WorldModel model, int x, int y, int harvestedPenalty)
        {
            return model.Matrix[y, x] == CellType.Harvested ? harvestedPenalty : 1;
        }

        /// <summary>
        /// Divides every walkable cell between the given sources.
        /// sources: dict {ownerId: position}. Returns an owner matrix (rows x cols) with
        /// ownerId or null for obstacles/unreachable cells. Ties resolve in favor of
        /// whichever source appears first when iterating `sources`.
        /// </summary>
        public static string[,] MultiSourceBfs(WorldModel model, Dictionary<string, Vector2Int> sources)
        {
            var owner = new string[model.Rows, model.Cols];
            var queue = new Queue<Vector2Int>();

            foreach (var kvp in sources)
            {
                var pos = kvp.Value;
                if (!IsWalkable(model, pos.x, pos.y)) continue;
                if (owner[pos.y, pos.x] == null)
                {
                    owner[pos.y, pos.x] = kvp.Key;
                    queue.Enqueue(pos);
                }
            }

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                string currentOwner = owner[cur.y, cur.x];
                foreach (var d in Neighbors4)
                {
                    int nx = cur.x + d.x, ny = cur.y + d.y;
                    if (!IsWalkable(model, nx, ny)) continue;
                    if (owner[ny, nx] == null)
                    {
                        owner[ny, nx] = currentOwner;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }

            return owner;
        }

        /// <summary>Weighted shortest path from start to goal, avoiding obstacles.
        /// Returns null if no path exists, otherwise the list of cells including start and goal.</summary>
        public static List<Vector2Int> ShortestPath(WorldModel model, Vector2Int start, Vector2Int goal, int harvestedPenalty = 3)
        {
            var result = NearestTarget(model, start, new HashSet<Vector2Int> { goal }, harvestedPenalty);
            return result?.Path;
        }

        /// <summary>Cheapest target (by weighted distance) reachable from start, via Dijkstra.
        /// Returns null if none of the targets can be reached.</summary>
        public static PathResult NearestTarget(WorldModel model, Vector2Int start, HashSet<Vector2Int> targets, int harvestedPenalty = 3)
        {
            if (targets == null || targets.Count == 0) return null;
            if (!IsWalkable(model, start.x, start.y)) return null;

            if (targets.Contains(start))
                return new PathResult { Target = start, Path = new List<Vector2Int> { start } };

            var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
            var prev = new Dictionary<Vector2Int, Vector2Int>();

            // SortedSet used as a portable priority queue. PQItem compares ONLY on
            // (Cost, Tie) — never on Pos, since Vector2Int has no IComparable — and Tie
            // is a strictly increasing counter, so every entry compares unequal to every
            // other one (avoiding the Pos comparison entirely, even for self-lookups).
            int tieCounter = 0;
            var pending = new SortedSet<PQItem>();
            pending.Add(new PQItem { Cost = 0, Tie = tieCounter++, Pos = start });

            while (pending.Count > 0)
            {
                var entry = pending.Min;
                pending.Remove(entry);
                int cost = entry.Cost;
                var pos = entry.Pos;

                if (dist.TryGetValue(pos, out int best) && cost > best) continue;

                if (targets.Contains(pos))
                {
                    var path = RebuildPath(prev, start, pos);
                    return new PathResult { Target = pos, Path = path };
                }

                foreach (var d in Neighbors4)
                {
                    int nx = pos.x + d.x, ny = pos.y + d.y;
                    if (!IsWalkable(model, nx, ny)) continue;
                    var npos = new Vector2Int(nx, ny);
                    int newCost = cost + StepCost(model, nx, ny, harvestedPenalty);
                    if (!dist.TryGetValue(npos, out int curBest) || newCost < curBest)
                    {
                        dist[npos] = newCost;
                        prev[npos] = pos;
                        pending.Add(new PQItem { Cost = newCost, Tie = tieCounter++, Pos = npos });
                    }
                }
            }

            return null;
        }

        /// <summary>Priority-queue entry. IComparable by design covers only Cost/Tie so the
        /// SortedSet never needs to compare two Vector2Int values.</summary>
        private struct PQItem : System.IComparable<PQItem>
        {
            public int Cost;
            public int Tie;
            public Vector2Int Pos;

            public int CompareTo(PQItem other)
            {
                int c = Cost.CompareTo(other.Cost);
                return c != 0 ? c : Tie.CompareTo(other.Tie);
            }
        }

        private static List<Vector2Int> RebuildPath(Dictionary<Vector2Int, Vector2Int> prev, Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int> { goal };
            while (path[path.Count - 1] != start)
                path.Add(prev[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }
}