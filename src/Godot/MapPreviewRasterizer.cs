#nullable enable
using System;
using Godot;
using SpecialPG.Core.Maps;
using CoreTileData = SpecialPG.Core.Maps.TileData;

/// <summary>
/// Top-down RGBA snapshot of a <see cref="FloorSlice"/> for the map workbench preview (Shell-only).
/// </summary>
public static class MapPreviewRasterizer
{
    /// <summary>
    /// Downsample so the longest map edge maps to at most <paramref name="maxCellsOnLongEdge"/> pixels (nearest mapping).
    /// </summary>
    public static ImageTexture RasterizeFloor(FloorSlice floor, int maxCellsOnLongEdge = 128)
    {
        ArgumentNullException.ThrowIfNull(floor);
        var w = floor.Width;
        var h = floor.Height;
        if (w <= 0 || h <= 0)
            throw new ArgumentException("Floor has no extent.");

        var imgW = Mathf.Min(w, maxCellsOnLongEdge);
        var imgH = Mathf.Min(h, maxCellsOnLongEdge);
        imgW = Mathf.Max(1, imgW);
        imgH = Mathf.Max(1, imgH);

        var img = Image.CreateEmpty(imgW, imgH, false, Image.Format.Rgba8);

        for (var py = 0; py < imgH; py++)
        {
            for (var px = 0; px < imgW; px++)
            {
                var gx = floor.MinX + px * (w - 1) / Mathf.Max(1, imgW - 1);
                var gy = floor.MinY + py * (h - 1) / Mathf.Max(1, imgH - 1);
                if (w == 1)
                    gx = floor.MinX;
                if (h == 1)
                    gy = floor.MinY;

                var t = floor.Get(gx, gy);
                var c = TileToColor(t);
                img.SetPixel(px, py, c);
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        return tex;
    }

    private static Color TileToColor(CoreTileData t)
    {
        if (t.TileKind == TerrainTileKinds.Water)
            return new Color(0.25f, 0.55f, 0.92f, 1f);
        if ((t.Flags & TileFlags.Blocked) != 0 && t.TileKind != TerrainTileKinds.Water)
            return new Color(0.20f, 0.45f, 0.24f, 1f);
        if (t.TileKind == TerrainTileKinds.Land || t.TileKind is >= 1 and <= 8)
            return new Color(0.32f, 0.72f, 0.38f, 1f);

        return new Color(0.5f, 0.52f, 0.55f, 1f);
    }
}
