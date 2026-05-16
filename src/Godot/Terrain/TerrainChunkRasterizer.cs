#nullable enable

using System.Collections.Generic;

using Godot;

using SpecialPG.Core.Maps;

using SpecialPG.Core.Maps.Noise;

using SpecialPG.Core.Maps.Rendering;

namespace SpecialPG;

/// <summary>Builds a chunk-local <see cref="Image"/> for one floor chunk.</summary>
public static class TerrainChunkRasterizer
{
    /// <summary>Planning margin for transition sampling at chunk edges.</summary>
    public const int TransitionMarginCells = 1;

    public static Image BuildChunkImage(
        FloorSlice floor,
        int cx,
        int cy,
        ITerrainEvaluator eval,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        float cellSizePx,
        bool useSprites,
        TerrainAtlasCatalog catalog,
        Image? atlasImage,
        List<TileDrawOp> mainOps,
        List<TileDrawOp> transitionOps,
        bool waterAnimate,
        long animationTimeMs,
        bool transitionsEnabled)
    {
        floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out var lw, out var lh);
        var imgW = Mathf.Max(1, Mathf.CeilToInt(lw * cellSizePx));
        var imgH = Mathf.Max(1, Mathf.CeilToInt(lh * cellSizePx));
        var img = Image.CreateEmpty(imgW, imgH, false, Image.Format.Rgba8);

        var planGx0 = gx0 - TransitionMarginCells;
        var planGy0 = gy0 - TransitionMarginCells;
        var planLw = lw + TransitionMarginCells * 2;
        var planLh = lh + TransitionMarginCells * 2;

        mainOps.Clear();
        transitionOps.Clear();
        var cap = Mathf.Max(4, planLw * planLh / 4);
        if (mainOps.Capacity < cap)
            mainOps.Capacity = cap;
        if (transitionOps.Capacity < cap)
            transitionOps.Capacity = cap;

        TileMainPatchPlanner.Plan(
            floor,
            planGx0,
            planGy0,
            planLw,
            planLh,
            eval,
            terrain,
            worldSeed,
            TerrainAtlasCatalog.VariantsPerCategory,
            mainOps);

        if (transitionsEnabled)
        {
            TileTransitionPlanner.Plan(
                floor,
                planGx0,
                planGy0,
                planLw,
                planLh,
                eval,
                terrain,
                worldSeed,
                TerrainAtlasCatalog.VariantsPerCategory,
                transitionOps);
        }

        mainOps.Sort(TileDrawOpComparer.Instance);
        transitionOps.Sort(TileDrawOpComparer.Instance);

        var spriteOk = useSprites && atlasImage is not null;
        PaintOpList(img, mainOps, gx0, gy0, lh, cellSizePx, floor, eval, terrain, worldSeed, spriteOk, catalog, atlasImage, waterAnimate, animationTimeMs);
        PaintOpList(img, transitionOps, gx0, gy0, lh, cellSizePx, floor, eval, terrain, worldSeed, spriteOk, catalog, atlasImage, waterAnimate, animationTimeMs);

        return img;
    }

    private static void PaintOpList(
        Image img,
        List<TileDrawOp> ops,
        int gx0,
        int gy0,
        int lh,
        float cellSizePx,
        FloorSlice floor,
        ITerrainEvaluator eval,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        bool spriteOk,
        TerrainAtlasCatalog catalog,
        Image? atlasImage,
        bool waterAnimate,
        long animationTimeMs)
    {
        foreach (var op in ops)
        {
            TerrainBakeRasterizer.PaintOp(
                img,
                op,
                gx0,
                gy0,
                lh,
                cellSizePx,
                floor,
                eval,
                terrain,
                worldSeed,
                spriteOk,
                catalog,
                atlasImage,
                waterAnimate,
                animationTimeMs);
        }
    }
}
