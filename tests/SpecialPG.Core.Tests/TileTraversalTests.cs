using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TileTraversalTests
{
    [Fact]
    public void Water_is_never_walkable_even_without_blocked_flag()
    {
        var waterOpenFlags = new TileData { TileKind = TerrainTileKinds.Water, Flags = 0, Variant = 0 };
        Assert.False(TileTraversal.IsWalkable(waterOpenFlags));

        var waterBlocked = new TileData { TileKind = TerrainTileKinds.Water, Flags = TileFlags.Blocked, Variant = 0 };
        Assert.False(TileTraversal.IsWalkable(waterBlocked));
    }

    [Fact]
    public void Walkable_land_requires_no_blocked_flag()
    {
        Assert.True(TileTraversal.IsWalkable(new TileData { TileKind = TerrainTileKinds.Land, Flags = 0, Variant = 0 }));
        Assert.False(TileTraversal.IsWalkable(new TileData { TileKind = TerrainTileKinds.Land, Flags = TileFlags.Blocked, Variant = 0 }));
    }
}
