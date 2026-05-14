using System.Collections.Generic;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Tile-level 4-connected landmass analysis for spawn placement: prefer the largest walkable component,
/// then Chebyshev distance from a preference point (typically map center).
/// </summary>
public static class LandmassSpawnSupport
{
    /// <summary>
    /// Finds a spawn cell on the <em>largest</em> 4-connected walkable component (using
    /// <see cref="TileTraversal.IsWalkable"/>), choosing among those cells the one reached first when scanning
    /// Chebyshev rings outward from <paramref name="centerGx"/>, <paramref name="centerGy"/>.
    /// </summary>
    /// <param name="acceptSpawnCell">Optional extra filter (e.g. sub-tile viability). When null, any walkable tile on the component qualifies.</param>
    /// <returns>False when the floor is unbounded, has no walkable tiles, or no matching cell on the largest component.</returns>
    public static bool TryFindSpawnChebyshevFromCenterOnLargestLandmass(
        FloorSlice floor,
        in TerrainNoiseConfig terrain,
        int centerGx,
        int centerGy,
        Func<int, int, bool>? acceptSpawnCell,
        out int spawnGx,
        out int spawnGy)
    {
        spawnGx = spawnGy = 0;
        if (!floor.IsBounded)
            return false;

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
            return false;

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
            return false;

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

        var maxR = System.Math.Min(512, System.Math.Max(w, h));
        for (var r = 0; r < maxR; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r)
                        continue;

                    var gx = centerGx + dx;
                    var gy = centerGy + dy;
                    if (!floor.Contains(gx, gy))
                        continue;

                    var li = gx - floor.MinX;
                    var lj = gy - floor.MinY;
                    if (comp[li, lj] != winningId || !walkable[li, lj])
                        continue;

                    if (acceptSpawnCell is not null && !acceptSpawnCell(gx, gy))
                        continue;

                    spawnGx = gx;
                    spawnGy = gy;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="gx"/>, <paramref name="gy"/> is tile-walkable and lies on the largest 4-connected walkable component
    /// (same labeling as <see cref="TryFindSpawnChebyshevFromCenterOnLargestLandmass"/>).
    /// </summary>
    public static bool IsWalkableOnLargestLandmass(FloorSlice floor, in TerrainNoiseConfig terrain, int gx, int gy)
    {
        if (!floor.Contains(gx, gy))
            return false;

        if (!TileTraversal.IsWalkable(floor.Get(gx, gy), terrain))
            return false;

        if (!floor.IsBounded)
            return false;

        var w = floor.Width;
        var h = floor.Height;
        var walkable = new bool[w, h];
        var anyWalk = false;
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
            {
                var ggx = floor.MinX + lx;
                var ggy = floor.MinY + ly;
                if (TileTraversal.IsWalkable(floor.Get(ggx, ggy), terrain))
                {
                    walkable[lx, ly] = true;
                    anyWalk = true;
                }
            }
        }

        if (!anyWalk)
            return false;

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
            return false;

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

        var li = gx - floor.MinX;
        var lj = gy - floor.MinY;
        return walkable[li, lj] && comp[li, lj] == winningId;
    }
}
