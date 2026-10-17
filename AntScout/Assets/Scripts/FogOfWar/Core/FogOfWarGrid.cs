using System;
using AntScout.FogOfWar.Config;
using UnityEngine;

namespace AntScout.FogOfWar.Core
{
    /// <summary>
    /// High-performance, zero-allocation 2D grid managing explored and visible cells.
    /// Handles circle rasterization and texture pixel buffer compilation.
    /// </summary>
    public sealed class FogOfWarGrid
    {
        private readonly FogOfWarDefinition _config;
        private readonly int _resolution;
        private readonly float _invCellSizeX;
        private readonly float _invCellSizeY;

        private readonly byte[] _exploredGrid;
        private readonly byte[] _visibleGrid;
        private readonly Color32[] _pixelBuffer;

        public int Resolution => _resolution;

        public FogOfWarGrid(FogOfWarDefinition config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _resolution = config.Resolution;

            float worldWidth = config.WorldWidth;
            float worldHeight = config.WorldHeight;

            if (worldWidth <= 0f || worldHeight <= 0f)
            {
                throw new InvalidOperationException($"[{nameof(FogOfWarGrid)}] Invalid world dimensions: Width={worldWidth}, Height={worldHeight}.");
            }

            _invCellSizeX = _resolution / worldWidth;
            _invCellSizeY = _resolution / worldHeight;

            int totalCells = _resolution * _resolution;
            _exploredGrid = new byte[totalCells];
            _visibleGrid = new byte[totalCells];
            _pixelBuffer = new Color32[totalCells];

            // Initialize default alpha
            for (int i = 0; i < totalCells; i++)
            {
                _pixelBuffer[i] = new Color32(0, 0, 0, 255);
            }
        }

        /// <summary>
        /// Clears the temporary visibility buffer before the next simulation frame.
        /// Zero heap allocation (native memory clear).
        /// </summary>
        public void ResetFrame()
        {
            Array.Clear(_visibleGrid, 0, _visibleGrid.Length);
        }

        /// <summary>
        /// Rasterizes a vision circle into the grid, marking cells as Visible and Explored.
        /// </summary>
        public void RasterizeCircle(Vector3 center, float radius)
        {
            if (radius <= 0f) return;

            WorldToGrid(center, out float gfx, out float gfy);
            float rCellsX = radius * _invCellSizeX;
            float rCellsY = radius * _invCellSizeY;
            float rCellsAvg = (rCellsX + rCellsY) * 0.5f;
            float rCellsAvgSq = rCellsAvg * rCellsAvg;

            int minX = Mathf.Max(0, Mathf.FloorToInt(gfx - rCellsX));
            int maxX = Mathf.Min(_resolution - 1, Mathf.CeilToInt(gfx + rCellsX));
            int minY = Mathf.Max(0, Mathf.FloorToInt(gfy - rCellsY));
            int maxY = Mathf.Min(_resolution - 1, Mathf.CeilToInt(gfy + rCellsY));

            for (int y = minY; y <= maxY; y++)
            {
                float dy = y - gfy;
                float dySq = dy * dy;
                int rowOffset = y * _resolution;

                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - gfx;
                    float distSq = dx * dx + dySq;

                    if (distSq <= rCellsAvgSq)
                    {
                        int idx = rowOffset + x;
                        _visibleGrid[idx] = 255;
                        _exploredGrid[idx] = 255;
                    }
                }
            }
        }

        /// <summary>
        /// Bakes the current Explored (R) and Visible (G) buffers into the target GPU Texture2D.
        /// </summary>
        public void BakeToTexture(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            int totalCells = _resolution * _resolution;
            for (int i = 0; i < totalCells; i++)
            {
                _pixelBuffer[i].r = _exploredGrid[i];
                _pixelBuffer[i].g = _visibleGrid[i];
            }

            texture.SetPixels32(_pixelBuffer);
            texture.Apply(false, false);
        }

        /// <summary>
        /// Evaluates the discrete visibility state at a world position.
        /// </summary>
        public FogVisibilityState GetVisibilityState(Vector3 worldPos)
        {
            if (!TryGetCellIndex(worldPos, out int idx))
            {
                return FogVisibilityState.Unexplored;
            }

            if (_visibleGrid[idx] > 0)
            {
                return FogVisibilityState.Visible;
            }
            if (_exploredGrid[idx] > 0)
            {
                return FogVisibilityState.Explored;
            }

            return FogVisibilityState.Unexplored;
        }

        public bool IsVisible(Vector3 worldPos)
        {
            return TryGetCellIndex(worldPos, out int idx) && _visibleGrid[idx] > 0;
        }

        public bool IsExplored(Vector3 worldPos)
        {
            return TryGetCellIndex(worldPos, out int idx) && _exploredGrid[idx] > 0;
        }

        private bool TryGetCellIndex(Vector3 worldPos, out int index)
        {
            Vector2 min = _config.MapMin;
            Vector2 max = _config.MapMax;

            if (worldPos.x < min.x || worldPos.x > max.x || worldPos.z < min.y || worldPos.z > max.y)
            {
                index = -1;
                return false;
            }

            int gx = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - min.x) * _invCellSizeX), 0, _resolution - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - min.y) * _invCellSizeY), 0, _resolution - 1);
            index = gy * _resolution + gx;
            return true;
        }

        private void WorldToGrid(Vector3 worldPos, out float gx, out float gy)
        {
            Vector2 min = _config.MapMin;
            gx = (worldPos.x - min.x) * _invCellSizeX;
            gy = (worldPos.z - min.y) * _invCellSizeY;
        }
    }
}
