using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FarmSim.Core;
 
namespace FarmSim.Simulation
{
    /// <summary>
    /// Top-level object which coordinates agents and the simulation.
    /// Equivalent to world.py. Plain C# class — Unity's WorldController MonoBehaviour
    /// drives it via Step(), the same way renderer.py did with matplotlib.
    /// </summary>
    public class World
    {
        public bool Started;
        public List<AgentModel> Agents = new List<AgentModel>();
        public WorldModel Model;
        public int HarvesterCount;
        public int TractorCount;
        public int MaxSteps;
        public int StepCount;
 
        // Metrics not present in the original Python model, added for the UI panel.
        public int TotalDistance;   // cells moved, summed across every agent
        public float FuelPerCell = 1f; // fuel units spent per cell moved — tune to taste
        public float FuelConsumed;
 
        private Dictionary<string, Vector2Int> lastPositions = new Dictionary<string, Vector2Int>();
        private readonly System.Random rng = new System.Random();
 
        public World(int harvesterCount = 3, int tractorCount = 2, int maxSteps = 500, WorldModel model = null)
        {
            HarvesterCount = harvesterCount;
            TractorCount = tractorCount;
            MaxSteps = maxSteps;
            Model = model ?? new WorldModel();
        }
 
        public World Spawn(AgentModel agent)
        {
            if (Started) throw new InvalidOperationException("Cannot spawn new agents: the simulation has already started");
            Agents.Add(agent);
            return this;
        }
 
        public void Build()
        {
            Model.Generate();
            Model.Agents = Agents;
            SpawnDefaultAgents();
            AssignTerritories();
 
            lastPositions = Agents.ToDictionary(a => a.Id, a => new Vector2Int(a.X, a.Y));
        }
 
        private void SpawnDefaultAgents()
        {
            var freeCells = FreeCells();
 
            for (int i = 0; i < HarvesterCount; i++)
            {
                var pos = PopLast(freeCells);
                Spawn(new HarvesterModel($"H{i}", pos.x, pos.y, Model));
            }
 
            for (int i = 0; i < TractorCount; i++)
            {
                var pos = PopLast(freeCells);
                Spawn(new TractorModel($"T{i}", pos.x, pos.y, Model));
            }
 
            Model.Agents = Agents; // keep in sync after spawning
        }
 
        private Vector2Int PopLast(List<Vector2Int> cells)
        {
            var last = cells[cells.Count - 1];
            cells.RemoveAt(cells.Count - 1);
            return last;
        }
 
        private List<Vector2Int> FreeCells()
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < Model.Rows; y++)
                for (int x = 0; x < Model.Cols; x++)
                    if (Model.Matrix[y, x] != CellType.Obstacle)
                        cells.Add(new Vector2Int(x, y));
 
            // Fisher-Yates shuffle (equivalent to Python's random.shuffle)
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            return cells;
        }
 
        private void AssignTerritories()
        {
            var sources = new Dictionary<string, Vector2Int>();
            foreach (var agent in Agents)
                if (agent is HarvesterModel h)
                    sources[h.Id] = new Vector2Int(h.X, h.Y);
            Model.AssignTerritories(sources);
        }
 
        /// <summary>Advances the simulation by a single tick. Unity's WorldController calls
        /// this on its own timer instead of using a blocking loop like Start().</summary>
        public bool Step()
        {
            Started = true;
            if (StepCount >= MaxSteps || IsDone()) return false;
 
            foreach (var agent in Agents) agent.Tick();
            TrackMovement();
            StepCount++;
            return true;
        }
 
        private void TrackMovement()
        {
            foreach (var agent in Agents)
            {
                var prev = lastPositions.TryGetValue(agent.Id, out var p) ? p : new Vector2Int(agent.X, agent.Y);
                int moved = Mathf.Abs(agent.X - prev.x) + Mathf.Abs(agent.Y - prev.y); // 0 or 1 per tick
                if (moved > 0)
                {
                    TotalDistance += moved;
                    FuelConsumed += moved * FuelPerCell;
                }
                lastPositions[agent.Id] = new Vector2Int(agent.X, agent.Y);
            }
        }
 
        public bool IsDone()
        {
            return StepCount >= MaxSteps || AllHarvestersDone();
        }
 
        private bool AllHarvestersDone()
        {
            return Agents.OfType<HarvesterModel>().All(h => h.Done);
        }
 
        public void Report()
        {
            Debug.Log($"[Sim] Finished after {StepCount} steps | " +
                      $"crops total {Model.TotalCrops} | harvested {Model.HarvestedCrops} | delivered {Model.DeliveredCrops}");
        }
    }
}
