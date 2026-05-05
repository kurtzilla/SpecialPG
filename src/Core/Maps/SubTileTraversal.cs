namespace SpecialPG.Core.Maps;

/// <summary>
/// Walkability at sub-tile resolution using the same water/override rules as <see cref="TileTraversal"/>,
/// with elevation sampled from <see cref="ITerrainEvaluator"/> at fractional world coordinates.
/// </summary>
public static class SubTileTraversal
{
    /// <summary>
    /// World X for noise: global tile column + fractional eastward offset in <c>[0,1)</c>.
    /// </summary>
    public static float SubCellWorldX(int tileX, int subX) =>
        tileX + (subX + 0.5f) / SubTileGrid.Resolution;

    /// <summary>
    /// World Y for noise: global tile row + fractional northward offset in <c>[0,1)</c>.
    /// </summary>
    public static float SubCellWorldY(int tileY, int subY) =>
        tileY + (subY + 0.5f) / SubTileGrid.Resolution;

    /// <summary>
    /// Whether the actor may stand at the given sub-cell. Requires <paramref name="evaluator"/> for noise sampling.
    /// </summary>
    public static bool IsWalkable(
        WorldMap map,
        int z,
        int tileX,
        int tileY,
        int subX,
        int subY,
        ITerrainEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(evaluator);
        if (!SubTileGrid.IsValidSub(subX) || !SubTileGrid.IsValidSub(subY))
            return false;

        if (!map.TryGetFloor(z, out var floor) || floor is null)
            return false;

        if (!floor.Contains(tileX, tileY))
            return false;

        var tile = floor.Get(tileX, tileY);
        if ((tile.Flags & TileFlags.Blocked) != 0)
            return false;
        if (tile.Override == TerrainOverride.ForceLand)
            return true;
        if (tile.Override == TerrainOverride.ForceWater)
            return false;

        var wx = SubCellWorldX(tileX, subX);
        var wy = SubCellWorldY(tileY, subY);
        var sample = evaluator.EvaluateAt(wx, wy);
        return !evaluator.IsWater(sample);
    }
}
