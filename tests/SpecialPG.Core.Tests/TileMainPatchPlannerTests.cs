using System;
using System.Linq;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TileMainPatchPlannerTests
{
    private static int FindSeedWhereAnchorWantsSize(int gx, int gy, int sizeCells)
    {
        for (var seed = 0; seed < 10_000; seed++)
        {
            var roll = (uint)HashCode.Combine(gx, gy, seed) % 7;
            var wants = sizeCells switch
            {
                4 => roll < 1,
                2 => roll >= 1 && roll < 3,
                _ => roll >= 3,
            };
            if (wants)
                return seed;
        }

        throw new InvalidOperationException($"No seed found for size {sizeCells} at ({gx},{gy}).");
    }

    private static readonly TileCell UniformCategoryCell = TileCell.SyntheticLand() with
    {
        Flags = TileFlags.Blocked,
    };

    private static void FillUniformCategory(FloorSlice floor, int gx0, int gy0, int w, int h)
    {
        for (var y = gy0; y < gy0 + h; y++)
        {
            for (var x = gx0; x < gx0 + w; x++)
                floor.Set(x, y, UniformCategoryCell);
        }
    }

    private static void AssertExactCoverage(List<TileDrawOp> ops, int gx0, int gy0, int lw, int lh)
    {
        var cover = new int[lw * lh];
        foreach (var op in ops)
        {
            for (var dy = 0; dy < op.SizeCells; dy++)
            {
                for (var dx = 0; dx < op.SizeCells; dx++)
                {
                    var gx = op.OriginGx + dx;
                    var gy = op.OriginGy + dy;
                    Assert.InRange(gx, gx0, gx0 + lw - 1);
                    Assert.InRange(gy, gy0, gy0 + lh - 1);
                    cover[(gy - gy0) * lw + (gx - gx0)]++;
                }
            }
        }

        Assert.All(cover, c => Assert.Equal(1, c));
    }

    [Fact]
    public void UniformLand4x4_places_one_4x4()
    {
        var cfg = TerrainNoiseConfig.Default(20);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillUniformCategory(floor, 0, 0, 4, 4);

        var seed = FindSeedWhereAnchorWantsSize(0, 0, 4);
        var ops = new List<TileDrawOp>();
        TileMainPatchPlanner.Plan(floor, 0, 0, 4, 4, eval, cfg, seed, 4, ops);

        var fourByFour = ops.Where(o => o.Key.Role == TileSpriteRole.Main4x4).ToList();
        Assert.Single(fourByFour);
        Assert.Equal(0, fourByFour[0].OriginGx);
        Assert.Equal(0, fourByFour[0].OriginGy);
        Assert.Equal(4, fourByFour[0].SizeCells);
        Assert.DoesNotContain(ops, o => o.Key.Role == TileSpriteRole.Main1x1);
    }

    [Fact]
    public void Ownership_no_double_cover()
    {
        var cfg = TerrainNoiseConfig.Default(21);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 8, 8, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillUniformCategory(floor, 0, 0, 8, 8);

        var ops = new List<TileDrawOp>();
        TileMainPatchPlanner.Plan(floor, 0, 0, 8, 8, eval, cfg, worldSeed: 42, 4, ops);

        AssertExactCoverage(ops, 0, 0, 8, 8);
    }

    [Fact]
    public void Mixed_categories_block_4x4()
    {
        var cfg = TerrainNoiseConfig.Default(22);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillUniformCategory(floor, 0, 0, 4, 4);
        var water = TileCell.SyntheticLand() with { Override = TerrainOverride.ForceWater };
        floor.Set(2, 0, water);
        floor.Set(3, 0, water);
        floor.Set(2, 1, water);
        floor.Set(3, 1, water);

        var seed = FindSeedWhereAnchorWantsSize(0, 0, 4);
        var ops = new List<TileDrawOp>();
        TileMainPatchPlanner.Plan(floor, 0, 0, 4, 4, eval, cfg, seed, 4, ops);

        Assert.DoesNotContain(ops, o => o.Key.Role == TileSpriteRole.Main4x4);
        AssertExactCoverage(ops, 0, 0, 4, 4);
    }

    [Fact]
    public void Global_anchor_deterministic()
    {
        var cfg = TerrainNoiseConfig.Default(23);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 8, 8, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillUniformCategory(floor, 0, 0, 8, 8);

        var a = new List<TileDrawOp>();
        var b = new List<TileDrawOp>();
        TileMainPatchPlanner.Plan(floor, 0, 0, 8, 8, eval, cfg, 99, 4, a);
        TileMainPatchPlanner.Plan(floor, 0, 0, 8, 8, eval, cfg, 99, 4, b);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Key, b[i].Key);
            Assert.Equal(a[i].OriginGx, b[i].OriginGx);
            Assert.Equal(a[i].OriginGy, b[i].OriginGy);
            Assert.Equal(a[i].SizeCells, b[i].SizeCells);
        }
    }

    [Fact]
    public void Partial_rect_at_chunk_edge_skips_oversized_patches()
    {
        var cfg = TerrainNoiseConfig.Default(24);
        var eval = new TerrainEvaluator(cfg);
        var floor = new FloorSlice(0, 0, 8, 8, z: 0, chunkWidth: 32, chunkHeight: 32);
        FillUniformCategory(floor, 0, 0, 8, 8);

        var seed = FindSeedWhereAnchorWantsSize(0, 0, 4);
        var ops = new List<TileDrawOp>();
        TileMainPatchPlanner.Plan(floor, 0, 0, 3, 3, eval, cfg, seed, 4, ops);

        Assert.DoesNotContain(ops, o => o.Key.Role == TileSpriteRole.Main4x4);
        AssertExactCoverage(ops, 0, 0, 3, 3);
    }
}
