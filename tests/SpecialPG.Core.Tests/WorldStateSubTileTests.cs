using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WorldStateSubTileTests
{
    [Fact]
    public void TryStepSubTile_crosses_east_tile_boundary()
    {
        var map = new WorldMap(4, 4, 8, 8);
        var floor = map.GetOrCreateFloor(0);
        var land = new TileCell { Override = TerrainOverride.ForceLand, ElevationBucket = 200 };
        floor.Set(0, 0, land);
        floor.Set(1, 0, land);
        var world = new WorldState(map, 0, 0, 0);
        world.SetActorCellFromShell(0, 0, 0, SubTileGrid.Resolution - 1, SubTileGrid.CenterSub);

        Assert.True(world.TryStepSubTile(1, 0));
        Assert.Equal(1, world.ActorX);
        Assert.Equal(0, world.ActorSubX);
    }

    [Fact]
    public void TryMove_recenters_sub_on_tile_step()
    {
        var map = new WorldMap(3, 3, 8, 8);
        var floor = map.GetOrCreateFloor(0);
        var land = new TileCell { Override = TerrainOverride.ForceLand, ElevationBucket = 200 };
        floor.Set(0, 0, land);
        floor.Set(1, 0, land);
        var world = new WorldState(map, 0, 0, 0);
        world.SetActorCellFromShell(0, 0, 0, 0, 0);

        Assert.True(world.TryMove(GridDirection.East));
        Assert.Equal(1, world.ActorX);
        Assert.Equal(SubTileGrid.CenterSub, world.ActorSubX);
        Assert.Equal(SubTileGrid.CenterSub, world.ActorSubY);
    }

    [Fact]
    public void TryStepSubTile_rejects_non_unit_delta()
    {
        var map = new WorldMap(2, 2, 4, 4);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand() with { Override = TerrainOverride.ForceLand });
        var world = new WorldState(map, 0, 0, 0);
        Assert.False(world.TryStepSubTile(2, 0));
    }
}
