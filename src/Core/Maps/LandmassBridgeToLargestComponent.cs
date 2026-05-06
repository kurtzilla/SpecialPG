using System.Collections.Generic;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Connects global (0,0) (clamped into the floor) to the largest 4-connected walkable landmass by painting a
/// shortest 4-connected path of <see cref="TerrainOverride.ForceLand"/> tiles when the origin lies on a different
/// component after procedural patches.
/// </summary>
public static class LandmassBridgeToLargestComponent
{
    private static readonly TileCell BridgeLandCell = TileCell.SyntheticLand(0) with
    {
        Override = TerrainOverride.ForceLand,
    };

    /// <summary>Runs <see cref="ApplyToFloor"/> on every present bounded floor.</summary>
    public static void ApplyToBoundedWorld(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsBounded)
            throw new ArgumentException("Landmass bridge applies to bounded worlds only.", nameof(map));

        var terrain = map.TerrainConfig;
        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var floor) || floor is null || !floor.IsBounded)
                continue;
            ApplyToFloor(floor, terrain);
        }
    }

    /// <summary>
    /// When the clamped global origin is not already on the largest tile-walkable component, BFS-carves
    /// <see cref="TerrainOverride.ForceLand"/> along a shortest path through the grid to that component.
    /// </summary>
    public static void ApplyToFloor(FloorSlice floor, in TerrainNoiseConfig terrain)
    {
        if (!floor.IsBounded)
            return;

        var w = floor.Width;
        var h = floor.Height;
        var walkable = new bool[w, h];
        var anyWalk = false;
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
            {
                var gx = floor.MinX + lx;
                var gy = floor.MinY + ly;
                if (TileTraversal.IsWalkable(floor.Get(gx, gy), terrain))
                {
                    walkable[lx, ly] = true;
                    anyWalk = true;
                }
            }
        }

        if (!anyWalk)
            return;

        var comp = new int[w, h];
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
                comp[lx, ly] = -1;
        }

        var compSizes = new List<int>();
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
            {
                if (!walkable[lx, ly] || comp[lx, ly] != -1)
                    continue;

                var id = compSizes.Count;
                var q = new Queue<(int Lx, int Ly)>();
                q.Enqueue((lx, ly));
                comp[lx, ly] = id;
                var size = 0;
                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    size++;
                    TryEnqueue(cx - 1, cy);
                    TryEnqueue(cx + 1, cy);
                    TryEnqueue(cx, cy - 1);
                    TryEnqueue(cx, cy + 1);

                    void TryEnqueue(int nx, int ny)
                    {
                        if ((uint)nx >= (uint)w || (uint)ny >= (uint)h)
                            return;
                        if (!walkable[nx, ny] || comp[nx, ny] != -1)
                            return;
                        comp[nx, ny] = id;
                        q.Enqueue((nx, ny));
                    }
                }

                compSizes.Add(size);
            }
        }

        if (compSizes.Count == 0)
            return;

        var winningId = 0;
        var bestSize = compSizes[0];
        for (var i = 1; i < compSizes.Count; i++)
        {
            if (compSizes[i] > bestSize)
            {
                bestSize = compSizes[i];
                winningId = i;
            }
        }

        var maxGx = floor.MinX + w - 1;
        var maxGy = floor.MinY + h - 1;
        var ax = Math.Clamp(0, floor.MinX, maxGx);
        var ay = Math.Clamp(0, floor.MinY, maxGy);
        var ali = ax - floor.MinX;
        var alj = ay - floor.MinY;

        if (walkable[ali, alj] && comp[ali, alj] == winningId)
            return;

        var visited = new bool[w, h];
        var parentLx = new int[w, h];
        var parentLy = new int[w, h];
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
            {
                parentLx[lx, ly] = -1;
                parentLy[lx, ly] = -1;
            }
        }

        var bfs = new Queue<(int Lx, int Ly)>();
        bfs.Enqueue((ali, alj));
        visited[ali, alj] = true;
        var endLx = -1;
        var endLy = -1;

        while (bfs.Count > 0)
        {
            var (cx, cy) = bfs.Dequeue();
            if (walkable[cx, cy] && comp[cx, cy] == winningId)
            {
                endLx = cx;
                endLy = cy;
                break;
            }

            TryStep(cx - 1, cy);
            TryStep(cx + 1, cy);
            TryStep(cx, cy - 1);
            TryStep(cx, cy + 1);

            void TryStep(int nx, int ny)
            {
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h || visited[nx, ny])
                    return;
                visited[nx, ny] = true;
                parentLx[nx, ny] = cx;
                parentLy[nx, ny] = cy;
                bfs.Enqueue((nx, ny));
            }
        }

        if (endLx < 0)
            return;

        var prevSuppress = floor.SuppressChunkModificationTracking;
        floor.SuppressChunkModificationTracking = true;
        try
        {
            var px = endLx;
            var py = endLy;
            while (px >= 0)
            {
                var ggx = floor.MinX + px;
                var ggy = floor.MinY + py;
                floor.Set(ggx, ggy, BridgeLandCell);
                if (px == ali && py == alj)
                    break;
                var plx = parentLx[px, py];
                var ply = parentLy[px, py];
                if (plx < 0)
                    break;
                px = plx;
                py = ply;
            }
        }
        finally
        {
            floor.SuppressChunkModificationTracking = prevSuppress;
        }
    }
}
