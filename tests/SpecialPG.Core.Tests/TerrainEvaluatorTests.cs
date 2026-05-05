using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TerrainEvaluatorTests
{
    [Fact]
    public void EvaluateAt_IsDeterministic_ForSameSeedAndCoordinates()
    {
        var config = TerrainNoiseConfig.Default(seed: 42_4242);
        var a = new TerrainEvaluator(config);
        var b = new TerrainEvaluator(config);

        var s1 = a.EvaluateAt(12.34f, -56.78f);
        var s2 = b.EvaluateAt(12.34f, -56.78f);

        Assert.Equal(s1.Elevation, s2.Elevation, precision: 6);
        Assert.Equal(s1.Moisture, s2.Moisture, precision: 6);
        Assert.Equal(s1.Temperature, s2.Temperature, precision: 6);
    }

    [Fact]
    public void EvaluateAt_DiffersAcrossSeeds()
    {
        var a = new TerrainEvaluator(TerrainNoiseConfig.Default(seed: 1));
        var b = new TerrainEvaluator(TerrainNoiseConfig.Default(seed: 2));

        var s1 = a.EvaluateAt(100f, 200f);
        var s2 = b.EvaluateAt(100f, 200f);

        Assert.NotEqual(s1.Elevation, s2.Elevation);
    }

    [Fact]
    public void MoistureAndTemperature_AreClampedToUnitInterval()
    {
        var eval = new TerrainEvaluator(TerrainNoiseConfig.Default(seed: 99));
        for (var i = -20; i <= 20; i++)
        {
            var s = eval.EvaluateAt(i * 13.7f, i * -7.3f);
            Assert.InRange(s.Moisture, 0f, 1f);
            Assert.InRange(s.Temperature, 0f, 1f);
        }
    }

    [Fact]
    public void Classification_MatchesThresholds()
    {
        var config = TerrainNoiseConfig.Default(1) with
        {
            WaterElevationThreshold = 0f,
            CoastElevationThreshold = 0.5f,
            HillElevationThreshold = 0.5f
        };
        var eval = new TerrainEvaluator(config);

        Assert.True(eval.IsWater(new TerrainSample { Elevation = -0.01f, Moisture = 0, Temperature = 0 }));
        Assert.False(eval.IsCoastal(new TerrainSample { Elevation = -0.01f, Moisture = 0, Temperature = 0 }));

        Assert.True(eval.IsCoastal(new TerrainSample { Elevation = 0.25f, Moisture = 0, Temperature = 0 }));
        Assert.False(eval.IsWater(new TerrainSample { Elevation = 0.25f, Moisture = 0, Temperature = 0 }));
        Assert.False(eval.IsHilly(new TerrainSample { Elevation = 0.25f, Moisture = 0, Temperature = 0 }));

        Assert.True(eval.IsHilly(new TerrainSample { Elevation = 0.51f, Moisture = 0, Temperature = 0 }));
    }

    [Fact]
    public void GoldenSample_MatchesRecordedValues()
    {
        var eval = new TerrainEvaluator(TerrainNoiseConfig.Default(seed: 12345));
        var s = eval.EvaluateAt(10f, 20f);

        Assert.Equal(-0.184714764f, s.Elevation, precision: 5);
        Assert.Equal(0.52807796f, s.Moisture, precision: 5);
        Assert.Equal(0.349752009f, s.Temperature, precision: 5);
    }
}
