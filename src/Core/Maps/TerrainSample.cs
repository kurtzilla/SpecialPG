namespace SpecialPG.Core.Maps;

/// <summary>
/// Normalized terrain channels at a point. Use <see cref="TerrainEvaluator"/> for water/coast/hill classification
/// (depends on <see cref="Noise.TerrainNoiseConfig"/> thresholds).
/// </summary>
public readonly struct TerrainSample
{
    /// <summary>Height field in approximately [-1, 1].</summary>
    public float Elevation { get; init; }

    /// <summary>Moisture in [0, 1].</summary>
    public float Moisture { get; init; }

    /// <summary>Temperature in [0, 1].</summary>
    public float Temperature { get; init; }
}
