using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FloorSliceChunkTests
{
    [Fact]
    public void Set_across_chunk_boundary_round_trips()
    {
        var floor = new FloorSlice(0, 0, 65, 65, 0, 32, 32);
        var a = new TileData { TileKind = 7, Flags = 1, Variant = 2 };
        var b = new TileData { TileKind = 9, Flags = 0, Variant = 1 };
        floor.Set(31, 0, a);
        floor.Set(32, 0, b);
        floor.Set(64, 64, new TileData { TileKind = 3, Flags = 0, Variant = 0 });

        Assert.Equal(a, floor.Get(31, 0));
        Assert.Equal(b, floor.Get(32, 0));
        Assert.Equal(3, floor.Get(64, 64).TileKind);
    }

    [Fact]
    public void Set_default_on_unallocated_chunk_does_not_allocate()
    {
        var floor = new FloorSlice(0, 0, 64, 64, 0, 32, 32);
        floor.Set(0, 0, default);
        Assert.False(floor.HasAnyDefinedTile());
    }
}
