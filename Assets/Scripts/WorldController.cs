using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using FarmSim.Core;
using FarmSim.Simulation;

namespace FarmSim.UnityBridge
{
    /// <summary>
    /// Bridges the plain C# simulation (World/WorldModel) to a Unity scene: paints the
    /// field onto a Tilemap and keeps agent GameObjects (AgentView) in sync each tick.
    /// This is the Unity equivalent of renderer.py.
    /// </summary>
    public class WorldController : MonoBehaviour
    {
        [Header("Simulation")]
        public int harvesterCount = 3;
        public int tractorCount = 2;
        public int maxSteps = 500;
        [Tooltip("Seconds between simulation ticks.")]
        public float stepInterval = 0.15f;
        [Tooltip("If true, the field size/position comes from whatever area is already " +
                 "painted on the Tilemap. If false, uses Manual Rows/Cols starting at cell (0,0).")]
        public bool useTilemapFootprint = true;
        [Tooltip("Only used when Use Tilemap Footprint is off.")]
        public int manualRows = 20;
        public int manualCols = 20;

        [Header("Tilemap")]
        public Tilemap tilemap;
        [Tooltip("Assign your own simple sprites/tiles for each cell type.")]
        public TileBase emptyTile;
        public TileBase cropTile;
        public TileBase obstacleTile;
        public TileBase harvestedTile;
        public TileBase collectionTile;

        [Header("Agent prefabs")]
        public GameObject harvesterPrefab;
        public GameObject tractorPrefab;

        public World World { get; private set; }

        private readonly Dictionary<string, AgentView> agentViews = new Dictionary<string, AgentView>();
        private float timer;

        // Unity cell coordinates of the field's origin (top-left in sim space), used by SimToCell.
        private int originCellX;
        private int originCellY;

        private void Start()
        {
            int rows, cols;
            DetermineFieldBounds(out rows, out cols);

            var model = new WorldModel(rows, cols);
            World = new World(harvesterCount, tractorCount, maxSteps, model);
            World.Build();

            PaintField();
            SpawnAgentViews();
        }

        /// <summary>Finds the rectangle of cells painted with 'Empty Tile' (your brown dirt
        /// tile) inside the Tilemap, ignoring any other painted tiles (like the surrounding
        /// green grass). This is the occupiable area the field is generated into — NOT the
        /// full painted bounds of the Tilemap, which would include the grass too.</summary>
        private void DetermineFieldBounds(out int rows, out int cols)
        {
            if (!useTilemapFootprint || tilemap == null || emptyTile == null)
            {
                originCellX = 0;
                originCellY = 0;
                rows = manualRows;
                cols = manualCols;

                if (useTilemapFootprint && emptyTile == null)
                    Debug.LogWarning("WorldController: asigna 'Empty Tile' (tu tile de tierra café) " +
                        "para detectar el área ocupable automáticamente. Usando el tamaño manual por ahora.");
                return;
            }

            tilemap.CompressBounds();
            BoundsInt scanBounds = tilemap.cellBounds;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            bool found = false;

            foreach (var cell in scanBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != emptyTile) continue;
                found = true;
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            if (!found)
            {
                Debug.LogWarning("WorldController: no encontré ninguna celda pintada con 'Empty Tile' " +
                    "en el Tilemap. Revisa que ese campo apunte al mismo tile café que pintaste. Usando tamaño manual.");
                originCellX = 0;
                originCellY = 0;
                rows = manualRows;
                cols = manualCols;
                return;
            }

            originCellX = minX;
            originCellY = minY;
            cols = maxX - minX + 1;
            rows = maxY - minY + 1;
        }

        private void Update()
        {
            if (World == null || World.IsDone()) return;

            timer += Time.deltaTime;
            if (timer < stepInterval) return;
            timer = 0f;

            World.Step();
            RepaintField(); // crop cells turn into "harvested" tiles as the sim progresses
            SyncAgentViews();

            if (World.IsDone()) World.Report();
        }

        /// <summary>Converts a simulation grid coordinate to a Tilemap cell coordinate.
        /// The Python renderer draws row 0 at the top (imshow origin='upper'); Tilemap Y
        /// grows upward, so Y is flipped within the field's own footprint. X/Y are also
        /// offset by originCellX/Y so the field lines up with the area already painted
        /// on the Tilemap instead of always starting at cell (0,0).</summary>
        public Vector3Int SimToCell(int x, int y)
        {
            int rows = World != null ? World.Model.Rows : manualRows;
            return new Vector3Int(originCellX + x, originCellY + (rows - 1 - y), 0);
        }

        private TileBase TileFor(int cellType)
        {
            switch (cellType)
            {
                case CellType.Crop: return cropTile;
                case CellType.Obstacle: return obstacleTile;
                case CellType.Harvested: return harvestedTile;
                case CellType.Collection: return collectionTile;
                default: return emptyTile;
            }
        }

        private void PaintField()
        {
            var model = World.Model;
            for (int y = 0; y < model.Rows; y++)
                for (int x = 0; x < model.Cols; x++)
                    tilemap.SetTile(SimToCell(x, y), TileFor(model.Matrix[y, x]));
        }

        private void RepaintField()
        {
            // Simple approach: repaint every cell each tick. The grid is small (e.g. 20x20)
            // so this is cheap. If you scale the grid up a lot, swap this for a dirty-cell
            // list populated inside WorldModel.Harvest / Deliver instead.
            PaintField();
        }

        private void SpawnAgentViews()
        {
            foreach (var agent in World.Agents)
            {
                GameObject prefab = agent is HarvesterModel ? harvesterPrefab : tractorPrefab;
                if (prefab == null) continue;

                var go = Instantiate(prefab, transform);
                go.name = agent.Id;

                var view = go.GetComponent<AgentView>();
                if (view == null) view = go.AddComponent<AgentView>();

                Vector3 startPos = tilemap.CellToWorld(SimToCell(agent.X, agent.Y)) + tilemap.tileAnchor;
                view.Init(agent, startPos);
                agentViews[agent.Id] = view;
            }
        }

        private void SyncAgentViews()
        {
            foreach (var agent in World.Agents)
            {
                if (!agentViews.TryGetValue(agent.Id, out var view)) continue;
                Vector3 worldPos = tilemap.CellToWorld(SimToCell(agent.X, agent.Y)) + tilemap.tileAnchor;
                view.MoveTo(worldPos, stepInterval);
            }
        }
    }
}