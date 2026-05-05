using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TerrainVisualColorTests
{
    [Fact]
    public void ForceLand_skips_evaluator_water()
    {
        var cfg = TerrainNoiseConfig.Default(1);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell
        {
            Override = TerrainOverride.ForceLand,
            ElevationBucket = 1,
            MoistureBucket = 0,
        };

        var rgb = TerrainVisualColor.AtWorld(0f, 0f, tile, eval, cfg);
        Assert.True(rgb.G > rgb.B * 0.9f, "Expected land-green dominance.");
    }

    [Fact]
    public void ForceWater_is_blue_regardless_of_eval()
    {
        var cfg = TerrainNoiseConfig.Default(2);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell
        {
            Override = TerrainOverride.ForceWater,
            ElevationBucket = 200,
            MoistureBucket = 0,
        };

        var rgb = TerrainVisualColor.AtWorld(100f, 100f, tile, eval, cfg);
        Assert.True(rgb.B > rgb.R && rgb.B > rgb.G);
    }
}
