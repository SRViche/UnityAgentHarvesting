using System.Collections.Generic;
using UnityEngine;

namespace FarmSim.Core
{
    /// <summary>
    /// Represents the world that agents can see and interact with.
    /// Equivalent to world_model.py. Matrix is indexed as Matrix[y, x] (row-major):
    /// 0 = empty, 1 = crop, 2 = obstacle, 3 = harvested, 4 = collection point.
    /// </summary>
    public class WorldModel
    {
        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public float CropRate;
        public float ObstacleRate;
        public int CollectionPointCount;

        public int[,] Matrix;
        public List<Vector2Int> CollectionPoints = new List<Vector2Int>();
        private string[,] territory; // null entries = unassigned/unreachable

        public int TotalCrops { get; private set; }
        public int HarvestedCrops { get; private set; }
        public int DeliveredCrops { get; private set; }

        // Set by World after spawning agents — mirrors Python's `model.agents`.
        public List<AgentModel> Agents = new List<AgentModel>();

        private readonly System.Random rng;

        public WorldModel(int rows = 20, int cols = 20, float cropRate = 0.45f,
            float obstacleRate = 0.1f, int collectionPoints = 1, int? seed = null)
        {
            Rows = rows;
            Cols = cols;
            CropRate = cropRate;
            ObstacleRate = obstacleRate;
            CollectionPointCount = collectionPoints;
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            Matrix = new int[rows, cols];
        }

        public void Generate()
        {
            Matrix = new int[Rows, Cols];
            CollectionPoints.Clear();

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    double roll = rng.NextDouble();
                    if (roll < ObstacleRate)
                        Matrix[y, x] = CellType.Obstacle;
                    else if (roll < ObstacleRate + CropRate)
                        Matrix[y, x] = CellType.Crop;
                    else
                        Matrix[y, x] = CellType.Empty;
                }
            }

            PlaceCollectionPoints();

            TotalCrops = CountCells(CellType.Crop);
            HarvestedCrops = 0;
            DeliveredCrops = 0;
        }

        private int CountCells(int type)
        {
            int count = 0;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    if (Matrix[y, x] == type) count++;
            return count;
        }

        private void PlaceCollectionPoints()
        {
            int placed = 0;
            int attempts = 0;
            while (placed < CollectionPointCount && attempts < 200)
            {
                attempts++;
                int x = rng.Next(0, Cols);
                int y = rng.Next(0, Rows);
                if (Matrix[y, x] == CellType.Obstacle) continue;
                Matrix[y, x] = CellType.Collection;
                CollectionPoints.Add(new Vector2Int(x, y));
                placed++;
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Cols && y >= 0 && y < Rows;

        public int GetCell(int x, int y) => Matrix[y, x];

        public bool IsCrop(int x, int y) => InBounds(x, y) && Matrix[y, x] == CellType.Crop;

        public bool IsObstacle(int x, int y) => InBounds(x, y) && Matrix[y, x] == CellType.Obstacle;

        /// <summary>Marks a crop cell as harvested. Returns the yield collected (0 if none).</summary>
        public int Harvest(int x, int y)
        {
            if (!IsCrop(x, y)) return 0;
            Matrix[y, x] = CellType.Harvested;
            HarvestedCrops++;
            return 1;
        }

        public void Deliver(int amount)
        {
            DeliveredCrops += amount;
        }

        /// <summary>Splits the walkable field between agents using multi-source BFS.
        /// sources: dict {agentId: position}.</summary>
        public void AssignTerritories(Dictionary<string, Vector2Int> sources)
        {
            territory = Pathfinding.MultiSourceBfs(this, sources);
        }

        public string OwnerOf(int x, int y)
        {
            if (territory == null) return null;
            return territory[y, x];
        }

        /// <summary>All cells still bearing crops that belong to ownerId's territory.</summary>
        public List<Vector2Int> AssignedCropCells(string ownerId)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    if (Matrix[y, x] == CellType.Crop && OwnerOf(x, y) == ownerId)
                        cells.Add(new Vector2Int(x, y));
            return cells;
        }

        public bool IsFullyHarvested() => HarvestedCrops >= TotalCrops;
    }
}