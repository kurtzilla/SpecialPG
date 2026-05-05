using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class SubTileTraversalTests
{
    [Fact]
    public void Blocked_tile_unwalkable_at_sub_center()
    {
        var map = new WorldMap(4, 4, 8, 8);
        var floor = map.GetOrCreateFloor(0);
        floor.Set(1, 1, TileCell.SyntheticLand() with { Flags = TileFlags.Blocked });
        var eval = new TerrainEvaluator(map.TerrainConfig);
        Assert.False(SubTileTraversal.IsWalkable(map, 0, 1, 1, 8, 8, eval));
    }

    [Fact]
    public void ForceLand_walkable_even_if_noise_would_be_water()
    {
        var map = new WorldMap(4, 4, 8, 8);
        var floor = map.GetOrCreateFloor(0);
        floor.Set(0, 0, new TileCell { Override = TerrainOverride.ForceLand, ElevationBucket = 1 });
        var eval = new TerrainEvaluator(map.TerrainConfig);
        Assert.True(SubTileTraversal.IsWalkable(map, 0, 0, 0, 0, 0, eval));
    }

    [Fact]
    public void ForceWater_unwalkable()
    {
        var map = new WorldMap(2, 2, 4, 4);
        map.GetOrCreateFloor(0).Set(0, 0, new TileCell { Override = TerrainOverride.ForceWater });
        var eval = new TerrainEvaluator(map.TerrainConfig);
        Assert.False(SubTileTraversal.IsWalkable(map, 0, 0, 0, 0, 0, eval));
    }
}
