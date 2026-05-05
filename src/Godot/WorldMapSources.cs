#nullable enable
using System;
using Godot;
using SpecialPG.Core.Maps;
using CoreTileCell = SpecialPG.Core.Maps.TileCell;

namespace SpecialPG;

/// <summary>Loads <see cref="WorldMap"/> from a JSON file path (Godot <see cref="FileAccess"/>).</summary>
public sealed class JsonWorldMapSource : IWorldMapSource
{
    private readonly string _resourcePath;

    public JsonWorldMapSource(string resourcePath) => _resourcePath = resourcePath;

    public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
    {
        sourceSummary = $"JSON — {_resourcePath}";
        errorDetail = null;

        if (!FileAccess.FileExists(_resourcePath))
        {
            errorDetail = "File not found.";
            return null;
        }

        using var file = FileAccess.Open(_resourcePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            errorDetail = "Could not open file.";
            return null;
        }

        var text = file.GetAsText();
        if (!WorldMapJson.TryDeserialize(text, out var map, out var err) || map is null)
        {
            errorDetail = err;
            GD.PrintErr($"[JsonWorldMapSource] Deserialize failed: {err}");
            return null;
        }

        if (map.PresentFloorIndices().Count == 0)
        {
            errorDetail = "Map had no floors.";
            GD.PrintErr("[JsonWorldMapSource] Map JSON had no floors.");
            return null;
        }

        var integrity = MapIntegrity.Validate(map);
        if (integrity.HasErrors)
        {
            errorDetail = "Integrity validation failed.";
            GD.PrintErr("[JsonWorldMapSource] Map JSON failed integrity checks.");
            foreach (var issue in integrity.Issues)
            {
                if (issue.Severity == MapIntegritySeverity.Error)
                    GD.PrintErr($"[JsonWorldMapSource] {issue.Message}");
            }

            return null;
        }

        return map;
    }
}

/// <summary>Deterministic placeholder map when no JSON (or future procedural pipeline) provides one.</summary>
public sealed class FallbackSampleWorldMapSource : IWorldMapSource
{
    private readonly ShellAppConfig _shell;

    public FallbackSampleWorldMapSource(ShellAppConfig shell) =>
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));

    public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
    {
        sourceSummary = "Built-in fallback (no prior map source succeeded)";
        errorDetail = null;
        return SampleWorldMapBootstrap.CreateFallbackMap(_shell);
    }
}

/// <summary>Shared helpers for shell-built sample maps (checkerboard fill used by <see cref="GameRoot"/>).</summary>
public static class SampleWorldMapBootstrap
{
    public static WorldMap CreateFallbackMap(ShellAppConfig shell)
    {
        var map = new WorldMap(shell.DefaultMapWidthCells, shell.DefaultMapHeightCells, shell.ChunkWidthCells,
            shell.ChunkHeightCells);
        FillCheckerboard(map.GetOrCreateFloor(0), 0);
        FillCheckerboard(map.GetOrCreateFloor(1), 1);
        var bx = map.MinX + map.Width / 2;
        var by = map.MinY + map.Height / 2;
        map.GetOrCreateFloor(0).Set(bx, by, CoreTileCell.SyntheticLand() with { Flags = TileFlags.Blocked });
        map.AddVerticalLink(new VerticalLink
        {
            FromX = map.MinX,
            FromY = map.MinY,
            FromZ = 0,
            ToX = map.MinX,
            ToY = map.MinY,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });
        return map;
    }

    public static void FillCheckerboard(FloorSlice floor, int zBias)
    {
        for (var ly = 0; ly < floor.Height; ly++)
        {
            for (var lx = 0; lx < floor.Width; lx++)
            {
                var gx = floor.MinX + lx;
                var gy = floor.MinY + ly;
                var elev = (byte)(130 + (((lx + ly + zBias) % 2) * 40));
                floor.Set(gx, gy, new CoreTileCell
                {
                    ElevationBucket = elev,
                    MoistureBucket = 128,
                    Override = TerrainOverride.None,
                    Flags = 0,
                    Variant = (byte)(((lx + ly + zBias) % 2) * 4),
                });
            }
        }
    }
}
