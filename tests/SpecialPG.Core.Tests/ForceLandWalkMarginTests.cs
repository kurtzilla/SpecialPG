using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class ForceLandWalkMarginTests
{
    [Fact]
    public void Dilates_one_cell_ring_around_force_land_in_water_field()
    {
        var map = new WorldMap(5, 5, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(9);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 5; gy++)
        {
            for (var gx = 0; gx < 5; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        floor.Set(2, 2, TileCell.SyntheticLand(0) with { Override = TerrainOverride.ForceLand });
        ForceLandWalkMargin.ApplyToFloor(floor, map.TerrainConfig);

        foreach (var (gx, gy) in new[] { (2, 2), (1, 2), (3, 2), (2, 1), (2, 3) })
        {
            var t = floor.Get(gx, gy);
            Assert.Equal(TerrainOverride.ForceLand, t.Override);
        }

        Assert.NotEqual(TerrainOverride.ForceLand, floor.Get(1, 1).Override);
    }

    [Fact]
    public void Does_not_overwrite_force_water_even_when_adjacent_to_force_land()
    {
        var map = new WorldMap(3, 3, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(3);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 3; gy++)
        {
            for (var gx = 0; gx < 3; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        floor.Set(1, 1, TileCell.SyntheticLand(0) with { Override = TerrainOverride.ForceLand });
        floor.Set(1, 0, TileCell.SyntheticWater() with { Override = TerrainOverride.ForceWater });
        ForceLandWalkMargin.ApplyToFloor(floor, map.TerrainConfig);

        Assert.Equal(TerrainOverride.ForceWater, floor.Get(1, 0).Override);
    }

    [Fact]
    public void Does_not_overwrite_dry_blocked_tiles()
    {
        var map = new WorldMap(3, 3, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(4) with { WaterElevationThreshold = -0.5f };
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 3; gy++)
        {
            for (var gx = 0; gx < 3; gx++)
                floor.Set(gx, gy, TileCell.SyntheticLand(0));
        }

        var dryBlocked = new TileCell
        {
            ElevationBucket = 200,
            MoistureBucket = 0,
            Override = TerrainOverride.None,
            Flags = TileFlags.Blocked,
            Variant = 0,
        };
        floor.Set(1, 0, dryBlocked);
        floor.Set(1, 1, TileCell.SyntheticLand(0) with { Override = TerrainOverride.ForceLand });
        ForceLandWalkMargin.ApplyToFloor(floor, map.TerrainConfig);

        Assert.Equal(TerrainOverride.None, floor.Get(1, 0).Override);
        Assert.Equal(TileFlags.Blocked, floor.Get(1, 0).Flags);
    }

    /// <summary>
    /// Repro for patch edge: <see cref="TileCell.SyntheticWater"/> is tile-blocked; standing on <see cref="TerrainOverride.ForceLand"/>
    /// west of it cannot sub-step west until margin converts that neighbor to ForceLand.
    /// </summary>
    [Fact]
    public void West_sub_step_from_force_land_blocked_by_tile_water_until_margin_applies()
    {
        var cfg = TerrainNoiseConfig.Default(11);
        var map = new WorldMap(3, 3, 8, 8, 0, 0);
        map.TerrainConfig = cfg;
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 3; gy++)
        {
            for (var gx = 0; gx < 3; gx++)
                floor.Set(gx, gy, TileCell.SyntheticLand(0));
        }

        floor.Set(1, 1, TileCell.SyntheticLand(0) with { Override = TerrainOverride.ForceLand });
        floor.Set(0, 1, TileCell.SyntheticWater());

        var world = new WorldState(map, 1, 1, 0);
        world.SetActorCellFromShell(1, 1, 0, 0, SubTileGrid.CenterSub);
        Assert.False(world.TryStepSubTile(-1, 0));

        var eval = new TerrainEvaluator(cfg);
        var reason = SubTileTraversal.DiagnoseUnwalkable(map, 0, 0, 1, SubTileGrid.Resolution - 1, SubTileGrid.CenterSub, eval);
        Assert.NotNull(reason);

        ForceLandWalkMargin.ApplyToFloor(floor, cfg);
        var world2 = new WorldState(map, 1, 1, 0);
        world2.SetActorCellFromShell(1, 1, 0, 0, SubTileGrid.CenterSub);
        Assert.True(world2.TryStepSubTile(-1, 0));
    }
}
