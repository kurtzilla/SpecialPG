using System.Linq;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Rules for water placement: every water cell must belong to at least one fully-water 2×2 block
/// (no isolated 1×1 water). Prefer <see cref="ApplyMinimumWaterBlobSizeTwoByTwo(WorldMap)"/> when
/// finalizing a whole map so all floors share one policy.
/// </summary>
public static class WaterTerrainRules
{
    /// <summary>
    /// Applies the minimum 2×2 water rule to every present floor in <paramref name="map"/>.
    /// </summary>
    public static void ApplyMinimumWaterBlobSizeTwoByTwo(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var floor) || floor is null)
                continue;
            ApplyMinimumWaterBlobSizeTwoByTwo(floor);
        }
    }

    /// <summary>
    /// Converts water tiles that are not part of any all-water 2×2 square to plain walkable land.
    /// Iterates until stable so peeling thin strips resolves correctly.
    /// </summary>
    public static void ApplyMinimumWaterBlobSizeTwoByTwo(FloorSlice floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        if (!floor.IsBounded)
        {
            ApplyMinimumWaterBlobSizeTwoByTwoUnbounded(floor);
            return;
        }

        var maxIter = floor.Width * floor.Height + 16;
        var iter = 0;
        var changed = true;
        while (changed && iter++ < maxIter)
        {
            changed = false;
            for (var gy = floor.MinY; gy < floor.MinY + floor.Height; gy++)
            {
                for (var gx = floor.MinX; gx < floor.MinX + floor.Width; gx++)
                {
                    if (floor.Get(gx, gy).TileKind != TerrainTileKinds.Water)
                        continue;
                    if (IsWaterPartOfAtLeastOneTwoByTwoBlock(floor, gx, gy))
                        continue;

                    floor.Set(gx, gy, new TileData
                    {
                        TileKind = TerrainTileKinds.Land,
                        Flags = 0,
                        Variant = 0,
                    });
                    changed = true;
                }
            }
        }
    }

    private static void ApplyMinimumWaterBlobSizeTwoByTwoUnbounded(FloorSlice floor)
    {
        var maxIter = Math.Max(256, floor.LoadedChunkCount * floor.ChunkWidth * floor.ChunkHeight + 16);
        var iter = 0;
        var changed = true;
        while (changed && iter++ < maxIter)
        {
            changed = false;
            foreach (var (cx, cy) in floor.GetLoadedChunkCoordinates().ToArray())
            {
                floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out var lw, out var lh);
                for (var ly = 0; ly < lh; ly++)
                {
                    for (var lx = 0; lx < lw; lx++)
                    {
                        var gx = gx0 + lx;
                        var gy = gy0 + ly;
                        if (floor.Get(gx, gy).TileKind != TerrainTileKinds.Water)
                            continue;
                        if (IsWaterPartOfAtLeastOneTwoByTwoBlock(floor, gx, gy))
                            continue;

                        floor.Set(gx, gy, new TileData
                        {
                            TileKind = TerrainTileKinds.Land,
                            Flags = 0,
                            Variant = 0,
                        });
                        changed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// True if <paramref name="gx"/>,<paramref name="gy"/> is water and lies in at least one
    /// 2×2 rectangle where all four cells are water.
    /// </summary>
    public static bool IsWaterPartOfAtLeastOneTwoByTwoBlock(FloorSlice floor, int gx, int gy)
    {
        ArgumentNullException.ThrowIfNull(floor);
        if (floor.Get(gx, gy).TileKind != TerrainTileKinds.Water)
            return false;

        if (!floor.IsBounded)
        {
            for (var ox = gx - 1; ox <= gx; ox++)
            {
                for (var oy = gy - 1; oy <= gy; oy++)
                {
                    if (IsWater(floor, ox, oy) && IsWater(floor, ox + 1, oy) &&
                        IsWater(floor, ox, oy + 1) && IsWater(floor, ox + 1, oy + 1))
                        return true;
                }
            }

            return false;
        }

        var maxX = floor.MinX + floor.Width - 1;
        var maxY = floor.MinY + floor.Height - 1;

        for (var ox = gx - 1; ox <= gx; ox++)
        {
            for (var oy = gy - 1; oy <= gy; oy++)
            {
                if (ox < floor.MinX || oy < floor.MinY)
                    continue;
                if (ox + 1 > maxX || oy + 1 > maxY)
                    continue;
                if (IsWater(floor, ox, oy) && IsWater(floor, ox + 1, oy) &&
                    IsWater(floor, ox, oy + 1) && IsWater(floor, ox + 1, oy + 1))
                    return true;
            }
        }

        return false;
    }

    private static bool IsWater(FloorSlice floor, int gx, int gy) =>
        floor.Get(gx, gy).TileKind == TerrainTileKinds.Water;
}
