using System.Collections.Generic;
using UnityEngine;

namespace FarmSim.Core
{
    public class AgentModel
    {
        private static readonly Dictionary<string, Vector2Int> Directions = new Dictionary<string, Vector2Int>
        {
            { "U", new Vector2Int(0, -1) },
            { "D", new Vector2Int(0, 1) },
            { "L", new Vector2Int(-1, 0) },
            { "R", new Vector2Int(1, 0) },
            { "S", new Vector2Int(0, 0) },
        };

        public string Id;
        public int X, Y;
        protected WorldModel model;
        public int Energy = 100;
        public List<Vector2Int> Path = new List<Vector2Int>(); 
        public string Direction = "S"; 

        public AgentModel(string id, int x, int y, WorldModel model)
        {
            Id = id;
            X = x;
            Y = y;
            this.model = model;
        }

        public bool CanMoveTo(int x, int y)
        {
            if (!model.InBounds(x, y)) return false;
            return !model.IsObstacle(x, y);
        }

        /// <summary>Adopts a path returned by pathfinding (includes the current cell first).</summary>
        public void SetPath(List<Vector2Int> path)
        {
            if (path == null) { Path = new List<Vector2Int>(); return; }
            var copy = new List<Vector2Int>(path);
            if (copy.Count > 0 && copy[0] == new Vector2Int(X, Y))
                copy.RemoveAt(0);
            Path = copy;
        }

        public List<Vector2Int> PathTo(Vector2Int target, int harvestedPenalty = 3)
        {
            var path = Pathfinding.ShortestPath(model, new Vector2Int(X, Y), target, harvestedPenalty);
            if (path != null) SetPath(path);
            return path;
        }

        public bool MoveStep()
        {
            if (Path.Count == 0)
            {
                Direction = "S";
                return false;
            }

            var next = Path[0];
            Direction = DirectionTo(next.x, next.y);

            if (CanMoveTo(next.x, next.y))
            {
                X = next.x;
                Y = next.y;
                Path.RemoveAt(0);
                return true;
            }

            // Blocked (e.g. obstacle changed at runtime): drop the stale path.
            Path = new List<Vector2Int>();
            return false;
        }

        private string DirectionTo(int nx, int ny)
        {
            int dx = nx - X, dy = ny - Y;
            foreach (var kvp in Directions)
                if (kvp.Value.x == dx && kvp.Value.y == dy)
                    return kvp.Key;
            return "S";
        }

        public int DistanceTo(int x, int y) => Mathf.Abs(X - x) + Mathf.Abs(Y - y);

        public bool IsAdjacent(int x, int y, bool includeDiagonals = true)
        {
            int dx = Mathf.Abs(X - x), dy = Mathf.Abs(Y - y);
            if (includeDiagonals) return Mathf.Max(dx, dy) == 1;
            return dx + dy == 1;
        }

        public bool HasArrived() => Path.Count == 0;

        /// <summary>Per-tick behavior. Overridden by HarvesterModel / TractorModel.</summary>
        public virtual void Tick() { }
    }
}