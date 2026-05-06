namespace SpecialPG.Core.Maps;

/// <summary>
/// Ensures a contiguous walkable (ForceLand) region around a configurable anchor (default global (0,0)) clamped
/// into each bounded floor; procedural generation passes map center so this aligns with spawn/stairs.
/// </summary>
public static class OriginWalkabilityPatch
{
    /// <summary>Default Chebyshev radius: 5×5 cells around the clamped anchor.</summary>
    public const int DefaultChebyshevRadius = 2;

    private static readonly TileCell OriginLandCell = TileCell.SyntheticLand(0) with
    {
        Override = TerrainOverride.ForceLand,
    };

    /// <summary>
    /// Writes <see cref="TerrainOverride.ForceLand"/> tiles in a Chebyshev ball around
    /// <paramref name="anchorGx"/>, <paramref name="anchorGy"/> (clamped to each floor) on every present bounded
    /// floor (typically Z=0 and Z=1). Defaults anchor to global (0,0) for backward compatibility.
    /// </summary>
    public static void ApplyToBoundedWorld(WorldMap map, int chebyshevRadius = DefaultChebyshevRadius,
        int anchorGx = 0, int anchorGy = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsBounded)
            throw new ArgumentException("Origin patch applies to bounded worlds only.", nameof(map));

        if (chebyshevRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(chebyshevRadius));

        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var floor) || floor is null || !floor.IsBounded)
                continue;

            var maxGx = floor.MinX + floor.Width - 1;
            var maxGy = floor.MinY + floor.Height - 1;
            var ax = Math.Clamp(anchorGx, floor.MinX, maxGx);
            var ay = Math.Clamp(anchorGy, floor.MinY, maxGy);

            var gx0 = Math.Max(floor.MinX, ax - chebyshevRadius);
            var gx1 = Math.Min(maxGx, ax + chebyshevRadius);
            var gy0 = Math.Max(floor.MinY, ay - chebyshevRadius);
            var gy1 = Math.Min(maxGy, ay + chebyshevRadius);

            var prevSuppress = floor.SuppressChunkModificationTracking;
            floor.SuppressChunkModificationTracking = true;
            try
            {
                for (var gy = gy0; gy <= gy1; gy++)
                {
                    for (var gx = gx0; gx <= gx1; gx++)
                        floor.Set(gx, gy, OriginLandCell);
                }
            }
            finally
            {
                floor.SuppressChunkModificationTracking = prevSuppress;
            }
        }
    }
}
