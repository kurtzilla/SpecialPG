namespace SpecialPG.Core.Maps;

/// <summary>
/// Supplies a <see cref="WorldMap"/> for a session (JSON, procedural generation, tests, etc.).
/// Shell composition chooses which implementations run and in what order (see <see cref="ChainedWorldMapSource"/>).
/// </summary>
public interface IWorldMapSource
{
    /// <summary>
    /// Returns a map when this source succeeds; otherwise null.
    /// <paramref name="sourceSummary"/> is a short HUD/log label (e.g. file path or "Procedural seed 42").
    /// <paramref name="errorDetail"/> is optional diagnostic text when the source ran but rejected the map.
    /// </summary>
    WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail);
}
