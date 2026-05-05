using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class MapGenerationParametersTests
{
    [Fact]
    public void Create_clamps_and_balances_water()
    {
        var p = MapGenerationParameters.Create(seed: 3, landPercent: 200);
        Assert.Equal(100, p.LandPercent);
        Assert.Equal(0, p.WaterPercent);
        Assert.True(p.IsValid);
    }

    [Fact]
    public void High_water_produces_mostly_water_tiles()
    {
        var p = MapGenerationParameters.Create(seed: 99_001, landPercent: 5);
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(64, 64, 16, 16, p);
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(map);
        var floor = map.GetOrCreateFloor(0);
        var water = 0;
        var land = 0;
        var midX = floor.MinX + floor.Width / 2;
        var midY = floor.MinY + floor.Height / 2;
        for (var y = floor.MinY; y < floor.MinY + floor.Height; y++)
        {
            for (var x = floor.MinX; x < floor.MinX + floor.Width; x++)
            {
                if (x == midX && y == midY)
                    continue;
                if (floor.Get(x, y).TileKind == TerrainTileKinds.Water)
                    water++;
                else
                    land++;
            }
        }

        Assert.True(water > land * 3, $"expected mostly water, got water={water} land={land}");
    }
}
