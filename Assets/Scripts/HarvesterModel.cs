using System.Collections.Generic;
using UnityEngine;

namespace FarmSim.Core
{
    
    public class HarvesterModel : AgentModel
    {
        private const int HarvestedPenalty = 3;

        public int Capacity;
        public int Load;
        public Vector2Int? Target;       // crop cell currently being pursued
        public Vector2Int? UnloadTarget; // tractor or collection point being approached
        public bool Done;

        public HarvesterModel(string id, int x, int y, WorldModel model, int capacity = 30)
            : base(id, x, y, model)
        {
            Capacity = capacity;
        }

        public bool IsFull() => Load >= Capacity;

        public List<Vector2Int> TerritoryCells() => model.AssignedCropCells(Id);

        public override void Tick()
        {
            if (Done) return;

            if (IsFull())
            {
                HandleUnloading();
                return;
            }

            if (HasArrived() && Target.HasValue)
                HarvestCurrentCell();

            if (HasArrived())
                PlanNextHarvest();

            MoveStep();
        }

        private void PlanNextHarvest()
        {
            var remaining = TerritoryCells();
            if (remaining.Count == 0)
            {
                Target = null;
                if (Load == 0) Done = true;
                else HandleUnloading();
                return;
            }

            var result = Pathfinding.NearestTarget(model, new Vector2Int(X, Y), new HashSet<Vector2Int>(remaining), HarvestedPenalty);
            if (result == null)
            {
                Done = true;
                return;
            }

            Target = result.Target;
            SetPath(result.Path);
        }

        private void HarvestCurrentCell()
        {
            int gained = model.Harvest(X, Y);
            if (gained > 0) Load += gained;
            Target = null;
        }

        /// <summary>Finds the closest place to dump the load: a tractor within reach, or a collection point.</summary>
        private void HandleUnloading()
        {
            var tractor = FindNearbyTractor();
            if (tractor != null)
            {
                TransferTo(tractor);
                return;
            }

            if (!UnloadTarget.HasValue || HasArrived())
            {
                var destination = Pathfinding.NearestTarget(model, new Vector2Int(X, Y), new HashSet<Vector2Int>(model.CollectionPoints), HarvestedPenalty);
                if (destination == null) return;
                UnloadTarget = destination.Target;
                SetPath(destination.Path);
            }

            var ut = UnloadTarget.Value;
            if (IsAdjacent(ut.x, ut.y) || (X == ut.x && Y == ut.y))
            {
                model.Deliver(Load);
                Load = 0;
                UnloadTarget = null;
            }
            else
            {
                MoveStep();
            }
        }

        private TractorModel FindNearbyTractor()
        {
            foreach (var agent in model.Agents)
            {
                if (agent == this) continue;
                if (agent is TractorModel tractor && IsAdjacent(tractor.X, tractor.Y) && tractor.HasCapacityFor(Load))
                    return tractor;
            }
            return null;
        }

        private void TransferTo(TractorModel tractor)
        {
            tractor.ReceiveLoad(Load);
            Load = 0;
            UnloadTarget = null;
        }
    }
}