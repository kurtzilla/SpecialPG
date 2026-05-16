using System;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>One procedural decor marker in global grid space (Phase 5).</summary>
public readonly record struct DecorCell(int Gx, int Gy, int VariantIndex);

/// <summary>
/// Deterministic sparse decor scatter per chunk. Land-like categories only; skips water and blocked tiles.
/// </summary>
public static class DecorScatterPlanner
{
    public const int PlannerVersion = 1;

    /// <summary>Approximate land cells per 100 that receive decor (v1 ~3%).</summary>
    public const int ScatterThresholdPercent = 3;

    public const int VariantCount = 8;

    public static void PlanChunk(
        FloorSlice floor,
        int cx,
        int cy,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        List<DecorCell> destination)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out var lw, out var lh);

        for (var ly = 0; ly < lh; ly++)
        {
            for (var lx = 0; lx < lw; lx++)
            {
                var gx = gx0 + lx;
                var gy = gy0 + ly;
                if (!floor.Contains(gx, gy))
                    continue;

                var tile = floor.Get(gx, gy);
                var category = TerrainAppearance.Resolve(gx + 0.5f, gy + 0.5f, tile, evaluator, terrain);
                if (!IsDecorCategory(category))
                    continue;

                var roll = (uint)HashCode.Combine(gx, gy, worldSeed, PlannerVersion) % 100;
                if (roll >= ScatterThresholdPercent)
                    continue;

                var variant = (int)((uint)HashCode.Combine(gx, gy, worldSeed, 0xDEC0) % (uint)VariantCount);
                destination.Add(new DecorCell(gx, gy, variant));
            }
        }
    }

    private static bool IsDecorCategory(TerrainRenderCategory category) =>
        category is TerrainRenderCategory.Land
            or TerrainRenderCategory.Hill
            or TerrainRenderCategory.Coast;
}
