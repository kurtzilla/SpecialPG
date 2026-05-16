using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Plans <see cref="TileSpriteRole.Side"/> ops on category boundaries inside a planning rectangle.</summary>
public static class TileTransitionPlanner
{
    private static readonly (int Dx, int Dy, TransitionFacing Facing)[] Cardinal =
    {
        (0, -1, TransitionFacing.North),
        (1, 0, TransitionFacing.East),
        (0, 1, TransitionFacing.South),
        (-1, 0, TransitionFacing.West),
    };

    public static void Plan(
        FloorSlice floor,
        int gx0,
        int gy0,
        int lw,
        int lh,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        int variantCount,
        List<TileDrawOp> destination)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(destination);
        if (variantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(variantCount));
        if (lw <= 0 || lh <= 0)
            return;

        var categories = new TerrainRenderCategory[lw * lh];
        FillCategoryGrid(floor, gx0, gy0, lw, lh, evaluator, terrain, categories);

        for (var gy = gy0; gy < gy0 + lh; gy++)
        {
            for (var gx = gx0; gx < gx0 + lw; gx++)
            {
                var li = LocalIndex(gx0, gy0, lw, gx, gy);
                var inner = categories[li];
                var innerGroup = TerrainTransitionGrouping.FromCategory(inner);
                if (innerGroup == TerrainTransitionGroup.Empty)
                    continue;

                if (!floor.Contains(gx, gy))
                    continue;

                var tile = floor.Get(gx, gy);

                for (var i = 0; i < Cardinal.Length; i++)
                {
                    var (dx, dy, facing) = Cardinal[i];
                    var ngx = gx + dx;
                    var ngy = gy + dy;
                    var neighbor = SampleCategory(categories, gx0, gy0, lw, lh, ngx, ngy);
                    if (!TerrainTransitionGrouping.NeedsTransition(innerGroup, TerrainTransitionGrouping.FromCategory(neighbor)))
                        continue;

                    var variant = TileVariantSelector.SelectVariant(gx, gy, worldSeed, tile.Variant, variantCount);
                    var layer = TileSpriteResolver.DrawLayerFor(inner);
                    var key = new TileSpriteKey(inner, TileSpriteRole.Side, variant);
                    destination.Add(new TileDrawOp(key, gx, gy, 1, layer, facing));
                }
            }
        }
    }

    private static void FillCategoryGrid(
        FloorSlice floor,
        int gx0,
        int gy0,
        int lw,
        int lh,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        TerrainRenderCategory[] categories)
    {
        for (var gy = gy0; gy < gy0 + lh; gy++)
        {
            for (var gx = gx0; gx < gx0 + lw; gx++)
            {
                var li = LocalIndex(gx0, gy0, lw, gx, gy);
                if (!floor.Contains(gx, gy))
                {
                    categories[li] = TerrainRenderCategory.Empty;
                    continue;
                }

                var tile = floor.Get(gx, gy);
                categories[li] = TerrainAppearance.Resolve(gx + 0.5f, gy + 0.5f, tile, evaluator, terrain);
            }
        }
    }

    private static TerrainRenderCategory SampleCategory(
        TerrainRenderCategory[] categories,
        int gx0,
        int gy0,
        int lw,
        int lh,
        int gx,
        int gy)
    {
        if (gx < gx0 || gy < gy0 || gx >= gx0 + lw || gy >= gy0 + lh)
            return TerrainRenderCategory.Empty;

        return categories[LocalIndex(gx0, gy0, lw, gx, gy)];
    }

    private static int LocalIndex(int gx0, int gy0, int lw, int gx, int gy) =>
        (gy - gy0) * lw + (gx - gx0);
}
