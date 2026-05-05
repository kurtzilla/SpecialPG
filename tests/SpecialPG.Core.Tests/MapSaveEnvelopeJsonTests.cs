using System.Text.Json.Nodes;
using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class MapSaveEnvelopeJsonTests
{
    [Fact]
    public void Roundtrip_preserves_generation_block()
    {
        var p = MapGenerationParameters.Create(42, 60);
        var env = new MapSaveEnvelope
        {
            EnvelopeSchemaVersion = 2,
            Generation = MapGenerationParametersDto.FromParameters(p),
            WorldMapJson = "{}",
            EntitiesJson = "",
        };

        var json = MapSaveEnvelopeJson.Serialize(env);
        Assert.True(MapSaveEnvelopeJson.TryDeserialize(json, out var back, out var err), err);
        Assert.NotNull(back);
        var q = back!.Generation.ToParameters();
        Assert.Equal(p.Seed, q.Seed);
        Assert.Equal(p.LandPercent, q.LandPercent);
        Assert.Equal(p.WaterPercent, q.WaterPercent);
        Assert.Equal("", back.EntitiesJson ?? "");
    }

    [Fact]
    public void FromBoundedWorld_round_trips_entities_and_map()
    {
        var map = new WorldMap(8, 8, 4, 4);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        var gen = MapGenerationParameters.Create(7, 55);
        var entities = new EntityStore(map);
        var id = entities.Spawn(EntityKinds.Prop, 3, 3, 0);

        var env = MapSaveEnvelope.FromBoundedWorld(map, gen, entities);
        var json = MapSaveEnvelopeJson.Serialize(env);
        Assert.True(MapSaveEnvelopeJson.TryDeserialize(json, out var back, out var err), err);

        Assert.True(MapSaveEnvelope.TryCreateWorldState(back!, 0, 0, 0, out var world, out var genBack, out var err2),
            err2);
        Assert.NotNull(world);
        Assert.True(genBack.HasValue);
        Assert.Equal(gen.Seed, genBack!.Value.Seed);
        Assert.True(world!.Entities.TryGet(id, out var r));
        Assert.Equal(3, r.X);
        Assert.Equal(3, r.Y);
    }

    [Fact]
    public void Deserialize_v1_style_json_missing_entities_yields_empty_store()
    {
        var worldInner = WorldMapJson.Serialize(new WorldMap(4, 4, 32, 32));
        var legacy = new JsonObject
        {
            ["envelopeSchemaVersion"] = 1,
            ["generation"] = JsonNode.Parse(
                """{"schemaVersion":1,"seed":1,"landPercent":50,"waterPercent":50}"""),
            ["worldMapJson"] = JsonValue.Create(worldInner),
        }.ToJsonString();

        Assert.True(MapSaveEnvelopeJson.TryDeserialize(legacy, out var env, out var err), err);
        Assert.NotNull(env);
        Assert.True(
            MapSaveEnvelope.TryCreateWorldState(env!, 0, 0, 0, out var world, out _, out var err2),
            err2);
        Assert.Equal(0, world!.Entities.Count);
    }
}
