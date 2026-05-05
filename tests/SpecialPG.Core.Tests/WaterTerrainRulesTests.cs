using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WaterTerrainRulesTests
{
    private static readonly TerrainNoiseConfig Cfg = TerrainNoiseConfig.Default(0);

    [Fact]
    public void ApplyMinimumWaterBlobSizeTwoByTwo_removes_lone_water_tile()
    {
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 32, chunkHeight: 32);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                floor.Set(x, y, TileCell.SyntheticLand());
        }

        floor.Set(1, 1, TileCell.SyntheticWater());
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(floor, Cfg);

        Assert.False(TileTraversal.IsWaterSurface(floor.Get(1, 1), Cfg));
    }

    [Fact]
    public void ApplyMinimumWaterBlobSizeTwoByTwo_keeps_full_two_by_two_water_block()
    {
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 32, chunkHeight: 32);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                floor.Set(x, y, TileCell.SyntheticLand());
        }

        var w = TileCell.SyntheticWater();
        floor.Set(0, 0, w);
        floor.Set(1, 0, w);
        floor.Set(0, 1, w);
        floor.Set(1, 1, w);
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(floor, Cfg);

        Assert.True(TileTraversal.IsWaterSurface(floor.Get(0, 0), Cfg));
        Assert.True(TileTraversal.IsWaterSurface(floor.Get(1, 0), Cfg));
        Assert.True(TileTraversal.IsWaterSurface(floor.Get(0, 1), Cfg));
        Assert.True(TileTraversal.IsWaterSurface(floor.Get(1, 1), Cfg));
    }

    [Fact]
    public void ApplyMinimumWaterBlobSizeTwoByTwo_WorldMap_dispatches_per_floor()
    {
        var map = new WorldMap(4, 4);
        map.TerrainConfig = Cfg;
        var f = map.GetOrCreateFloor(0);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                f.Set(x, y, TileCell.SyntheticLand());
        }

        f.Set(1, 1, TileCell.SyntheticWater());
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(map);
        Assert.False(TileTraversal.IsWaterSurface(f.Get(1, 1), Cfg));
    }

    [Fact]
    public void IsWaterPartOfAtLeastOneTwoByTwoBlock_detects_corner_membership()
    {
        var floor = new FloorSlice(0, 0, 3, 3, z: 0, chunkWidth: 8, chunkHeight: 8);
        var w = TileCell.SyntheticWater();
        floor.Set(0, 0, w);
        floor.Set(1, 0, w);
        floor.Set(0, 1, w);
        floor.Set(1, 1, w);

        Assert.True(WaterTerrainRules.IsWaterPartOfAtLeastOneTwoByTwoBlock(floor, 0, 0, Cfg));
        Assert.True(WaterTerrainRules.IsWaterPartOfAtLeastOneTwoByTwoBlock(floor, 1, 1, Cfg));
    }
}
