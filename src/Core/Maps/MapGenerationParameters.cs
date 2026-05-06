namespace SpecialPG.Core.Maps;

/// <summary>
/// Versioned inputs for procedural generation and round-trip with saved maps / map workbench UI.
/// <see cref="LandPercent"/> / <see cref="WaterPercent"/> target global cell fractions (~); optional 2×2 water blob cleanup can reduce visible water.
/// </summary>
public readonly record struct MapGenerationParameters(
    int SchemaVersion,
    int Seed,
    int LandPercent,
    int WaterPercent)
{
    /// <summary>Bump when adding/removing/repurposing fields (serialization compatibility).</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Land + water must equal 100.</summary>
    public bool IsValid =>
        LandPercent is >= 0 and <= 100 &&
        WaterPercent is >= 0 and <= 100 &&
        LandPercent + WaterPercent == 100;

    public static MapGenerationParameters Create(int seed, int landPercent)
    {
        var lp = Math.Clamp(landPercent, 0, 100);
        return new MapGenerationParameters(CurrentSchemaVersion, seed, lp, 100 - lp);
    }

    /// <summary>Default proc profile when only a seed is supplied (legacy callers).</summary>
    public static MapGenerationParameters FromSeedOnly(int seed) => Create(seed, landPercent: 55);
}
