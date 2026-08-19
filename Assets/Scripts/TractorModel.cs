using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarmSim.Core
{
    /// <summary>
    /// Auxiliary agent to which a harvester's load is transferred when both agents meet at
    /// adjacent cells (including diagonals). Eventually, tractors must go to a collection
    /// point to dump all of its load. Equivalent to tractor.py.
    /// </summary>
    public class TractorModel : AgentModel
    {
        private const int HarvestedPenalty = 3;

        public int Capacity;
        public int Load;
        public HarvesterModel Assisting; // harvester currently being tracked for pickup

        public TractorModel(string id, int x, int y, WorldModel model, int capacity = 70)
            : base(id, x, y, model)
        {
            Capacity = capacity;
        }

        public bool HasCapacityFor(int amount) => Load + amount <= Capacity;

        public void ReceiveLoad(int amount) => Load = Mathf.Min(Capacity, Load + amount);

        public override void Tick()
        {
            if (Load >= Capacity)
            {
                Deliver();
                return;
            }

            var harvester = FindNeedyHarvester();
            if (harvester != null)
            {
                Assisting = harvester;
                Approach(harvester);
                return;
            }

            if (Load > 0) Deliver();
        }

        /// <summary>Picks the closest harvester that is full (or close to full) and needs a pickup.</summary>
        private HarvesterModel FindNeedyHarvester()
        {
            var loaded = model.Agents
                .OfType<HarvesterModel>()
                .Where(h => !ReferenceEquals(h, this) && h.Load > 0)
                .ToList();

            if (loaded.Count == 0) return null;

            loaded.Sort((a, b) =>
            {
                int fa = a.IsFull() ? 0 : 1;
                int fb = b.IsFull() ? 0 : 1;
                if (fa != fb) return fa.CompareTo(fb);
                return DistanceTo(a.X, a.Y).CompareTo(DistanceTo(b.X, b.Y));
            });

            return loaded[0];
        }

        private void Approach(HarvesterModel harvester)
        {
            if (IsAdjacent(harvester.X, harvester.Y))
            {
                Path = new List<Vector2Int>();
                return;
            }

            var result = Pathfinding.NearestTarget(model, new Vector2Int(X, Y), AdjacentCells(harvester.X, harvester.Y), HarvestedPenalty);
            if (result == null) return;
            SetPath(result.Path);
            MoveStep();
        }

        private HashSet<Vector2Int> AdjacentCells(int x, int y)
        {
            var cells = new HashSet<Vector2Int>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (model.InBounds(nx, ny) && !model.IsObstacle(nx, ny))
                        cells.Add(new Vector2Int(nx, ny));
                }
            return cells;
        }

        private void Deliver()
        {
            if (!HasArrived() || AtCollectionPoint() == null)
            {
                var destination = Pathfinding.NearestTarget(model, new Vector2Int(X, Y), new HashSet<Vector2Int>(model.CollectionPoints), HarvestedPenalty);
                if (destination == null) return;
                SetPath(destination.Path);
                MoveStep();
                return;
            }

            model.Deliver(Load);
            Load = 0;
        }

        private Vector2Int? AtCollectionPoint()
        {
            var pos = new Vector2Int(X, Y);
            return model.CollectionPoints.Contains(pos) ? (Vector2Int?)pos : null;
        }
    }
}