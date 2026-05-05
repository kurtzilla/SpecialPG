using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>Movement rules on <see cref="TileCell"/>; used by <see cref="WorldState"/>.</summary>
public static class TileTraversal
{
    /// <summary>
    /// Walkability uses <paramref name="terrain"/> thresholds for water vs land when
    /// <see cref="TerrainOverride"/> is <see cref="TerrainOverride.None"/>.
    /// </summary>
    public static bool IsWalkable(TileCell tile, in TerrainNoiseConfig terrain)
    {
        if ((tile.Flags & TileFlags.Blocked) != 0)
            return false;
        if (tile.Override == TerrainOverride.ForceLand)
            return true;
        if (tile.Override == TerrainOverride.ForceWater)
            return false;
        if (tile.IsEmpty)
            return true;
        return tile.DecodeElevation() >= terrain.WaterElevationThreshold;
    }

    /// <summary>Surface is water for visuals/rules (ignores <see cref="TileFlags.Blocked"/>).</summary>
    public static bool IsWaterSurface(TileCell tile, in TerrainNoiseConfig terrain)
    {
        if (tile.Override == TerrainOverride.ForceLand)
            return false;
        if (tile.Override == TerrainOverride.ForceWater)
            return true;
        if (tile.IsEmpty)
            return false;
        return tile.DecodeElevation() < terrain.WaterElevationThreshold;
    }
}
