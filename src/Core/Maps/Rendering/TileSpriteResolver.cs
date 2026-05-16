using System;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Maps cells to terrain draw ops; single-cell entry point and helpers for <see cref="TileMainPatchPlanner"/>.</summary>
public static class TileSpriteResolver
{
    public const int DefaultVariantsPerCategory = 4;

    /// <summary>
    /// Writes one <see cref="TileSpriteRole.Main1x1"/> op for <paramref name="gx"/>, <paramref name="gy"/>.
    /// </summary>
    public static void ResolveCell(
        int gx,
        int gy,
        in TileCell tile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        int variantCount,
        Span<TileDrawOp> destination,
        out int opCount)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        if (variantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(variantCount));

        opCount = 0;
        if (destination.Length == 0)
            return;

        destination[0] = MakeMainOp(gx, gy, tile, evaluator, terrain, worldSeed, variantCount, TileSpriteRole.Main1x1, 1);
        opCount = 1;
    }

    public static TileDrawOp MakeMainOp(
        int originGx,
        int originGy,
        in TileCell anchorTile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        int variantCount,
        TileSpriteRole role,
        int sizeCells)
    {
        var category = TerrainAppearance.Resolve(originGx + 0.5f, originGy + 0.5f, anchorTile, evaluator, terrain);
        var variant = TileVariantSelector.SelectVariant(originGx, originGy, worldSeed, anchorTile.Variant, variantCount);
        var layer = DrawLayerFor(category);
        var key = new TileSpriteKey(category, role, variant);
        return new TileDrawOp(key, originGx, originGy, sizeCells, layer);
    }

    public static TerrainDrawLayer DrawLayerFor(TerrainRenderCategory category) =>
        category is TerrainRenderCategory.DeepWater
            or TerrainRenderCategory.ShallowWater
            or TerrainRenderCategory.ForcedWater
            ? TerrainDrawLayer.Water
            : TerrainDrawLayer.GroundNatural;
}
