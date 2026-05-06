using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// RGB (linear 0–1) for map previews and Shell terrain painting from <see cref="ITerrainEvaluator"/> at world float coordinates.
/// </summary>
public readonly struct TerrainRgb(float r, float g, float b)
{
    public float R { get; } = r;

    public float G { get; } = g;

    public float B { get; } = b;
}

/// <summary>
/// Milestone 7: continuous noise sampling for shoreline / elevation tint (overrides still win).
/// </summary>
public static class TerrainVisualColor
{
    private static readonly TerrainRgb WaterDeep = new(0.05f, 0.18f, 0.82f);
    private static readonly TerrainRgb WaterShallow = new(0.18f, 0.48f, 0.99f);
    private static readonly TerrainRgb Land = new(0.32f, 0.72f, 0.38f);
    private static readonly TerrainRgb CoastSand = new(0.60f, 0.62f, 0.52f);
    private static readonly TerrainRgb Blocked = new(0.20f, 0.45f, 0.24f);
    private static readonly TerrainRgb Empty = new(0.50f, 0.52f, 0.55f);
    private static readonly TerrainRgb HillTint = new(0.55f, 0.50f, 0.40f);

    /// <summary>Representative colors for shell HUD legend (actual pixels blend by elevation / overrides).</summary>
    public static readonly (string Label, TerrainRgb Color)[] LegendSwatches =
    {
        ("Deep water", WaterDeep),
        ("Shallow water", WaterShallow),
        ("Land", Land),
        ("Coast (low land)", CoastSand),
        ("Hills (high elev.)", HillTint),
        ("Blocked tile", Blocked),
        ("Forced land (override)", Land),
        ("Forced water (override)", WaterDeep),
        ("Empty / no chunk data", Empty),
    };

    /// <summary>
    /// Visual color at <paramref name="worldX"/>, <paramref name="worldY"/> using tile overrides and continuous noise when applicable.
    /// </summary>
    public static TerrainRgb AtWorld(
        float worldX,
        float worldY,
        in TileCell tile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        if (tile.Override == TerrainOverride.ForceLand)
            return Land;
        if (tile.Override == TerrainOverride.ForceWater)
            return WaterDeep;
        if ((tile.Flags & TileFlags.Blocked) != 0 && !TileTraversal.IsWaterSurface(tile, terrain))
            return Blocked;
        if (tile.IsEmpty)
            return FromEvaluator(worldX, worldY, evaluator, terrain);

        return FromEvaluator(worldX, worldY, evaluator, terrain);
    }

    private static TerrainRgb FromEvaluator(float worldX, float worldY, ITerrainEvaluator e, in TerrainNoiseConfig cfg)
    {
        var s = e.EvaluateAt(worldX, worldY);
        if (e.IsWater(s))
        {
            var span = Math.Max(1e-4f, cfg.WaterElevationThreshold - (-1f));
            var t = Clamp01((cfg.WaterElevationThreshold - s.Elevation) / span);
            return LerpRgb(WaterShallow, WaterDeep, t);
        }

        var landRgb = Land;
        if (s.Elevation < cfg.CoastElevationThreshold)
        {
            var t = InverseLerp(cfg.WaterElevationThreshold, cfg.CoastElevationThreshold, s.Elevation);
            landRgb = LerpRgb(CoastSand, Land, Clamp01(t));
        }

        if (s.Elevation > cfg.HillElevationThreshold)
        {
            var t = InverseLerp(cfg.HillElevationThreshold, 1f, Math.Min(s.Elevation, 1f));
            landRgb = LerpRgb(landRgb, HillTint, Clamp01(t) * 0.35f);
        }

        return landRgb;
    }

    /// <summary>
    /// Short legend-aligned label for HUD/debug (tile center <paramref name="worldX"/>, <paramref name="worldY"/>).
    /// </summary>
    public static string DescribeCategoryAtTileCenter(
        float worldX,
        float worldY,
        in TileCell tile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        if (tile.Override == TerrainOverride.ForceLand)
            return "Forced land (override)";
        if (tile.Override == TerrainOverride.ForceWater)
            return "Forced water (override)";
        if ((tile.Flags & TileFlags.Blocked) != 0 && !TileTraversal.IsWaterSurface(tile, terrain))
            return "Blocked tile";

        return DescribeFromEvaluator(worldX, worldY, evaluator, terrain);
    }

    private static string DescribeFromEvaluator(float worldX, float worldY, ITerrainEvaluator e, in TerrainNoiseConfig cfg)
    {
        var s = e.EvaluateAt(worldX, worldY);
        if (e.IsWater(s))
        {
            var span = Math.Max(1e-4f, cfg.WaterElevationThreshold - (-1f));
            var t = Clamp01((cfg.WaterElevationThreshold - s.Elevation) / span);
            return t >= 0.5f ? "Shallow water" : "Deep water";
        }

        if (s.Elevation < cfg.CoastElevationThreshold)
            return "Coast (low land)";
        if (s.Elevation > cfg.HillElevationThreshold)
            return "Hills (high elev.)";
        return "Land";
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static float InverseLerp(float a, float b, float v)
    {
        if (Math.Abs(b - a) < 1e-6f)
            return 0f;
        return (v - a) / (b - a);
    }

    private static TerrainRgb LerpRgb(TerrainRgb x, TerrainRgb y, float t)
    {
        t = Clamp01(t);
        return new TerrainRgb(
            x.R + (y.R - x.R) * t,
            x.G + (y.G - x.G) * t,
            x.B + (y.B - x.B) * t);
    }
}
