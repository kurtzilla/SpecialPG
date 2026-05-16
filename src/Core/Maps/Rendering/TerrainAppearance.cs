using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>
/// Shared terrain appearance category at world coordinates (used by visuals and sprite resolution).
/// </summary>
public static class TerrainAppearance
{
    /// <summary>
    /// Classify a cell sample at <paramref name="worldX"/>, <paramref name="worldY"/>.
    /// Empty cells use continuous noise only (same as <see cref="TerrainVisualColor.AtWorld"/>).
    /// </summary>
    public static TerrainRenderCategory Resolve(
        float worldX,
        float worldY,
        in TileCell tile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        if (tile.Override == TerrainOverride.ForceLand)
        {
            var s = evaluator.EvaluateAt(worldX, worldY);
            return evaluator.IsWater(s) ? TerrainRenderCategory.ForcedLandCoastBlend : TerrainRenderCategory.ForcedLandOverride;
        }

        if (tile.Override == TerrainOverride.ForceWater)
            return TerrainRenderCategory.ForcedWater;

        if ((tile.Flags & TileFlags.Blocked) != 0 && !TileTraversal.IsWaterSurface(tile, terrain))
            return TerrainRenderCategory.Blocked;

        return FromEvaluator(worldX, worldY, evaluator, terrain);
    }

    private static TerrainRenderCategory FromEvaluator(
        float worldX,
        float worldY,
        ITerrainEvaluator e,
        in TerrainNoiseConfig cfg)
    {
        var s = e.EvaluateAt(worldX, worldY);
        if (e.IsWater(s))
        {
            var span = Math.Max(1e-4f, cfg.WaterElevationThreshold - (-1f));
            var t = Clamp01((cfg.WaterElevationThreshold - s.Elevation) / span);
            return t >= 0.5f ? TerrainRenderCategory.ShallowWater : TerrainRenderCategory.DeepWater;
        }

        if (s.Elevation < cfg.CoastElevationThreshold)
            return TerrainRenderCategory.Coast;
        if (s.Elevation > cfg.HillElevationThreshold)
            return TerrainRenderCategory.Hill;
        return TerrainRenderCategory.Land;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
