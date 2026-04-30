using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FogOfWarStateTests
{
    [Fact]
    public void ApplyAxisAlignedRect_marks_cells_inside_half_extents()
    {
        var fog = new FogOfWarState();
        fog.ApplyAxisAlignedRect(0, 0, 1, 1, 1, 1, 0, 0, 4, 4);
        Assert.True(fog.IsRevealed(0, 0, 0, 0, 0, 0, 4, 4));
        Assert.True(fog.IsRevealed(0, 0, 1, 1, 0, 0, 4, 4));
        Assert.True(fog.IsRevealed(0, 0, 2, 2, 0, 0, 4, 4));
        Assert.False(fog.IsRevealed(0, 0, 3, 3, 0, 0, 4, 4));
    }

    [Fact]
    public void ApplyAxisAlignedRect_with_negative_map_origin()
    {
        var fog = new FogOfWarState();
        const int minX = -4;
        const int minY = -4;
        fog.ApplyAxisAlignedRect(0, 0, -1, 0, 2, 2, minX, minY, 8, 8);
        Assert.True(fog.IsRevealed(0, 0, -2, 0, minX, minY, 8, 8));
        Assert.False(fog.IsRevealed(0, 0, 4, 0, minX, minY, 8, 8));
    }

    [Fact]
    public void ApplyCircle_reveals_cells_inside_radius_and_excludes_corners()
    {
        var fog = new FogOfWarState();
        fog.ApplyCircle(0, 0, 2, 2, 2, 0, 0, 6, 6);

        Assert.True(fog.IsRevealed(0, 0, 2, 2, 0, 0, 6, 6));
        Assert.True(fog.IsRevealed(0, 0, 4, 2, 0, 0, 6, 6));
        Assert.True(fog.IsRevealed(0, 0, 2, 0, 0, 0, 6, 6));
        Assert.False(fog.IsRevealed(0, 0, 4, 4, 0, 0, 6, 6));
    }

    [Fact]
    public void ApplyCircle_respects_negative_origin_and_map_bounds()
    {
        var fog = new FogOfWarState();
        const int minX = -4;
        const int minY = -4;
        fog.ApplyCircle(0, 0, -3, -3, 2, minX, minY, 6, 6);

        Assert.True(fog.IsRevealed(0, 0, -3, -3, minX, minY, 6, 6));
        Assert.True(fog.IsRevealed(0, 0, -1, -3, minX, minY, 6, 6));
        Assert.False(fog.IsRevealed(0, 0, 2, 2, minX, minY, 6, 6));
    }
}
