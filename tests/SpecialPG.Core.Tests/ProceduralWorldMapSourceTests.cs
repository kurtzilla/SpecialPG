using System;
using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class ProceduralWorldMapSourceTests
{
    [Fact]
    public void TryBuild_passes_MapIntegrity()
    {
        var src = new ProceduralWorldMapSource(64, 48, 32, 32, seed: 42_4242);
        var map = src.TryBuildWorldMap(out _, out var err);

        Assert.Null(err);
        Assert.NotNull(map);

        var integrity = MapIntegrity.Validate(map!);
        Assert.False(integrity.HasErrors, string.Join("; ", integrity.Issues.ConvertAll(i => i.Message)));
    }

    [Fact]
    public void Same_seed_produces_identical_tiles()
    {
        const int seed = 9_001;
        var a = ProceduralWorldMapGenerator.BuildBoundedWorld(16, 16, 8, 8, seed);
        var b = ProceduralWorldMapGenerator.BuildBoundedWorld(16, 16, 8, 8, seed);

        var f0a = a.GetOrCreateFloor(0);
        var f0b = b.GetOrCreateFloor(0);

        for (var y = f0a.MinY; y < f0a.MinY + f0a.Height; y++)
        {
            for (var x = f0a.MinX; x < f0a.MinX + f0a.Width; x++)
            {
                Assert.Equal(f0a.Get(x, y).TileKind, f0b.Get(x, y).TileKind);
                Assert.Equal(f0a.Get(x, y).Flags, f0b.Get(x, y).Flags);
            }
        }

        Assert.Equal(a.VerticalLinks.Count, b.VerticalLinks.Count);
    }

    [Fact]
    public void Different_seed_changes_content()
    {
        var a = ProceduralWorldMapGenerator.BuildBoundedWorld(24, 24, 12, 12, seed: 1);
        var b = ProceduralWorldMapGenerator.BuildBoundedWorld(24, 24, 12, 12, seed: 2);

        var f0a = a.GetOrCreateFloor(0);
        var f0b = b.GetOrCreateFloor(0);

        var anyDiff = false;
        for (var y = f0a.MinY; y < f0a.MinY + f0a.Height && !anyDiff; y++)
        {
            for (var x = f0a.MinX; x < f0a.MinX + f0a.Width && !anyDiff; x++)
            {
                if (f0a.Get(x, y).TileKind != f0b.Get(x, y).TileKind ||
                    f0a.Get(x, y).Flags != f0b.Get(x, y).Flags)
                    anyDiff = true;
            }
        }

        Assert.True(anyDiff);
    }

    [Fact]
    public void ProceduralWorldMapSource_summary_mentions_seed()
    {
        var src = new ProceduralWorldMapSource(8, 8, 4, 4, seed: 777);
        src.TryBuildWorldMap(out var summary, out _);

        Assert.Contains("777", summary);
        Assert.Contains("seed", summary, StringComparison.OrdinalIgnoreCase);
    }
}
