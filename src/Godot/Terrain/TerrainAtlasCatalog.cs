#nullable enable

using System.Collections.Generic;

using System.Diagnostics.CodeAnalysis;

using System.IO;

using Godot;

using SpecialPG.Core.Maps.Rendering;



namespace SpecialPG;



/// <summary>

/// Maps <see cref="TileSpriteKey"/> to pixel regions in <c>res://art/terrain/terrain_atlas.png</c>.

/// Call <see cref="TryLoad"/> once before CPU bake blits; use <see cref="TryGetAtlasImage"/> for <see cref="Image.BlitRect"/>.

/// </summary>

public sealed class TerrainAtlasCatalog

{

    public const string AtlasResourcePath = "res://art/terrain/terrain_atlas.png";

    public const int TilePixelSize = 32;

    public const int VariantsPerCategory = 4;



    /// <summary>Per-category band: 1×1 + 2×2 + 4×4 + Side transition strip (pixels).</summary>

    public const int CategoryBandHeight = 256;

    public const int TransitionStripHeight = 32;

    private const int Strip1x1Height = 32;

    private const int Strip2x2Height = 64;

    private const int Strip4x4Height = 128;



    private readonly Dictionary<TileSpriteKey, Rect2I> _regions = new();

    private Texture2D? _atlasTexture;

    private Image? _atlasImage;

    private string? _loadDiagnostics;



    public Texture2D? AtlasTexture => _atlasTexture;



    public bool IsLoaded => _atlasImage is not null && !_atlasImage.IsEmpty();



    /// <summary>Human-readable result of the last <see cref="TryLoad"/> call (for HUD / logs).</summary>

    public string LoadDiagnostics => _loadDiagnostics ?? "not loaded";



    /// <summary>Loads atlas for CPU baking. Prefers a direct PNG read (reliable for large atlases); texture is optional.</summary>

    public bool TryLoad()

    {

        _regions.Clear();

        _atlasTexture = null;

        _atlasImage = null;

        _loadDiagnostics = "missing file";



        if (!TryLoadCpuImageFromDisk(out var diskImage))

        {

            if (!ResourceLoader.Exists(AtlasResourcePath))

            {

                GD.PushWarning($"[TerrainAtlasCatalog] Missing {AtlasResourcePath}; terrain sprites disabled.");

                return false;

            }



            var tex = ResourceLoader.Load<Texture2D>(AtlasResourcePath);

            if (tex is null)

            {

                _loadDiagnostics = "texture load failed";

                GD.PushWarning($"[TerrainAtlasCatalog] Failed to load {AtlasResourcePath}; terrain sprites disabled.");

                return false;

            }



            _atlasTexture = tex;

            diskImage = tex.GetImage();

            if (diskImage is null || diskImage.IsEmpty())

            {

                _atlasTexture = null;

                _loadDiagnostics = "texture GetImage() empty";

                GD.PushWarning(

                    $"[TerrainAtlasCatalog] Atlas image empty for {AtlasResourcePath} (try reimport as lossless / VRAM uncompressed); terrain sprites disabled.");

                return false;

            }

        }

        else if (ResourceLoader.Exists(AtlasResourcePath))

        {

            _atlasTexture = ResourceLoader.Load<Texture2D>(AtlasResourcePath);

        }



        _atlasImage = diskImage;

        BuildRegionTable();

        _loadDiagnostics = $"ok {_atlasImage.GetWidth()}x{_atlasImage.GetHeight()}";

        return true;

    }



    public bool TryGetPixelRect(TileSpriteKey key, out Rect2I rect) =>

        _regions.TryGetValue(key, out rect);

    public bool TryGetSidePixelRect(
        TerrainRenderCategory category,
        TransitionFacing facing,
        int variant,
        out Rect2I rect)
    {
        var v = variant % VariantsPerCategory;
        var bandY = CategoryRow(category) * CategoryBandHeight;
        var y = bandY + Strip1x1Height + Strip2x2Height + Strip4x4Height;
        var x = (int)facing * VariantsPerCategory * TilePixelSize + v * TilePixelSize;
        rect = new Rect2I(x, y, TilePixelSize, TransitionStripHeight);
        return _atlasImage is not null && x + TilePixelSize <= _atlasImage.GetWidth() && y + TransitionStripHeight <= _atlasImage.GetHeight();
    }



    public bool TryGetAtlasImage([NotNullWhen(true)] out Image? image)

    {

        image = _atlasImage;

        return image is not null && !image.IsEmpty();

    }



    private static bool TryLoadCpuImageFromDisk([NotNullWhen(true)] out Image? image)

    {

        image = null;

        var path = ProjectSettings.GlobalizePath(AtlasResourcePath);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))

        {

            return false;

        }



        var loaded = Image.LoadFromFile(path);

        if (loaded is null || loaded.IsEmpty())

        {

            return false;

        }



        image = loaded;

        return true;

    }



    private void BuildRegionTable()

    {

        foreach (TerrainRenderCategory category in System.Enum.GetValues<TerrainRenderCategory>())

        {

            var row = CategoryRow(category);

            var bandY = row * CategoryBandHeight;

            for (var v = 0; v < VariantsPerCategory; v++)

            {

                _regions[new TileSpriteKey(category, TileSpriteRole.Main1x1, v)] =

                    new Rect2I(v * TilePixelSize, bandY, TilePixelSize, TilePixelSize);

                _regions[new TileSpriteKey(category, TileSpriteRole.Main2x2, v)] =

                    new Rect2I(v * 64, bandY + Strip1x1Height, 64, Strip2x2Height);

                _regions[new TileSpriteKey(category, TileSpriteRole.Main4x4, v)] =

                    new Rect2I(v * 128, bandY + Strip1x1Height + Strip2x2Height, 128, 128);

                for (var f = 0; f < 4; f++)
                {
                    _regions[new TileSpriteKey(category, TileSpriteRole.Side, v * 4 + f)] =
                        new Rect2I(
                            f * VariantsPerCategory * TilePixelSize + v * TilePixelSize,
                            bandY + Strip1x1Height + Strip2x2Height + Strip4x4Height,
                            TilePixelSize,
                            TransitionStripHeight);
                }
            }

        }

    }



    private static int CategoryRow(TerrainRenderCategory category) =>

        category switch

        {

            TerrainRenderCategory.DeepWater => 0,

            TerrainRenderCategory.ShallowWater => 1,

            TerrainRenderCategory.Coast => 2,

            TerrainRenderCategory.Land => 3,

            TerrainRenderCategory.Hill => 4,

            TerrainRenderCategory.Blocked => 5,

            TerrainRenderCategory.ForcedLandCoastBlend => 6,

            TerrainRenderCategory.ForcedLandOverride => 7,

            TerrainRenderCategory.ForcedWater => 8,

            TerrainRenderCategory.Empty => 9,

            _ => 9,

        };

}

