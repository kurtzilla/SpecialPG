#nullable enable
using System;
using Godot;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;

/// <summary>
/// Top-down RGBA snapshot of a <see cref="FloorSlice"/> for the map workbench preview (Shell-only).
/// </summary>
public static class MapPreviewRasterizer
{
    /// <summary>
    /// Downsample so the longest map edge maps to at most <paramref name="maxCellsOnLongEdge"/> pixels (nearest mapping).
    /// </summary>
    public static ImageTexture RasterizeFloor(FloorSlice floor, TerrainNoiseConfig terrain,
        int maxCellsOnLongEdge = 128)
    {
        ArgumentNullException.ThrowIfNull(floor);
        var eval = new TerrainEvaluator(terrain);
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
                var wx = gx + 0.5f;
                var wy = gy + 0.5f;
                var rgb = TerrainVisualColor.AtWorld(wx, wy, t, eval, terrain);
                img.SetPixel(px, py, new Color(rgb.R, rgb.G, rgb.B, 1f));
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        return tex;
    }
}
