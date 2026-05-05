using System.Diagnostics.CodeAnalysis;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Logical bundle for persisting a map together with the generation settings used to create or last edit it.
/// Serialize with <see cref="MapSaveEnvelopeJson"/>.
/// </summary>
public sealed class MapSaveEnvelope
{
    /// <summary>Bump when the envelope shape changes. Version 2 adds <see cref="EntitiesJson"/>.</summary>
    public int EnvelopeSchemaVersion { get; set; } = 2;

    /// <summary>Generation parameters snapshot.</summary>
    public MapGenerationParametersDto Generation { get; set; } = new();

    /// <summary>
    /// Full <see cref="WorldMapJson"/> payload string for the tile/link graph (may be large).
    /// </summary>
    public string WorldMapJson { get; set; } = "";

    /// <summary>
    /// <see cref="EntityStoreJson"/> payload for <see cref="EntityStore"/> (separate from tiles). Omitted or empty in v1 envelopes.
    /// </summary>
    public string EntitiesJson { get; set; } = "";

    /// <summary>
    /// Packs a bounded world for persistence (throws if <see cref="WorldMapJson.Serialize"/> cannot run).
    /// </summary>
    public static MapSaveEnvelope FromBoundedWorld(WorldMap map, MapGenerationParameters generation,
        EntityStore entities)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        return new MapSaveEnvelope
        {
            EnvelopeSchemaVersion = 2,
            Generation = MapGenerationParametersDto.FromParameters(generation),
            WorldMapJson = global::SpecialPG.Core.Maps.WorldMapJson.Serialize(map),
            EntitiesJson = EntityStoreJson.Serialize(entities),
        };
    }

    /// <summary>
    /// Hydrates map + entity store into a new <see cref="WorldState"/> (fog is fresh; caller may restore actor pose separately).
    /// </summary>
    public static bool TryCreateWorldState(MapSaveEnvelope envelope, int actorX, int actorY, int actorZ,
        [NotNullWhen(true)] out WorldState? world, [NotNullWhen(true)] out MapGenerationParameters? generation,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        world = null;
        generation = null;
        error = null;

        if (!global::SpecialPG.Core.Maps.WorldMapJson.TryDeserialize(envelope.WorldMapJson, out var map, out var mapErr) ||
            map is null)
        {
            error = mapErr ?? "World map deserialize failed.";
            return false;
        }

        generation = envelope.Generation.ToParameters();
        world = new WorldState(map, actorX, actorY, actorZ);
        var entitiesPayload = envelope.EntitiesJson ?? "";
        if (!EntityStoreJson.TryDeserializeInto(world.Entities, entitiesPayload, out var entErr))
        {
            error = entErr;
            world = null;
            generation = null;
            return false;
        }

        return true;
    }
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
