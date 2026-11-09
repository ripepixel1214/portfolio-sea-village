#nullable enable
using System;
using UnityEngine;
using SeaVillage.Ocean.Maps;

namespace SeaVillage.Ocean
{
    // 항해 위치로 탐험 셀과 섬 발견을 갱신한다. 미니맵 표시 여부와 관계없이 씬 루트가 Tick한다.
    public sealed class OceanExploration : IDisposable
    {
        private readonly SeaArea area;
        private readonly Island[] islands;
        private readonly bool[] discovered;
        private readonly float cellWorldSize;
        private readonly float shipRevealRadius;
        private readonly float islandRevealRadius;
        private RectInt? changedCells;
        private bool disposed;

        public int Columns { get; }
        public int Rows { get; }
        // null은 저장 복원/리셋으로 인한 전체 교체, 값이 있으면 이번 Tick에서 바뀐 셀 범위다.
        public event Action<RectInt?>? Changed;

        public OceanExploration(SeaArea area, Island[] islands, OceanSceneConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.area = area;
            this.islands = islands ?? Array.Empty<Island>();
            discovered = new bool[this.islands.Length];
            cellWorldSize = config.FogCellWorldSize;
            shipRevealRadius = config.ShipRevealRadius;
            islandRevealRadius = config.IslandRevealRadius;
            Columns = Mathf.Max(1, Mathf.RoundToInt(area.Size.x / cellWorldSize));
            Rows = Mathf.Max(1, Mathf.RoundToInt(area.Size.y / cellWorldSize));
            MinimapFogState.Configure(Columns, Rows);

            foreach (Island island in this.islands)
            {
                if (island != null && island.IslandName == config.DefaultIslandName)
                {
                    RevealWorldCircle(island.WorldPosition, islandRevealRadius);
                    break;
                }
            }

            RecomputeIslandDiscovery();
            MinimapFogState.Changed += OnStateReplaced;
        }

        public void Tick(Vector2 shipPosition)
        {
            if (disposed) return;
            changedCells = null;
            bool changed = RevealWorldCircle(shipPosition, shipRevealRadius);
            for (int i = 0; i < islands.Length; i++)
            {
                if (discovered[i] || islands[i] == null || !IsWorldExplored(islands[i].WorldPosition))
                    continue;

                discovered[i] = true;
                changed = true;
                RevealWorldCircle(islands[i].WorldPosition, islandRevealRadius);
            }

            if (changed) Changed?.Invoke(changedCells);
        }

        public bool IsIslandDiscovered(int index)
            => index >= 0 && index < discovered.Length && discovered[index];

        public bool IsExplored(int x, int y) => MinimapFogState.IsExplored(x, y);

        public bool IsWorldExplored(Vector2 world)
        {
            ToCell(world, out int x, out int y);
            return IsExplored(x, y);
        }

        private bool RevealWorldCircle(Vector2 worldCenter, float radiusWorld)
        {
            ToCell(worldCenter, out int centerX, out int centerY);
            float radiusCells = radiusWorld / cellWorldSize;
            int radius = Mathf.CeilToInt(radiusCells);
            float radiusSquared = radiusCells * radiusCells;
            bool changed = false;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radiusSquared) continue;
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!MinimapFogState.Reveal(x, y)) continue;
                    IncludeChangedCell(x, y);
                    changed = true;
                }
            }
            return changed;
        }

        private void IncludeChangedCell(int x, int y)
        {
            if (!changedCells.HasValue)
            {
                changedCells = new RectInt(x, y, 1, 1);
                return;
            }

            RectInt bounds = changedCells.Value;
            int minX = Mathf.Min(bounds.xMin, x);
            int minY = Mathf.Min(bounds.yMin, y);
            changedCells = new RectInt(minX, minY,
                Mathf.Max(bounds.xMax, x + 1) - minX,
                Mathf.Max(bounds.yMax, y + 1) - minY);
        }

        private void ToCell(Vector2 world, out int x, out int y)
        {
            Vector2 normalized = area.Normalize(world);
            x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * Columns), 0, Columns - 1);
            y = Mathf.Clamp(Mathf.FloorToInt(normalized.y * Rows), 0, Rows - 1);
        }

        private void RecomputeIslandDiscovery()
        {
            for (int i = 0; i < islands.Length; i++)
                discovered[i] = islands[i] != null && IsWorldExplored(islands[i].WorldPosition);
        }

        private void OnStateReplaced()
        {
            RecomputeIslandDiscovery();
            Changed?.Invoke(null);
        }

        public void Dispose()
        {
            if (disposed) return;
            MinimapFogState.Changed -= OnStateReplaced;
            Changed = null;
            disposed = true;
        }
    }
}
