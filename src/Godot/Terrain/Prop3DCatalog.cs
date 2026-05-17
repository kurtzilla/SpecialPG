#nullable enable

using System.Collections.Generic;
using Godot;

namespace SpecialPG;

/// <summary>Loads shipped Kenney 3D prop entries from <c>res://art/3d/manifest.json</c>.</summary>
public sealed class Prop3DCatalog
{
    public const string ManifestResourcePath = "res://art/3d/manifest.json";

    private readonly Dictionary<int, Prop3DEntry> _byDecorVariant = new();
    private readonly Dictionary<string, Prop3DEntry> _byId = new();
    private string _loadDiagnostics = "not loaded";

    public bool IsLoaded { get; private set; }

    public string LoadDiagnostics => _loadDiagnostics;

    public bool TryLoad()
    {
        _byDecorVariant.Clear();
        _byId.Clear();
        IsLoaded = false;
        _loadDiagnostics = "missing manifest";

        if (!FileAccess.FileExists(ManifestResourcePath))
        {
            return false;
        }

        using var file = FileAccess.Open(ManifestResourcePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            _loadDiagnostics = "manifest open failed";
            return false;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            _loadDiagnostics = "manifest not a dict";
            return false;
        }

        var root = parsed.AsGodotDictionary();
        if (!root.TryGetValue("props", out var propsVar) || propsVar.VariantType != Variant.Type.Array)
        {
            _loadDiagnostics = "manifest missing props";
            return false;
        }

        var count = 0;
        foreach (var item in propsVar.AsGodotArray())
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var d = item.AsGodotDictionary();
            var entry = new Prop3DEntry
            {
                Id = d.GetValueOrDefault("id", "").AsString(),
                ResourcePath = d.GetValueOrDefault("resource_path", "").AsString(),
                Scale = d.GetValueOrDefault("scale", 1f).AsSingle(),
                YOffset = d.GetValueOrDefault("y_offset", 0f).AsSingle(),
            };
            if (d.TryGetValue("decor_variant", out var dv))
                entry.DecorVariant = dv.AsInt32();
            if (string.IsNullOrEmpty(entry.ResourcePath))
                continue;
            if (!string.IsNullOrEmpty(entry.Id))
                _byId[entry.Id] = entry;
            if (entry.DecorVariant is int variant && !string.IsNullOrEmpty(entry.Id))
                _byDecorVariant[variant] = entry;
            count++;
        }

        if (count == 0)
        {
            _loadDiagnostics = "manifest empty";
            return false;
        }

        IsLoaded = true;
        _loadDiagnostics = $"ok {count} props";
        return true;
    }

    public bool TryGetForDecorVariant(int variantIndex, out Prop3DEntry entry)
    {
        entry = null!;
        return _byDecorVariant.TryGetValue(variantIndex, out entry);
    }

    public sealed class Prop3DEntry
    {
        public string Id { get; set; } = "";

        public string ResourcePath { get; set; } = "";

        public float Scale { get; set; } = 1f;

        public float YOffset { get; set; }

        public int? DecorVariant { get; set; }
    }
}
