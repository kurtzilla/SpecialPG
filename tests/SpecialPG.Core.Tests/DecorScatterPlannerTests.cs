using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class DecorScatterPlannerTests
{
    private static void FillLand(FloorSlice floor, int gx0, int gy0, int w, int h)
    {
        var land = TileCell.SyntheticLand();
        for (var y = gy0; y < gy0 + h; y++)
        {
            for (var x = gx0; x < gx0 + w; x++)
                floor.Set(x, y, land);
        }
    }

    [Fact]
    public void PlanChunk_is_deterministic()
    {
        var cfg = TerrainNoiseConfig.Default(40);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 32, 32, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillLand(floor, 0, 0, 32, 32);

        var a = new List<DecorCell>();
        var b = new List<DecorCell>();
        DecorScatterPlanner.PlanChunk(floor, 0, 0, eval, cfg, worldSeed: 99, a);
        DecorScatterPlanner.PlanChunk(floor, 0, 0, eval, cfg, worldSeed: 99, b);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i], b[i]);
        }
    }

    [Fact]
    public void PlanChunk_skips_water_tiles()
    {
        var cfg = TerrainNoiseConfig.Default(41) with { WaterElevationThreshold = 1.01f };
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 8, 8, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillLand(floor, 0, 0, 8, 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
                floor.Set(x, y, TileCell.SyntheticWater());
        }

        var ops = new List<DecorCell>();
        DecorScatterPlanner.PlanChunk(floor, 0, 0, eval, cfg, 7, ops);
        Assert.Empty(ops);
    }

    [Fact]
    public void PlanChunk_bounded_count_on_full_land_chunk()
    {
        var cfg = TerrainNoiseConfig.Default(42);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 32, 32, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillLand(floor, 0, 0, 32, 32);

        var ops = new List<DecorCell>();
        DecorScatterPlanner.PlanChunk(floor, 0, 0, eval, cfg, 123, ops);

        Assert.InRange(ops.Count, 1, 32 * 32 / 10);
        Assert.All(ops, c => Assert.InRange(c.VariantIndex, 0, DecorScatterPlanner.VariantCount - 1));
    }
}
