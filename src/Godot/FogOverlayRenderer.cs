#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>
/// Draws fog as a single board-sized quad and shapes edge alpha in shader from a floor-scoped mask texture.
/// </summary>
public partial class FogOverlayRenderer : Node2D
{
    private sealed class FloorMask
    {
        public FloorMask(Image targetImage, Image displayImage, ImageTexture displayTexture, int width, int height)
        {
            TargetImage = targetImage;
            DisplayImage = displayImage;
            DisplayTexture = displayTexture;
            Width = width;
            Height = height;
        }

        public Image TargetImage { get; }
        public Image DisplayImage { get; }
        public ImageTexture DisplayTexture { get; }
        public int Width { get; }
        public int Height { get; }
        public int DirtyMinX { get; set; } = int.MaxValue;
        public int DirtyMinY { get; set; } = int.MaxValue;
        public int DirtyMaxX { get; set; } = int.MinValue;
        public int DirtyMaxY { get; set; } = int.MinValue;
        public bool HasDirtyRect => DirtyMinX <= DirtyMaxX && DirtyMinY <= DirtyMaxY;

        public void MarkDirty(int x0, int y0, int x1, int y1)
        {
            if (x0 < DirtyMinX) DirtyMinX = x0;
            if (y0 < DirtyMinY) DirtyMinY = y0;
            if (x1 > DirtyMaxX) DirtyMaxX = x1;
            if (y1 > DirtyMaxY) DirtyMaxY = y1;
        }

        public void SetDirtyRect(int minX, int minY, int maxX, int maxY)
        {
            DirtyMinX = minX;
            DirtyMinY = minY;
            DirtyMaxX = maxX;
            DirtyMaxY = maxY;
        }

        public void ClearDirtyRect()
        {
            DirtyMinX = int.MaxValue;
            DirtyMinY = int.MaxValue;
            DirtyMaxX = int.MinValue;
            DirtyMaxY = int.MinValue;
        }
    }

    private readonly Dictionary<int, FloorMask> _masksByFloorZ = new();
    private readonly Dictionary<int, Vector2I> _floorCellMins = new();
    private readonly Dictionary<int, Vector2I> _floorCellSizes = new();
    private ShaderMaterial? _material;
    private ImageTexture? _whiteTexture;
    private ImageTexture? _defaultMaskTexture;
    private Rect2 _boardRect;
    private int _boardWidthCells;
    private int _boardHeightCells;
    private int _floorMinX;
    private int _floorMinY;
    private int _pixelsPerCell = 8;
    private int _activeFloorZ = int.MinValue;
    private bool _gpuEnabled = true;
    private bool _fogSlidingMaskEnabled = true;
    private bool _largeMapMode;
    private int _maskWindowCells = 256;
    private int _recenterMarginCells = 48;
    private readonly Dictionary<int, Vector2I> _maskOriginGlobalByFloor = new();
    private readonly Dictionary<int, Vector2I> _maskAllocatedCellsByFloor = new();
    private float _revealLerpSpeed = 10.0f;
    private float _brushHardCoreRatio = 0.72f;
    private float _brushFeatherExponent = 1.40f;
    private int _revealUpdateHz = 20;
    private double _revealUpdateAccumS;

    public override void _Ready()
    {
        ZIndex = 6;
        EnsureMaterial();
        EnsureWhiteTexture();
        EnsureDefaultMaskTexture();
    }

    public void ConfigureStyle(Color fogColor, float edgeWidthCells, float edgeSoftness, int blurSamples, bool gpuEnabled)
    {
        EnsureMaterial();
        _gpuEnabled = gpuEnabled;
        _material!.SetShaderParameter("fog_color", fogColor);
        _material.SetShaderParameter("edge_width_cells", Mathf.Max(0.1f, edgeWidthCells));
        _material.SetShaderParameter("edge_softness", Mathf.Max(0.2f, edgeSoftness));
        _material.SetShaderParameter("blur_taps", Mathf.Clamp(blurSamples, 1, 12));
        Visible = gpuEnabled;
        QueueRedraw();
    }

    public void ConfigureRevealSmoothing(float revealLerpSpeed, float brushHardCoreRatio, float brushFeatherExponent,
        int revealUpdateHz)
    {
        _revealLerpSpeed = Mathf.Clamp(revealLerpSpeed, 0.5f, 40.0f);
        _brushHardCoreRatio = Mathf.Clamp(brushHardCoreRatio, 0.2f, 0.95f);
        _brushFeatherExponent = Mathf.Clamp(brushFeatherExponent, 0.5f, 4.0f);
        _revealUpdateHz = Mathf.Clamp(revealUpdateHz, 5, 240);
    }

    public void ConfigureBoard(Vector2 boardOriginTopLeft, int floorWidthCells, int floorHeightCells, float cellSizePx, int pixelsPerCell)
    {
        _boardWidthCells = floorWidthCells;
        _boardHeightCells = floorHeightCells;
        _boardRect = new Rect2(boardOriginTopLeft, new Vector2(floorWidthCells * cellSizePx, floorHeightCells * cellSizePx));
        _pixelsPerCell = Mathf.Clamp(pixelsPerCell, 1, 32);
    }

    /// <summary>Sliding window fog mask: enabled when <paramref name="slidingMaskEnabled"/> and (large map mode or map area &gt;= 4M cells).</summary>
    public void SetSlidingFogMode(bool slidingMaskEnabled, bool largeMapMode, int maskWindowCells, int recenterMarginCells)
    {
        _fogSlidingMaskEnabled = slidingMaskEnabled;
        _largeMapMode = largeMapMode;
        _maskWindowCells = Mathf.Clamp(maskWindowCells, 32, 2048);
        _recenterMarginCells = Mathf.Clamp(recenterMarginCells, 8, Math.Max(8, _maskWindowCells / 2));
    }

    public void EnsureFloorMask(int floorZ, int floorMinX, int floorMinY, int floorWidthCells, int floorHeightCells)
    {
        _floorMinX = floorMinX;
        _floorMinY = floorMinY;
        _floorCellMins[floorZ] = new Vector2I(floorMinX, floorMinY);
        _floorCellSizes[floorZ] = new Vector2I(floorWidthCells, floorHeightCells);

        var useSlidingAlloc = _fogSlidingMaskEnabled &&
                              (_largeMapMode || (long)floorWidthCells * floorHeightCells >= 4_000_000L);
        var maskWCells = useSlidingAlloc ? Mathf.Min(_maskWindowCells, floorWidthCells) : floorWidthCells;
        var maskHCells = useSlidingAlloc ? Mathf.Min(_maskWindowCells, floorHeightCells) : floorHeightCells;

        _maskAllocatedCellsByFloor[floorZ] = new Vector2I(maskWCells, maskHCells);

        if (!useSlidingAlloc)
            _maskOriginGlobalByFloor.Remove(floorZ);

        var maskWidth = Math.Max(1, maskWCells * _pixelsPerCell);
        var maskHeight = Math.Max(1, maskHCells * _pixelsPerCell);

        if (_masksByFloorZ.TryGetValue(floorZ, out var existing) &&
            existing.Width == maskWidth && existing.Height == maskHeight)
        {
            PushSlidingShaderUniforms(useSlidingAlloc, floorMinX, floorMinY, floorWidthCells, floorHeightCells, floorZ);
            return;
        }

        var targetImage = Image.CreateEmpty(maskWidth, maskHeight, false, Image.Format.Rgba8);
        targetImage.Fill(new Color(0f, 0f, 0f, 0f));
        var displayImage = Image.CreateEmpty(maskWidth, maskHeight, false, Image.Format.Rgba8);
        displayImage.Fill(new Color(0f, 0f, 0f, 0f));
        var displayTexture = ImageTexture.CreateFromImage(displayImage);
        _masksByFloorZ[floorZ] = new FloorMask(targetImage, displayImage, displayTexture, maskWidth, maskHeight);
        _maskOriginGlobalByFloor.Remove(floorZ);

        PushSlidingShaderUniforms(useSlidingAlloc, floorMinX, floorMinY, floorWidthCells, floorHeightCells, floorZ);

        if (floorZ == _activeFloorZ)
        {
            _material?.SetShaderParameter("fog_mask", displayTexture);
            QueueRedraw();
        }
    }

    /// <summary>Re-center sliding mask when the anchor nears the edge; refill from Core fog state.</summary>
    public void SyncSlidingMaskAnchor(int floorZ, int anchorGlobalX, int anchorGlobalY, WorldState world, int playerId = 0)
    {
        if (!_fogSlidingMaskEnabled || !_masksByFloorZ.ContainsKey(floorZ) || !_floorCellSizes.TryGetValue(floorZ, out var fsize))
            return;

        if (!_largeMapMode && (long)fsize.X * fsize.Y < 4_000_000L)
            return;

        if (!_maskAllocatedCellsByFloor.TryGetValue(floorZ, out var mcells))
            return;

        var floorMin = _floorCellMins[floorZ];
        var mw = mcells.X;
        var mh = mcells.Y;

        var needInit = !_maskOriginGlobalByFloor.TryGetValue(floorZ, out var origin);
        var ox = needInit ? anchorGlobalX - mw / 2 : origin.X;
        var oy = needInit ? anchorGlobalY - mh / 2 : origin.Y;

        ox = Mathf.Clamp(ox, floorMin.X, floorMin.X + Mathf.Max(0, fsize.X - mw));
        oy = Mathf.Clamp(oy, floorMin.Y, floorMin.Y + Mathf.Max(0, fsize.Y - mh));

        if (!needInit)
        {
            var left = ox + _recenterMarginCells;
            var right = ox + mw - 1 - _recenterMarginCells;
            var bottom = oy + _recenterMarginCells;
            var top = oy + mh - 1 - _recenterMarginCells;
            if (anchorGlobalX >= left && anchorGlobalX <= right && anchorGlobalY >= bottom && anchorGlobalY <= top)
            {
                PushSlidingShaderUniforms(true, floorMin.X, floorMin.Y, fsize.X, fsize.Y, floorZ);
                return;
            }

            ox = Mathf.Clamp(anchorGlobalX - mw / 2, floorMin.X, floorMin.X + Mathf.Max(0, fsize.X - mw));
            oy = Mathf.Clamp(anchorGlobalY - mh / 2, floorMin.Y, floorMin.Y + Mathf.Max(0, fsize.Y - mh));
        }

        _maskOriginGlobalByFloor[floorZ] = new Vector2I(ox, oy);
        RestampMaskFromFog(world, playerId, floorZ);
        PushSlidingShaderUniforms(true, floorMin.X, floorMin.Y, fsize.X, fsize.Y, floorZ);
    }

    private void RestampMaskFromFog(WorldState world, int playerId, int floorZ)
    {
        if (!_masksByFloorZ.TryGetValue(floorZ, out var floorMask) ||
            !_maskOriginGlobalByFloor.TryGetValue(floorZ, out var org) ||
            !_maskAllocatedCellsByFloor.TryGetValue(floorZ, out var mc) ||
            !_floorCellMins.TryGetValue(floorZ, out var fmin) ||
            !_floorCellSizes.TryGetValue(floorZ, out var fsz))
        {
            return;
        }

        floorMask.TargetImage.Fill(new Color(0f, 0f, 0f, 0f));
        floorMask.DisplayImage.Fill(new Color(0f, 0f, 0f, 0f));
        floorMask.ClearDirtyRect();

        var ppc = _pixelsPerCell;
        for (var gy = org.Y; gy < org.Y + mc.Y; gy++)
        {
            for (var gx = org.X; gx < org.X + mc.X; gx++)
            {
                if (!world.Fog.IsRevealed(playerId, floorZ, gx, gy, fmin.X, fmin.Y, fsz.X, fsz.Y))
                    continue;

                var lx = gx - org.X;
                var ly = gy - org.Y;
                var flippedY = mc.Y - 1 - ly;
                for (var py = 0; py < ppc; py++)
                {
                    for (var px = 0; px < ppc; px++)
                    {
                        var ipx = lx * ppc + px;
                        var ipy = flippedY * ppc + py;
                        floorMask.TargetImage.SetPixel(ipx, ipy, new Color(1f, 1f, 1f, 1f));
                    }
                }
            }
        }

        floorMask.DisplayImage.CopyFrom(floorMask.TargetImage);
        floorMask.DisplayTexture.Update(floorMask.DisplayImage);
        if (floorZ == _activeFloorZ)
            QueueRedraw();
    }

    private void PushSlidingShaderUniforms(bool sliding, int floorMinX, int floorMinY, int floorW, int floorH, int floorZ)
    {
        EnsureMaterial();
        if (_material is null)
            return;

        var hasOrigin = _maskOriginGlobalByFloor.TryGetValue(floorZ, out var org);
        var hasMc = _maskAllocatedCellsByFloor.TryGetValue(floorZ, out var mc);
        var useSliding = sliding && hasOrigin && hasMc;

        _material.SetShaderParameter("sliding_fog_mask", useSliding);
        _material.SetShaderParameter("board_cells", new Vector2(floorW, floorH));
        _material.SetShaderParameter("floor_min_xy", new Vector2(floorMinX, floorMinY));
        _material.SetShaderParameter("mask_pixels_per_cell", (float)_pixelsPerCell);

        if (useSliding)
        {
            _material.SetShaderParameter("mask_origin_global", new Vector2(org.X, org.Y));
            _material.SetShaderParameter("mask_cells", new Vector2(mc.X, mc.Y));
        }
        else
        {
            _material.SetShaderParameter("mask_origin_global", new Vector2(floorMinX, floorMinY));
            _material.SetShaderParameter("mask_cells", new Vector2(floorW, floorH));
        }
    }

    public void SetActiveFloor(int floorZ)
    {
        _activeFloorZ = floorZ;
        EnsureMaterial();
        if (_masksByFloorZ.TryGetValue(floorZ, out var floorMask))
        {
            _material!.SetShaderParameter("fog_mask", floorMask.DisplayTexture);
        }

        if (_floorCellSizes.TryGetValue(floorZ, out var fsz) && _floorCellMins.TryGetValue(floorZ, out var fmin))
        {
            var useSlidingAlloc = _fogSlidingMaskEnabled &&
                                  (_largeMapMode || (long)fsz.X * fsz.Y >= 4_000_000L);
            PushSlidingShaderUniforms(useSlidingAlloc, fmin.X, fmin.Y, fsz.X, fsz.Y, floorZ);
        }

        QueueRedraw();
    }

    public void SnapDisplayToTarget(int floorZ)
    {
        if (!_masksByFloorZ.TryGetValue(floorZ, out var floorMask))
        {
            return;
        }

        floorMask.DisplayImage.CopyFrom(floorMask.TargetImage);
        floorMask.DisplayTexture.Update(floorMask.DisplayImage);
        floorMask.ClearDirtyRect();
        if (floorZ == _activeFloorZ)
        {
            QueueRedraw();
        }
    }

    public void StampRevealCircle(int floorZ, int centerX, int centerY, int radiusCells)
    {
        StampRevealCircleAtGlobal(floorZ, centerX + 0.5f, centerY + 0.5f, radiusCells);
    }

    public void StampRevealCircleAtGlobal(int floorZ, float centerGlobalX, float centerGlobalY, float radiusCells)
    {
        if (!_masksByFloorZ.TryGetValue(floorZ, out var floorMask) ||
            !_floorCellMins.TryGetValue(floorZ, out var min) ||
            !_floorCellSizes.TryGetValue(floorZ, out var size))
        {
            return;
        }

        var hasMaskOrg = _maskOriginGlobalByFloor.TryGetValue(floorZ, out var maskOrg);
        var hasMcells = _maskAllocatedCellsByFloor.TryGetValue(floorZ, out var mcells);
        var sliding = _fogSlidingMaskEnabled &&
                      (_largeMapMode || (long)size.X * size.Y >= 4_000_000L) &&
                      hasMaskOrg &&
                      hasMcells;

        float localCenterX;
        float localCenterY;
        float flippedCellY;
        if (sliding)
        {
            localCenterX = centerGlobalX - maskOrg.X;
            localCenterY = centerGlobalY - maskOrg.Y;
            flippedCellY = (mcells.Y - 1) - localCenterY;
        }
        else
        {
            localCenterX = centerGlobalX - min.X;
            localCenterY = centerGlobalY - min.Y;
            // Match GameRoot's north-up mapping: texture Y grows down while global Y grows north/up.
            flippedCellY = (size.Y - 1) - localCenterY;
        }

        var centerPxX = localCenterX * _pixelsPerCell;
        var centerPxY = flippedCellY * _pixelsPerCell;
        var radiusPx = Mathf.Max(0.5f, radiusCells * _pixelsPerCell);
        var x0 = Mathf.Clamp(Mathf.FloorToInt(centerPxX - radiusPx), 0, floorMask.Width - 1);
        var x1 = Mathf.Clamp(Mathf.CeilToInt(centerPxX + radiusPx), 0, floorMask.Width - 1);
        var y0 = Mathf.Clamp(Mathf.FloorToInt(centerPxY - radiusPx), 0, floorMask.Height - 1);
        var y1 = Mathf.Clamp(Mathf.CeilToInt(centerPxY + radiusPx), 0, floorMask.Height - 1);
        var radiusSq = radiusPx * radiusPx;

        var hardCorePx = radiusPx * _brushHardCoreRatio;
        var featherPx = MathF.Max(0.001f, radiusPx - hardCorePx);

        for (var py = y0; py <= y1; py++)
        {
            var dy = (py + 0.5f) - centerPxY;
            for (var px = x0; px <= x1; px++)
            {
                var dx = (px + 0.5f) - centerPxX;
                var d2 = dx * dx + dy * dy;
                if (d2 <= radiusSq)
                {
                    var d = Mathf.Sqrt(d2);
                    float brushAlpha;
                    if (d <= hardCorePx)
                    {
                        brushAlpha = 1f;
                    }
                    else
                    {
                        var t = Mathf.Clamp((radiusPx - d) / featherPx, 0f, 1f);
                        brushAlpha = Mathf.Pow(t, _brushFeatherExponent);
                    }

                    var prev = floorMask.TargetImage.GetPixel(px, py);
                    if (brushAlpha > prev.A)
                    {
                        floorMask.TargetImage.SetPixel(px, py, new Color(1f, 1f, 1f, brushAlpha));
                    }
                }
            }
        }

        floorMask.MarkDirty(x0, y0, x1, y1);
        if (floorZ == _activeFloorZ)
        {
            QueueRedraw();
        }
    }

    public void AdvanceRevealAnimation(double deltaSeconds)
    {
        if (!_masksByFloorZ.TryGetValue(_activeFloorZ, out var floorMask) || !floorMask.HasDirtyRect)
        {
            return;
        }

        _revealUpdateAccumS += Math.Max(0.0, deltaSeconds);
        var stepS = 1.0 / Math.Max(1, _revealUpdateHz);
        if (_revealUpdateAccumS < stepS)
        {
            return;
        }

        var effectiveDt = (float)_revealUpdateAccumS;
        _revealUpdateAccumS = 0.0;
        var lerpFactor = 1f - Mathf.Exp(-_revealLerpSpeed * effectiveDt);
        if (lerpFactor <= 0f)
        {
            return;
        }

        var minX = Mathf.Clamp(floorMask.DirtyMinX, 0, floorMask.Width - 1);
        var maxX = Mathf.Clamp(floorMask.DirtyMaxX, 0, floorMask.Width - 1);
        var minY = Mathf.Clamp(floorMask.DirtyMinY, 0, floorMask.Height - 1);
        var maxY = Mathf.Clamp(floorMask.DirtyMaxY, 0, floorMask.Height - 1);
        const float doneEps = 0.003f;
        var stillDirty = false;
        var nextMinX = int.MaxValue;
        var nextMinY = int.MaxValue;
        var nextMaxX = int.MinValue;
        var nextMaxY = int.MinValue;

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                var targetA = floorMask.TargetImage.GetPixel(px, py).A;
                var display = floorMask.DisplayImage.GetPixel(px, py);
                var currentA = display.A;
                if (Mathf.Abs(targetA - currentA) <= doneEps)
                {
                    if (targetA != currentA)
                    {
                        floorMask.DisplayImage.SetPixel(px, py, new Color(1f, 1f, 1f, targetA));
                    }
                    continue;
                }

                var nextA = Mathf.Lerp(currentA, targetA, lerpFactor);
                floorMask.DisplayImage.SetPixel(px, py, new Color(1f, 1f, 1f, nextA));
                stillDirty = true;
                if (px < nextMinX) nextMinX = px;
                if (py < nextMinY) nextMinY = py;
                if (px > nextMaxX) nextMaxX = px;
                if (py > nextMaxY) nextMaxY = py;
            }
        }

        floorMask.DisplayTexture.Update(floorMask.DisplayImage);
        if (stillDirty)
        {
            floorMask.SetDirtyRect(nextMinX, nextMinY, nextMaxX, nextMaxY);
        }
        else
        {
            floorMask.ClearDirtyRect();
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_gpuEnabled || _material is null || _whiteTexture is null || !_masksByFloorZ.ContainsKey(_activeFloorZ))
        {
            return;
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawTextureRect(_whiteTexture, _boardRect, false, Colors.White);
    }

    private void EnsureMaterial()
    {
        if (_material is not null)
        {
            Material = _material;
            return;
        }

        var shader = GD.Load<Shader>("res://FogMaskOverlay.gdshader");
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("edge_width_cells", 1.0f);
        _material.SetShaderParameter("edge_softness", 1.6f);
        _material.SetShaderParameter("blur_taps", 6);
        _material.SetShaderParameter("fog_color", new Color(0.04f, 0.05f, 0.08f, 0.78f));
        EnsureDefaultMaskTexture();
        _material.SetShaderParameter("fog_mask", _defaultMaskTexture!);
        _material.SetShaderParameter("sliding_fog_mask", false);
        _material.SetShaderParameter("board_cells", Vector2.One);
        _material.SetShaderParameter("floor_min_xy", Vector2.Zero);
        _material.SetShaderParameter("mask_origin_global", Vector2.Zero);
        _material.SetShaderParameter("mask_cells", Vector2.One);
        _material.SetShaderParameter("mask_pixels_per_cell", 8f);
        Material = _material;
    }

    private void EnsureWhiteTexture()
    {
        if (_whiteTexture is not null)
        {
            return;
        }

        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _whiteTexture = ImageTexture.CreateFromImage(image);
    }

    private void EnsureDefaultMaskTexture()
    {
        if (_defaultMaskTexture is not null)
        {
            return;
        }

        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.Fill(new Color(0f, 0f, 0f, 0f));
        _defaultMaskTexture = ImageTexture.CreateFromImage(image);
    }
}
