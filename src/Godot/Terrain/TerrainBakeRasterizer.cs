#nullable enable

using System;

using Godot;

using SpecialPG.Core.Maps;

using SpecialPG.Core.Maps.Noise;

using SpecialPG.Core.Maps.Rendering;



namespace SpecialPG;



/// <summary>CPU rasterization of terrain draw ops into a bake <see cref="Image"/>.</summary>

public static class TerrainBakeRasterizer

{

    /// <summary>

    /// Paints one <see cref="TileDrawOp"/> into <paramref name="dest"/> using chunk-local origin

    /// <paramref name="chunkGx0"/> / <paramref name="chunkGy0"/>. Pixel rect is clipped to the image bounds.

    /// </summary>

    public static void PaintOp(

        Image dest,

        in TileDrawOp op,

        int chunkGx0,

        int chunkGy0,

        int chunkLh,

        float cellSizePx,

        FloorSlice floor,

        ITerrainEvaluator eval,

        in TerrainNoiseConfig terrain,

        int worldSeed,

        bool useSprites,

        TerrainAtlasCatalog catalog,

        Image? atlasImage,

        bool waterAnimate,

        long animationTimeMs)

    {

        var fx = (op.OriginGx - chunkGx0) * cellSizePx;

        var fy = ChunkPatchOriginY(op.OriginGy, chunkGy0, op.SizeCells, chunkLh, cellSizePx);

        var pw = Mathf.Max(1, Mathf.CeilToInt(op.SizeCells * cellSizePx));

        var ph = pw;

        var destRect = new Rect2I(Mathf.FloorToInt(fx), Mathf.FloorToInt(fy), pw, ph);

        var clip = ClipRectToImage(destRect, dest.GetWidth(), dest.GetHeight());

        if (clip.Size.X <= 0 || clip.Size.Y <= 0)

            return;



        var spriteKey = ResolveSpriteKey(op.Key, op.OriginGx, op.OriginGy, waterAnimate, animationTimeMs, worldSeed);
        var hasSideRect = false;
        Rect2I sideRect = default;
        if (op.Facing is { } facing && op.Key.Role == TileSpriteRole.Side)
            hasSideRect = catalog.TryGetSidePixelRect(op.Key.Category, facing, op.Key.VariantIndex, out sideRect);

        var hasMainRect = false;
        Rect2I mainRect = default;
        if (useSprites && atlasImage is not null)
            hasMainRect = catalog.TryGetPixelRect(spriteKey, out mainRect);

        if (hasSideRect && atlasImage is not null)
        {
            if (clip == destRect &&
                clip.Size.X == TerrainAtlasCatalog.TilePixelSize &&
                clip.Size.Y == TerrainAtlasCatalog.TilePixelSize &&
                op.SizeCells == 1)
            {
                dest.BlitRect(atlasImage!, sideRect, clip.Position);
                return;
            }

            using var sidePatch = atlasImage!.GetRegion(sideRect);
            sidePatch.Resize(destRect.Size.X, destRect.Size.Y, Image.Interpolation.Nearest);
            var sideOffset = clip.Position - destRect.Position;
            dest.BlitRect(sidePatch, new Rect2I(sideOffset, clip.Size), clip.Position);
            return;
        }

        if (hasMainRect)
        {
            var srcRect = mainRect;
            if (clip == destRect &&
                clip.Size.X == TerrainAtlasCatalog.TilePixelSize &&
                clip.Size.Y == TerrainAtlasCatalog.TilePixelSize &&
                op.SizeCells == 1)
            {
                dest.BlitRect(atlasImage!, srcRect, clip.Position);
                return;
            }

            using var patch = atlasImage!.GetRegion(srcRect);

            patch.Resize(destRect.Size.X, destRect.Size.Y, Image.Interpolation.Nearest);

            var srcOffset = clip.Position - destRect.Position;

            dest.BlitRect(patch, new Rect2I(srcOffset, clip.Size), clip.Position);

            return;
        }

        PaintOpColor(dest, op, chunkGx0, chunkGy0, chunkLh, cellSizePx, floor, eval, terrain, clip, waterAnimate, animationTimeMs, worldSeed);

    }

    private static (int Dx, int Dy) FacingDelta(TransitionFacing facing) =>
        facing switch
        {
            TransitionFacing.North => (0, -1),
            TransitionFacing.East => (1, 0),
            TransitionFacing.South => (0, 1),
            _ => (-1, 0),
        };

    private static TileSpriteKey ResolveSpriteKey(
        TileSpriteKey key,
        int gx,
        int gy,
        bool waterAnimate,
        long animationTimeMs,
        int worldSeed)
    {
        if (!waterAnimate || !TerrainWaterAnimation.IsWaterCategory(key.Category))
        {
            return key;
        }

        var frame = TerrainWaterAnimation.GetFrameIndex(worldSeed, gx, gy, animationTimeMs);
        return key with { VariantIndex = frame };
    }



    /// <summary>

    /// Paints <paramref name="destPixelRect"/> using atlas sprites when possible; otherwise <see cref="TerrainVisualColor"/>.

    /// </summary>

    public static void PaintCell(

        Image dest,

        Rect2I destPixelRect,

        in TileCell tile,

        int gx,

        int gy,

        ITerrainEvaluator eval,

        in TerrainNoiseConfig terrain,

        int worldSeed,

        TerrainAtlasCatalog catalog,

        Image atlasImage)

    {

        Span<TileDrawOp> buf = stackalloc TileDrawOp[1];

        TileSpriteResolver.ResolveCell(

            gx,

            gy,

            tile,

            eval,

            terrain,

            worldSeed,

            TerrainAtlasCatalog.VariantsPerCategory,

            buf,

            out var opCount);



        if (opCount == 0)

        {

            PaintColorFallback(dest, destPixelRect, tile, gx, gy, eval, terrain);

            return;

        }



        var op = buf[0];

        if (!catalog.TryGetPixelRect(op.Key, out var srcRect))

        {

            PaintColorFallback(dest, destPixelRect, tile, gx, gy, eval, terrain);

            return;

        }



        if (destPixelRect.Size.X == TerrainAtlasCatalog.TilePixelSize &&

            destPixelRect.Size.Y == TerrainAtlasCatalog.TilePixelSize)

        {

            dest.BlitRect(atlasImage, srcRect, destPixelRect.Position);

            return;

        }



        using var patch = atlasImage.GetRegion(srcRect);

        patch.Resize(destPixelRect.Size.X, destPixelRect.Size.Y, Image.Interpolation.Nearest);

        dest.BlitRect(patch, new Rect2I(Vector2I.Zero, destPixelRect.Size), destPixelRect.Position);

    }



    private static void PaintOpColor(

        Image dest,

        in TileDrawOp op,

        int chunkGx0,

        int chunkGy0,

        int chunkLh,

        float cellSizePx,

        FloorSlice floor,

        ITerrainEvaluator eval,

        in TerrainNoiseConfig terrain,

        Rect2I clip,

        bool waterAnimate,

        long animationTimeMs,

        int worldSeed)

    {

        for (var dy = 0; dy < op.SizeCells; dy++)

        {

            for (var dx = 0; dx < op.SizeCells; dx++)

            {

                var gx = op.OriginGx + dx;

                var gy = op.OriginGy + dy;

                if (!floor.Contains(gx, gy))

                    continue;



                var fx = (gx - chunkGx0) * cellSizePx;

                var fy = ChunkPatchOriginY(gy, chunkGy0, 1, chunkLh, cellSizePx);

                var pw = Mathf.Max(1, Mathf.CeilToInt(cellSizePx));

                var cellRect = new Rect2I(Mathf.FloorToInt(fx), Mathf.FloorToInt(fy), pw, pw);

                var cellClip = IntersectRects(cellRect, clip);

                if (cellClip.Size.X <= 0 || cellClip.Size.Y <= 0)

                    continue;



                var tile = floor.Get(gx, gy);

                if (op.Facing is { } facing)
                {
                    PaintTransitionColorFallback(
                        dest, cellClip, floor, tile, gx, gy, facing, eval, terrain, waterAnimate, animationTimeMs, worldSeed);
                }
                else
                {
                    PaintColorFallback(dest, cellClip, tile, gx, gy, eval, terrain, waterAnimate, animationTimeMs, worldSeed);
                }

            }

        }

    }

    private static void PaintTransitionColorFallback(
        Image dest,
        Rect2I destPixelRect,
        FloorSlice floor,
        in TileCell tile,
        int gx,
        int gy,
        TransitionFacing facing,
        ITerrainEvaluator eval,
        in TerrainNoiseConfig terrain,
        bool waterAnimate,
        long animationTimeMs,
        int worldSeed)
    {
        var waterFrame = waterAnimate
            ? TerrainWaterAnimation.GetFrameIndex(worldSeed, gx, gy, animationTimeMs)
            : -1;
        var inner = TerrainVisualColor.AtWorld(gx + 0.5f, gy + 0.5f, tile, eval, terrain, waterFrame);

        var (dx, dy) = FacingDelta(facing);
        var ngx = gx + dx;
        var ngy = gy + dy;
        TerrainRgb neighbor;
        if (floor.Contains(ngx, ngy))
        {
            var nTile = floor.Get(ngx, ngy);
            var nFrame = waterAnimate
                ? TerrainWaterAnimation.GetFrameIndex(worldSeed, ngx, ngy, animationTimeMs)
                : -1;
            neighbor = TerrainVisualColor.AtWorld(ngx + 0.5f, ngy + 0.5f, nTile, eval, terrain, nFrame);
        }
        else
        {
            neighbor = TerrainVisualColor.AtWorld(ngx + 0.5f, ngy + 0.5f, default, eval, terrain);
        }

        var rgb = LerpRgb(inner, neighbor, 0.5f);
        dest.FillRect(destPixelRect, new Color(rgb.R, rgb.G, rgb.B, 1f));
    }

    private static TerrainRgb LerpRgb(TerrainRgb a, TerrainRgb b, float t)
    {
        return new TerrainRgb(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);
    }



    public static void PaintColorFallback(

        Image dest,

        Rect2I destPixelRect,

        in TileCell tile,

        int gx,

        int gy,

        ITerrainEvaluator eval,

        in TerrainNoiseConfig terrain,

        bool waterAnimate = false,

        long animationTimeMs = 0,

        int worldSeed = 0)

    {

        var worldX = gx + 0.5f;

        var worldY = gy + 0.5f;

        var waterFrame = waterAnimate
            ? TerrainWaterAnimation.GetFrameIndex(worldSeed, gx, gy, animationTimeMs)
            : -1;

        var rgb = TerrainVisualColor.AtWorld(worldX, worldY, tile, eval, terrain, waterFrame);

        dest.FillRect(destPixelRect, new Color(rgb.R, rgb.G, rgb.B, 1f));

    }



    /// <summary>Top image row = northern chunk edge (see <c>GameRoot.CellRectGlobal</c> Y flip).</summary>
    private static int ChunkPatchOriginY(int originGy, int chunkGy0, int patchSizeCells, int chunkLh, float cellSizePx)
    {
        var northLocalGy = originGy - chunkGy0 + patchSizeCells - 1;
        return Mathf.FloorToInt((chunkLh - 1 - northLocalGy) * cellSizePx);
    }

    private static Rect2I ClipRectToImage(Rect2I rect, int imgW, int imgH)

    {

        var x0 = Mathf.Max(0, rect.Position.X);

        var y0 = Mathf.Max(0, rect.Position.Y);

        var x1 = Mathf.Min(imgW, rect.Position.X + rect.Size.X);

        var y1 = Mathf.Min(imgH, rect.Position.Y + rect.Size.Y);

        return new Rect2I(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));

    }



    private static Rect2I IntersectRects(Rect2I a, Rect2I b)

    {

        var x0 = Mathf.Max(a.Position.X, b.Position.X);

        var y0 = Mathf.Max(a.Position.Y, b.Position.Y);

        var x1 = Mathf.Min(a.Position.X + a.Size.X, b.Position.X + b.Size.X);

        var y1 = Mathf.Min(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);

        return new Rect2I(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));

    }

}

