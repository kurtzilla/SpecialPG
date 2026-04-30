using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WorldStateTests
{
    [Fact]
    public void TryMove_blocked_tile_fails()
    {
        var map = new WorldMap(3, 3);
        var floor = map.GetOrCreateFloor(0);
        floor.Set(1, 0, new TileData { TileKind = 1, Flags = TileFlags.Blocked, Variant = 0 });
        floor.Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });

        var state = new WorldState(map, 0, 0, 0);
        var ok = state.TryMove(GridDirection.East);

        Assert.False(ok);
        Assert.Equal(0, state.ActorX);
    }

    [Fact]
    public void TryMove_open_tile_succeeds()
    {
        var map = new WorldMap(3, 3);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });

        var state = new WorldState(map, 0, 0, 0);
        var ok = state.TryMove(GridDirection.East);

        Assert.True(ok);
        Assert.Equal(1, state.ActorX);
    }

    [Fact]
    public void TryUseVerticalLink_moves_actor()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.GetOrCreateFloor(1).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 1,
            ToY = 1,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var state = new WorldState(map, 0, 0, 0);
        var ok = state.TryUseVerticalLink();

        Assert.True(ok);
        Assert.Equal(1, state.ActorZ);
        Assert.Equal(1, state.ActorX);
        Assert.Equal(1, state.ActorY);
    }

    [Fact]
    public void TryUseVerticalLink_non_consecutive_Z_two_way_round_trip()
    {
        var map = new WorldMap(2, 2);
        var open = new TileData { TileKind = 1, Flags = 0, Variant = 0 };
        map.GetOrCreateFloor(0).Set(0, 0, open);
        map.GetOrCreateFloor(3).Set(0, 0, open);
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 3,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var state = new WorldState(map, 0, 0, 0);
        Assert.True(state.TryUseVerticalLink());
        Assert.Equal(3, state.ActorZ);
        Assert.Equal(0, state.ActorX);
        Assert.Equal(0, state.ActorY);

        Assert.True(state.TryUseVerticalLink());
        Assert.Equal(0, state.ActorZ);
        Assert.Equal(0, state.ActorX);
        Assert.Equal(0, state.ActorY);
    }
}
