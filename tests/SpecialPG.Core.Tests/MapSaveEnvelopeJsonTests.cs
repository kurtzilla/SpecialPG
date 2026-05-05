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
            EnvelopeSchemaVersion = 1,
            Generation = MapGenerationParametersDto.FromParameters(p),
            WorldMapJson = "{}",
        };

        var json = MapSaveEnvelopeJson.Serialize(env);
        Assert.True(MapSaveEnvelopeJson.TryDeserialize(json, out var back, out var err), err);
        Assert.NotNull(back);
        var q = back!.Generation.ToParameters();
        Assert.Equal(p.Seed, q.Seed);
        Assert.Equal(p.LandPercent, q.LandPercent);
        Assert.Equal(p.WaterPercent, q.WaterPercent);
    }
}
