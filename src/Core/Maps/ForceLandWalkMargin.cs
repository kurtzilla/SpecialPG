using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Expands <see cref="TerrainOverride.ForceLand"/> by one 4-connected ring so sub-tile steps from origin/bridge
/// corridors do not immediately sample noise-classified water in the adjacent tile (see patch/bridge edge plan).
/// </summary>
public static class ForceLandWalkMargin
{
    private static readonly TileCell MarginLandCell = TileCell.SyntheticLand(0) with
    {
        Override = TerrainOverride.ForceLand,
    };

    /// <summary>Applies <see cref="ApplyToFloor"/> to every present bounded floor.</summary>
    public static void ApplyToBoundedWorld(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsBounded)
            throw new ArgumentException("ForceLand margin applies to bounded worlds only.", nameof(map));

        var terrain = map.TerrainConfig;
        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var floor) || floor is null || !floor.IsBounded)
                continue;

            ApplyToFloor(floor, in terrain);
        }
    }

    /// <summary>
    /// Any non–<see cref="TerrainOverride.ForceLand"/> / non–<see cref="TerrainOverride.ForceWater"/> cell that is not
    /// dry-blocked and 4-adjacent to a ForceLand cell becomes ForceLand.
    /// </summary>
    public static void ApplyToFloor(FloorSlice floor, in TerrainNoiseConfig terrain)
    {
        if (!floor.IsBounded)
            return;

        var w = floor.Width;
        var h = floor.Height;
        var isForceLand = new bool[w, h];
        for (var ly = 0; ly < h; ly++)
        {
            for (var lx = 0; lx < w; lx++)
            {
                var gx = floor.MinX + lx;
                var gy = floor.MinY + ly;
                isForceLand[lx, ly] = floor.Get(gx, gy).Override == TerrainOverride.ForceLand;
            }
        }

        var prevSuppress = floor.SuppressChunkModificationTracking;
        floor.SuppressChunkModificationTracking = true;
        try
        {
            for (var ly = 0; ly < h; ly++)
            {
                for (var lx = 0; lx < w; lx++)
                {
                    if (isForceLand[lx, ly])
                        continue;

                    if (!TouchesForceLand4(isForceLand, w, h, lx, ly))
                        continue;

                    var gx = floor.MinX + lx;
                    var gy = floor.MinY + ly;
                    var t = floor.Get(gx, gy);
                    if (t.Override == TerrainOverride.ForceWater)
                        continue;
                    if ((t.Flags & TileFlags.Blocked) != 0 && !TileTraversal.IsWaterSurface(t, in terrain))
                        continue;

                    floor.Set(gx, gy, MarginLandCell);
                }
            }
        }
        finally
        {
            floor.SuppressChunkModificationTracking = prevSuppress;
        }
    }

    private static bool TouchesForceLand4(bool[,] isForceLand, int w, int h, int lx, int ly)
    {
        if (lx > 0 && isForceLand[lx - 1, ly])
            return true;
        if (lx + 1 < w && isForceLand[lx + 1, ly])
            return true;
        if (ly > 0 && isForceLand[lx, ly - 1])
            return true;
        if (ly + 1 < h && isForceLand[lx, ly + 1])
            return true;
        return false;
    }
}
