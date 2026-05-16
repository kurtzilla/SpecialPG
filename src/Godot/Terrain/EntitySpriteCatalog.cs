#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Maps <see cref="EntityRecord.Kind"/> to regions in <c>res://art/entities/entity_atlas.png</c>.</summary>
public sealed class EntitySpriteCatalog
{
    public const string AtlasResourcePath = "res://art/entities/entity_atlas.png";
    public const int TilePixelSize = 32;

    private readonly Dictionary<ushort, Rect2I> _regions = new();
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
            GD.PushWarning($"[EntitySpriteCatalog] Missing {AtlasResourcePath}; entity sprites use fallback colors.");
            return false;
        }

        _atlasImage = image;
        _atlasTexture = ImageTexture.CreateFromImage(_atlasImage);
        _regions[EntityKinds.Actor] = new Rect2I(0, 0, TilePixelSize, TilePixelSize);
        _regions[EntityKinds.Prop] = new Rect2I(TilePixelSize, 0, TilePixelSize, TilePixelSize);

        _loadDiagnostics = $"ok {_atlasImage.GetWidth()}x{_atlasImage.GetHeight()}";
        return true;
    }

    public bool TryGetPixelRect(ushort kind, out Rect2I rect)
    {
        if (kind == EntityKinds.None)
        {
            rect = default;
            return false;
        }

        if (_regions.TryGetValue(kind, out rect))
            return true;

        rect = _regions.GetValueOrDefault(EntityKinds.Prop);
        return _regions.ContainsKey(EntityKinds.Prop);
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
            return false;

        var loaded = Image.LoadFromFile(path);
        if (loaded is null || loaded.IsEmpty())
            return false;

        image = loaded;
        return true;
    }
}
