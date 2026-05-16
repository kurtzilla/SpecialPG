#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Godot;

namespace SpecialPG;

/// <summary>Maps decor variant indices to regions in <c>res://art/decor/decor_atlas.png</c>.</summary>
public sealed class DecorAtlasCatalog
{
    public const string AtlasResourcePath = "res://art/decor/decor_atlas.png";
    public const int TilePixelSize = 32;
    public const int VariantCount = 8;

    private readonly Dictionary<int, Rect2I> _regions = new();
    private Image? _atlasImage;
    private Texture2D? _atlasTexture;
    private string? _loadDiagnostics;

    public Texture2D? AtlasTexture => _atlasTexture;

    public bool IsLoaded => _atlasImage is not null && !_atlasImage.IsEmpty();

    public string LoadDiagnostics => _loadDiagnostics ?? "not loaded";

    public bool TryLoad()
    {
        _regions.Clear();
        _atlasImage = null;
        _loadDiagnostics = "missing file";

        if (!TryLoadCpuImageFromDisk(out var image))
        {
            GD.PushWarning($"[DecorAtlasCatalog] Missing {AtlasResourcePath}; decor sprites disabled.");
            return false;
        }

        _atlasImage = image;
        _atlasTexture = ImageTexture.CreateFromImage(_atlasImage);
        for (var v = 0; v < VariantCount; v++)
            _regions[v] = new Rect2I(v * TilePixelSize, 0, TilePixelSize, TilePixelSize);

        _loadDiagnostics = $"ok {_atlasImage.GetWidth()}x{_atlasImage.GetHeight()}";
        return true;
    }

    public bool TryGetPixelRect(int variantIndex, out Rect2I rect) =>
        _regions.TryGetValue(variantIndex % VariantCount, out rect);

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
            return false;

        var loaded = Image.LoadFromFile(path);
        if (loaded is null || loaded.IsEmpty())
            return false;

        image = loaded;
        return true;
    }
}
