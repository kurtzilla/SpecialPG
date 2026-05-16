using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TerrainAppearanceTests
{
    [Fact]
    public void ForceLand_on_noise_water_is_coast_blend_category()
    {
        var cfg = TerrainNoiseConfig.Default(1);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell { Override = TerrainOverride.ForceLand, ElevationBucket = 1 };
        Assert.True(TryFindNoisePoint(eval, cfg, water: true, out var wx, out var wy));

        var cat = TerrainAppearance.Resolve(wx, wy, tile, eval, cfg);
        Assert.Equal(TerrainRenderCategory.ForcedLandCoastBlend, cat);
    }

    [Fact]
    public void ForceLand_on_noise_land_is_override_category()
    {
        var cfg = TerrainNoiseConfig.Default(2);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell
        {
            Override = TerrainOverride.ForceLand,
            ElevationBucket = 200,
            MoistureBucket = 128,
        };
        Assert.True(TryFindNoisePoint(eval, cfg, water: false, out var wx, out var wy));

        var cat = TerrainAppearance.Resolve(wx, wy, tile, eval, cfg);
        Assert.Equal(TerrainRenderCategory.ForcedLandOverride, cat);
    }

    private static bool TryFindNoisePoint(
        TerrainEvaluator eval,
        in TerrainNoiseConfig cfg,
        bool water,
        out float wx,
        out float wy)
    {
        for (var i = 0; i < 128; i++)
        {
            for (var j = 0; j < 128; j++)
            {
                wx = i + 0.5f;
                wy = j + 0.5f;
                var s = eval.EvaluateAt(wx, wy);
                if (eval.IsWater(s) == water)
                    return true;
            }
        }

        wx = wy = 0f;
        return false;
    }

    [Fact]
    public void ForceWater_is_forced_water_category()
    {
        var cfg = TerrainNoiseConfig.Default(3);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell { Override = TerrainOverride.ForceWater, ElevationBucket = 200 };

        Assert.Equal(
            TerrainRenderCategory.ForcedWater,
            TerrainAppearance.Resolve(0f, 0f, tile, eval, cfg));
    }

    [Fact]
    public void Blocked_non_water_is_blocked_category()
    {
        var cfg = TerrainNoiseConfig.Default(4);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell
        {
            ElevationBucket = 200,
            MoistureBucket = 128,
            Flags = TileFlags.Blocked,
        };

        Assert.Equal(
            TerrainRenderCategory.Blocked,
            TerrainAppearance.Resolve(0f, 0f, tile, eval, cfg));
    }

    [Fact]
    public void Synthetic_water_is_shallow_or_deep_not_blocked()
    {
        var cfg = TerrainNoiseConfig.Default(5) with { WaterElevationThreshold = 1.01f };
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticWater();

        var cat = TerrainAppearance.Resolve(0f, 0f, tile, eval, cfg);
        Assert.NotEqual(TerrainRenderCategory.Blocked, cat);
        Assert.True(
            cat is TerrainRenderCategory.ShallowWater or TerrainRenderCategory.DeepWater,
            $"Water surface tile should classify as water, got {cat}.");
    }

    [Fact]
    public void Empty_cell_uses_noise_land_or_water()
    {
        var cfg = TerrainNoiseConfig.Default(6);
        var eval = new TerrainEvaluator(cfg);
        var tile = default(TileCell);

        var cat = TerrainAppearance.Resolve(0f, 0f, tile, eval, cfg);
        Assert.NotEqual(TerrainRenderCategory.Empty, cat);
    }

    [Fact]
    public void DescribeCategory_matches_appearance_for_force_land()
    {
        var cfg = TerrainNoiseConfig.Default(7);
        var eval = new TerrainEvaluator(cfg);
        var tile = new TileCell { Override = TerrainOverride.ForceLand, ElevationBucket = 1 };

        var cat = TerrainAppearance.Resolve(0f, 0f, tile, eval, cfg);
        var label = TerrainVisualColor.DescribeCategoryAtTileCenter(0f, 0f, tile, eval, cfg);
        if (cat == TerrainRenderCategory.ForcedLandCoastBlend)
            Assert.Equal("Forced land (coast blend)", label);
        else
            Assert.Equal("Forced land (override)", label);
    }
}
