using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WorldStateTests
{
    [Fact]
    public void TryMove_blocked_by_tile_flags()
    {
        var map = new WorldMap(3, 3);
        var floor = map.GetOrCreateFloor(0);
        floor.Set(1, 0, TileCell.SyntheticLand() with { Flags = TileFlags.Blocked });
        floor.Set(0, 0, TileCell.SyntheticLand());
        var world = new WorldState(map, 0, 0, 0);

        Assert.False(world.TryMove(GridDirection.East));
        Assert.Equal(0, world.ActorX);
    }

    [Fact]
    public void TryUseVerticalLink_moves_actor()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var world = new WorldState(map, 0, 0, 0);
        Assert.True(world.TryUseVerticalLink());
        Assert.Equal(1, world.ActorZ);
    }

    [Fact]
    public void TryCyclePresentFloor_changes_Z()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        var world = new WorldState(map, 0, 0, 0);

        Assert.True(world.TryCyclePresentFloor(1));
        Assert.Equal(1, world.ActorZ);
    }

    [Fact]
    public void ClampAfterShellMapMutation_clamps_actor_when_bounded()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        var world = new WorldState(map, 0, 0, 0);

        world.SetActorCellFromShell(99, 99, 0);
        Assert.Equal(1, world.ActorX);
        Assert.Equal(1, world.ActorY);
    }

    [Fact]
    public void TryUseVerticalLink_reverse_hop_works()
    {
        var map = new WorldMap(2, 2);
        var open = TileCell.SyntheticLand();
        map.GetOrCreateFloor(0).Set(0, 0, open);
        map.GetOrCreateFloor(1).Set(0, 0, open);
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var world = new WorldState(map, 0, 0, 1);
        Assert.True(world.TryUseVerticalLink());
        Assert.Equal(0, world.ActorZ);
    }
}
