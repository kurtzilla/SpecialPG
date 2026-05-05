namespace SpecialPG.Core.Maps.Noise;

/// <summary>
/// Tunable noise and classification thresholds for <see cref="TerrainEvaluator"/>.
/// Thresholds apply to normalized elevation in [-1, 1] after fractal summation.
/// </summary>
public readonly record struct TerrainNoiseConfig
{
    /// <summary>World seed; also drives permutation tables for elevation/moisture/temperature layers.</summary>
    public int Seed { get; init; }

    /// <summary>Multiplier applied to world (x, y) before sampling elevation noise (higher = more feature density).</summary>
    public float ElevationNoiseScale { get; init; }

    /// <summary>Multiplier for moisture layer (can differ from elevation for decorrelation).</summary>
    public float MoistureNoiseScale { get; init; }

    /// <summary>Multiplier for temperature layer.</summary>
    public float TemperatureNoiseScale { get; init; }

    public int Octaves { get; init; }
    public float Persistence { get; init; }
    public float Lacunarity { get; init; }

    /// <summary>Elevation strictly below this is treated as water.</summary>
    public float WaterElevationThreshold { get; init; }

    /// <summary>Elevation in [WaterElevationThreshold, CoastElevationThreshold) is coastal.</summary>
    public float CoastElevationThreshold { get; init; }

    /// <summary>Elevation strictly above this is hilly.</summary>
    public float HillElevationThreshold { get; init; }

    /// <summary>Bump when changing threshold semantics or noise composition (save / ruleset compatibility).</summary>
    public int RulesetVersion { get; init; }

    public static TerrainNoiseConfig Default(int seed) => new()
    {
        Seed = seed,
        RulesetVersion = 1,
        ElevationNoiseScale = 0.012f,
        MoistureNoiseScale = 0.018f,
        TemperatureNoiseScale = 0.009f,
        Octaves = 4,
        Persistence = 0.5f,
        Lacunarity = 2f,
        WaterElevationThreshold = -0.15f,
        CoastElevationThreshold = 0.05f,
        HillElevationThreshold = 0.35f
    };
}
