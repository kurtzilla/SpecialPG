namespace SpecialPG.Core.Maps;

/// <summary>
/// Logical bundle for persisting a map together with the generation settings used to create or last edit it.
/// Serialize with <see cref="MapSaveEnvelopeJson"/>.
/// </summary>
public sealed class MapSaveEnvelope
{
    /// <summary>Bump when the envelope shape changes.</summary>
    public int EnvelopeSchemaVersion { get; set; } = 1;

    /// <summary>Generation parameters snapshot.</summary>
    public MapGenerationParametersDto Generation { get; set; } = new();

    /// <summary>
    /// Full <see cref="WorldMapJson"/> payload string for the tile/link graph (may be large).
    /// </summary>
    public string WorldMapJson { get; set; } = "";
}

/// <summary>JSON-friendly DTO for <see cref="MapGenerationParameters"/> (record struct does not round-trip all serializers equally).</summary>
public sealed class MapGenerationParametersDto
{
    public int SchemaVersion { get; set; } = MapGenerationParameters.CurrentSchemaVersion;

    public int Seed { get; set; }

    public int LandPercent { get; set; }

    public int WaterPercent { get; set; }

    public static MapGenerationParametersDto FromParameters(MapGenerationParameters p) =>
        new()
        {
            SchemaVersion = p.SchemaVersion,
            Seed = p.Seed,
            LandPercent = p.LandPercent,
            WaterPercent = p.WaterPercent,
        };

    public MapGenerationParameters ToParameters()
    {
        var sv = SchemaVersion > 0 ? SchemaVersion : MapGenerationParameters.CurrentSchemaVersion;
        return new MapGenerationParameters(sv, Seed, LandPercent, WaterPercent);
    }
}
