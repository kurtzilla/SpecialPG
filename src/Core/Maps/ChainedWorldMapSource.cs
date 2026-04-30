namespace SpecialPG.Core.Maps;

/// <summary>
/// Tries each <see cref="IWorldMapSource"/> in order until one returns a non-null map (e.g. JSON then procedural fallback).
/// </summary>
public sealed class ChainedWorldMapSource : IWorldMapSource
{
    private readonly IReadOnlyList<IWorldMapSource> _sources;

    public ChainedWorldMapSource(params IWorldMapSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Length == 0)
            throw new ArgumentException("At least one map source is required.", nameof(sources));
        _sources = sources;
    }

    public ChainedWorldMapSource(IEnumerable<IWorldMapSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources as IReadOnlyList<IWorldMapSource> ?? sources.ToList();
        if (_sources.Count == 0)
            throw new ArgumentException("At least one map source is required.", nameof(sources));
    }

    public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
    {
        foreach (var src in _sources)
        {
            var map = src.TryBuildWorldMap(out sourceSummary, out errorDetail);
            if (map is not null)
                return map;
        }

        sourceSummary = "No map source succeeded.";
        errorDetail = "All chained map sources returned null.";
        return null;
    }
}
