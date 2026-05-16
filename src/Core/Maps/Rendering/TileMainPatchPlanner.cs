using System;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>
/// Plans main terrain patches (4×4, 2×2, 1×1) over a global cell rectangle using grid anchors and an ownership grid.
/// </summary>
public static class TileMainPatchPlanner
{
    /// <summary>Weights for patch-size roll at anchors: (size cells, weight). Total weight = 7.</summary>
    public static readonly (int Size, int Weight)[] MainPatchWeights = { (4, 1), (2, 2), (1, 4) };

    private const int TotalPatchWeight = 7;

    /// <summary>
    /// Fills <paramref name="destination"/> with non-overlapping main ops covering every cell in
    /// [<paramref name="gx0"/>, <paramref name="gx0"/>+<paramref name="lw"/>) × [<paramref name="gy0"/>, <paramref name="gy0"/>+<paramref name="lh"/>).
    /// </summary>
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

        destination.Clear();
        var owned = new bool[lw * lh];

        for (var gy = gy0; gy < gy0 + lh; gy++)
        {
            if (gy % 4 != 0)
                continue;

            for (var gx = gx0; gx < gx0 + lw; gx++)
            {
                if (gx % 4 != 0)
                    continue;
                if (!IsAreaFree(owned, gx0, gy0, lw, lh, gx, gy, 4))
                    continue;
                if (!TryGetUniformCategory(floor, evaluator, terrain, gx, gy, 4, out var category, out var anchorTile))
                    continue;
                if (!RollWantsPatchSize(worldSeed, gx, gy, 4))
                    continue;

                EmitPatch(destination, owned, gx0, gy0, lw, lh, gx, gy, anchorTile, evaluator, terrain, worldSeed,
                    variantCount, category, TileSpriteRole.Main4x4, 4);
            }
        }

        for (var gy = gy0; gy < gy0 + lh; gy++)
        {
            if (gy % 2 != 0)
                continue;

            for (var gx = gx0; gx < gx0 + lw; gx++)
            {
                if (gx % 2 != 0)
                    continue;
                if (!IsAreaFree(owned, gx0, gy0, lw, lh, gx, gy, 2))
                    continue;
                if (!TryGetUniformCategory(floor, evaluator, terrain, gx, gy, 2, out var category, out var anchorTile))
                    continue;
                if (!RollWantsPatchSize(worldSeed, gx, gy, 2))
                    continue;

                EmitPatch(destination, owned, gx0, gy0, lw, lh, gx, gy, anchorTile, evaluator, terrain, worldSeed,
                    variantCount, category, TileSpriteRole.Main2x2, 2);
            }
        }

        for (var gy = gy0; gy < gy0 + lh; gy++)
        {
            for (var gx = gx0; gx < gx0 + lw; gx++)
            {
                if (!IsCellFree(owned, gx0, gy0, lw, lh, gx, gy))
                    continue;
                if (!TryGetTile(floor, gx, gy, out var tile))
                    continue;

                var op = TileSpriteResolver.MakeMainOp(gx, gy, tile, evaluator, terrain, worldSeed, variantCount,
                    TileSpriteRole.Main1x1, 1);
                destination.Add(op);
                MarkOwned(owned, gx0, gy0, lw, lh, gx, gy, 1);
            }
        }
    }

    private static void EmitPatch(
        List<TileDrawOp> destination,
        bool[] owned,
        int gx0,
        int gy0,
        int lw,
        int lh,
        int gx,
        int gy,
        in TileCell anchorTile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        int variantCount,
        TerrainRenderCategory category,
        TileSpriteRole role,
        int size)
    {
        var op = TileSpriteResolver.MakeMainOp(gx, gy, anchorTile, evaluator, terrain, worldSeed, variantCount, role, size);
        destination.Add(op);
        MarkOwned(owned, gx0, gy0, lw, lh, gx, gy, size);
    }

    private static bool RollWantsPatchSize(int worldSeed, int gx, int gy, int sizeCells)
    {
        var roll = (uint)HashCode.Combine(gx, gy, worldSeed) % TotalPatchWeight;
        return sizeCells switch
        {
            4 => roll < MainPatchWeights[0].Weight,
            2 => roll >= MainPatchWeights[0].Weight && roll < MainPatchWeights[0].Weight + MainPatchWeights[1].Weight,
            _ => roll >= MainPatchWeights[0].Weight + MainPatchWeights[1].Weight,
        };
    }

    private static bool TryGetUniformCategory(
        FloorSlice floor,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int gx,
        int gy,
        int size,
        out TerrainRenderCategory category,
        out TileCell anchorTile)
    {
        if (!TryGetTile(floor, gx, gy, out anchorTile))
        {
            category = default;
            return false;
        }

        category = TerrainAppearance.Resolve(gx + 0.5f, gy + 0.5f, anchorTile, evaluator, terrain);
        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                if (!TryGetTile(floor, gx + dx, gy + dy, out var t))
                    return false;
                var c = TerrainAppearance.Resolve(gx + dx + 0.5f, gy + dy + 0.5f, t, evaluator, terrain);
                if (c != category)
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetTile(FloorSlice floor, int gx, int gy, out TileCell tile)
    {
        if (!floor.Contains(gx, gy))
        {
            tile = default;
            return false;
        }

        tile = floor.Get(gx, gy);
        return true;
    }

    private static int LocalIndex(int gx0, int gy0, int lw, int gx, int gy) => (gy - gy0) * lw + (gx - gx0);

    private static bool IsCellFree(bool[] owned, int gx0, int gy0, int lw, int lh, int gx, int gy) =>
        !owned[LocalIndex(gx0, gy0, lw, gx, gy)];

    private static bool IsAreaFree(bool[] owned, int gx0, int gy0, int lw, int lh, int gx, int gy, int size)
    {
        if (gx < gx0 || gy < gy0 || gx + size > gx0 + lw || gy + size > gy0 + lh)
            return false;

        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                if (!IsCellFree(owned, gx0, gy0, lw, lh, gx + dx, gy + dy))
                    return false;
            }
        }

        return true;
    }

    private static void MarkOwned(bool[] owned, int gx0, int gy0, int lw, int lh, int gx, int gy, int size)
    {
        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
                owned[LocalIndex(gx0, gy0, lw, gx + dx, gy + dy)] = true;
        }
    }
}
