using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class ChainedWorldMapSourceTests
{
    private sealed class NullSource : IWorldMapSource
    {
        public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
        {
            sourceSummary = "null";
            errorDetail = "skipped";
            return null;
        }
    }

    private sealed class FixedSource : IWorldMapSource
    {
        private readonly WorldMap _map;

        public FixedSource(WorldMap map) => _map = map;

        public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
        {
            sourceSummary = "fixed";
            errorDetail = null;
            return _map;
        }
    }

    [Fact]
    public void Chained_returns_first_non_null_map()
    {
        var expected = new WorldMap(3, 3);
        var chain = new ChainedWorldMapSource(new NullSource(), new FixedSource(expected));
        var got = chain.TryBuildWorldMap(out var summary, out var err);
        Assert.Same(expected, got);
        Assert.Equal("fixed", summary);
        Assert.Null(err);
    }

    [Fact]
    public void Chained_returns_null_when_all_sources_fail()
    {
        var chain = new ChainedWorldMapSource(new NullSource(), new NullSource());
        var got = chain.TryBuildWorldMap(out var summary, out var err);
        Assert.Null(got);
        Assert.Contains("No map source", summary);
        Assert.NotNull(err);
    }
}
