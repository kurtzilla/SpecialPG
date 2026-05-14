using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TerrainVisualColorTests
{
    [Fact]
    public void ForceLand_blends_coast_when_noise_sample_would_be_water()
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
        Assert.True(rgb.G > rgb.B * 0.9f, "Expected land-green dominance with softened coast blend.");
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

    [Fact]
    public void Synthetic_water_with_blocked_flag_uses_water_tint_not_blocked_green()
    {
        var cfg = TerrainNoiseConfig.Default(3) with { WaterElevationThreshold = 1.01f };
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticWater();

        var rgb = TerrainVisualColor.AtWorld(0f, 0f, tile, eval, cfg);
        Assert.True(rgb.B > rgb.R && rgb.B > rgb.G,
            "Water tiles use TileFlags.Blocked for movement; visuals must still read as water (blue), not Blocked.");
    }
}
