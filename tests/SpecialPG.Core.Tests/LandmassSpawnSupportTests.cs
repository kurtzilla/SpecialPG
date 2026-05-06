using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class LandmassSpawnSupportTests
{
    private static TileCell ForceLand => TileCell.SyntheticLand(0) with { Override = TerrainOverride.ForceLand };

    [Fact]
    public void Chooses_largest_landmass_nearest_to_center_in_Chebyshev_order()
    {
        var map = new WorldMap(7, 7, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(1);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 7; gy++)
        {
            for (var gx = 0; gx < 7; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        // Large component: left strip x=0..2 full height (21 cells)
        for (var gy = 0; gy < 7; gy++)
        {
            for (var gx = 0; gx <= 2; gx++)
                floor.Set(gx, gy, ForceLand);
        }

        // Small island not 4-adjacent to the strip (would merge with strip if touching).
        floor.Set(6, 6, ForceLand);

        var centerGx = 3;
        var centerGy = 3;
        Assert.True(LandmassSpawnSupport.TryFindSpawnChebyshevFromCenterOnLargestLandmass(
            floor, map.TerrainConfig, centerGx, centerGy,
            acceptSpawnCell: null,
            out var sx, out var sy));

        Assert.True(sx is >= 0 and <= 2);
        Assert.True(sy is >= 0 and <= 6);
        Assert.Equal(2, sx);
        Assert.Equal(2, sy);
    }

    [Fact]
    public void AcceptSpawnCell_filters_candidates_on_same_landmass()
    {
        var map = new WorldMap(5, 5, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(1);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 5; gy++)
        {
            for (var gx = 0; gx < 5; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        for (var gy = 0; gy < 5; gy++)
        {
            for (var gx = 0; gx <= 2; gx++)
                floor.Set(gx, gy, ForceLand);
        }

        var ok = LandmassSpawnSupport.TryFindSpawnChebyshevFromCenterOnLargestLandmass(
            floor, map.TerrainConfig, 2, 2,
            (_, _) => false,
            out _, out _);
        Assert.False(ok);
    }
}
