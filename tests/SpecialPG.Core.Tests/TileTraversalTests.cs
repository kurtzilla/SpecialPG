using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TileTraversalTests
{
    private static readonly TerrainNoiseConfig Config = TerrainNoiseConfig.Default(0);

    [Fact]
    public void Water_elevation_is_never_walkable_even_when_flags_omit_blocked()
    {
        var waterOpenFlags = TileCell.SyntheticWater() with { Flags = 0 };
        Assert.False(TileTraversal.IsWalkable(waterOpenFlags, Config));
    }

    [Fact]
    public void Blocked_flag_is_not_walkable()
    {
        var blocked = TileCell.SyntheticLand() with { Flags = TileFlags.Blocked };
        Assert.False(TileTraversal.IsWalkable(blocked, Config));
    }

    [Fact]
    public void Mid_elevation_land_is_walkable_when_not_blocked()
    {
        Assert.True(TileTraversal.IsWalkable(TileCell.SyntheticLand(), Config));
    }
}
