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

    [Fact]
    public void ApplyCircleSubTerrainAware_skips_ForceWater_tile()
    {
        var map = new WorldMap(1, 1);
        map.GetOrCreateFloor(0).Set(0, 0, new TileCell
        {
            Override = TerrainOverride.ForceWater,
            ElevationBucket = 1,
            MoistureBucket = 0,
        });
        var fog = new FogOfWarState();
        var eval = new TerrainEvaluator(map.TerrainConfig);
        fog.ApplyCircleSubTerrainAware(0, 0, 0.5f, 0.5f, 5f, map, eval, 0, 0, 1, 1);
        Assert.False(fog.IsRevealed(0, 0, 0, 0, 0, 0, 1, 1));
    }

    [Fact]
    public void ApplyCircleSubTerrainAware_does_not_reveal_ForceWater_cell_near_center()
    {
        var map = new WorldMap(5, 5);
        var floor = map.GetOrCreateFloor(0);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var o = x == 2 && y == 2 ? TerrainOverride.ForceWater : TerrainOverride.ForceLand;
                floor.Set(x, y, new TileCell { Override = o, ElevationBucket = 100, MoistureBucket = 0 });
            }
        }

        var fog = new FogOfWarState();
        var eval = new TerrainEvaluator(map.TerrainConfig);
        fog.ApplyCircleSubTerrainAware(0, 0, 2f, 1.5f, 1.5f, map, eval, 0, 0, 5, 5);
        Assert.False(fog.IsRevealed(0, 0, 2, 2, 0, 0, 5, 5));
        Assert.True(fog.IsRevealed(0, 0, 2, 1, 0, 0, 5, 5));
    }

    [Fact]
    public void IsWorldPointRevealed_true_for_legacy_full_cell_reveal()
    {
        var fog = new FogOfWarState();
        fog.ApplyCircle(0, 0, 5, 5, 2, 0, 0, 10, 10);
        Assert.True(fog.IsWorldPointRevealed(0, 0, 4.25f, 4.25f, 0, 0, 10, 10));
    }
}
