using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public sealed class TileTransitionPlannerTests
{
    private static void FillLand(FloorSlice floor, int gx0, int gy0, int w, int h)
    {
        var land = TileCell.SyntheticLand() with { Override = TerrainOverride.ForceLand };
        for (var gy = gy0; gy < gy0 + h; gy++)
        {
            for (var gx = gx0; gx < gx0 + w; gx++)
                floor.Set(gx, gy, land);
        }
    }

    [Fact]
    public void Water_north_of_land_places_side_on_land_facing_north()
    {
        var cfg = TerrainNoiseConfig.Default(1);
        var map = new WorldMap(8, 8);
        var floor = map.GetOrCreateFloor(0);
        FillLand(floor, 0, 0, 8, 8);
        floor.Set(2, 1, TileCell.SyntheticWater() with { Override = TerrainOverride.ForceWater });
        floor.Set(2, 2, TileCell.SyntheticLand() with { Override = TerrainOverride.ForceLand });

        var eval = new TerrainEvaluator(cfg);
        var ops = new List<TileDrawOp>();
        TileTransitionPlanner.Plan(floor, 0, 0, 8, 8, eval, cfg, 1, 4, ops);

        var landSouthOfWater = ops.FirstOrDefault(o =>
            o.OriginGx == 2 && o.OriginGy == 2 && o.Key.Role == TileSpriteRole.Side);
        Assert.NotEqual(default(TileDrawOp), landSouthOfWater);
        Assert.Equal(TransitionFacing.North, landSouthOfWater.Facing);
    }

    [Fact]
    public void Same_group_neighbors_emit_no_transition()
    {
        var cfg = TerrainNoiseConfig.Default(2);
        var map = new WorldMap(4, 4);
        var floor = map.GetOrCreateFloor(0);
        FillLand(floor, 0, 0, 4, 4);

        var eval = new TerrainEvaluator(cfg);
        var ops = new List<TileDrawOp>();
        TileTransitionPlanner.Plan(floor, 0, 0, 4, 4, eval, cfg, 2, 4, ops);

        Assert.Empty(ops);
    }

    [Fact]
    public void Plan_is_deterministic()
    {
        var cfg = TerrainNoiseConfig.Default(3);
        var map = new WorldMap(6, 6);
        var floor = map.GetOrCreateFloor(0);
        FillLand(floor, 0, 0, 6, 6);
        floor.Set(2, 1, TileCell.SyntheticWater() with { Override = TerrainOverride.ForceWater });
        floor.Set(3, 2, TileCell.SyntheticLand() with { Override = TerrainOverride.ForceLand, Flags = TileFlags.Blocked });

        var eval = new TerrainEvaluator(cfg);
        var a = new List<TileDrawOp>();
        var b = new List<TileDrawOp>();
        TileTransitionPlanner.Plan(floor, 0, 0, 6, 6, eval, cfg, 9, 4, a);
        TileTransitionPlanner.Plan(floor, 0, 0, 6, 6, eval, cfg, 9, 4, b);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Key, b[i].Key);
            Assert.Equal(a[i].OriginGx, b[i].OriginGx);
            Assert.Equal(a[i].OriginGy, b[i].OriginGy);
            Assert.Equal(a[i].Facing, b[i].Facing);
        }
    }

    [Fact]
    public void Ground_blocked_pair_emits_transition()
    {
        var cfg = TerrainNoiseConfig.Default(4);
        var map = new WorldMap(4, 4);
        var floor = map.GetOrCreateFloor(0);
        FillLand(floor, 0, 0, 4, 4);
        floor.Set(2, 1, TileCell.SyntheticLand() with { Flags = TileFlags.Blocked });

        var eval = new TerrainEvaluator(cfg);
        var ops = new List<TileDrawOp>();
        TileTransitionPlanner.Plan(floor, 0, 0, 4, 4, eval, cfg, 4, 4, ops);

        Assert.True(
            ops.Exists(o => o.OriginGx == 1 && o.OriginGy == 1 && o.Facing == TransitionFacing.East)
            || ops.Exists(o => o.OriginGx == 2 && o.OriginGy == 1 && o.Facing == TransitionFacing.West));
    }
}
