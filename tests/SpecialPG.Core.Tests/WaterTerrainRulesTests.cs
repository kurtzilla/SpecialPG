using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WaterTerrainRulesTests
{
    [Fact]
    public void ApplyMinimumWaterBlobSizeTwoByTwo_removes_lone_water_tile()
    {
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 4, chunkHeight: 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                floor.Set(x, y, new TileData { TileKind = TerrainTileKinds.Land, Flags = 0, Variant = 0 });
        }

        floor.Set(1, 1, new TileData { TileKind = TerrainTileKinds.Water, Flags = TileFlags.Blocked, Variant = 0 });

        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(floor);

        Assert.Equal(TerrainTileKinds.Land, floor.Get(1, 1).TileKind);
    }

    [Fact]
    public void ApplyMinimumWaterBlobSizeTwoByTwo_keeps_full_two_by_two_water_block()
    {
        var floor = new FloorSlice(0, 0, 4, 4, z: 0, chunkWidth: 4, chunkHeight: 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                floor.Set(x, y, new TileData { TileKind = TerrainTileKinds.Land, Flags = 0, Variant = 0 });
        }

        var w = new TileData { TileKind = TerrainTileKinds.Water, Flags = TileFlags.Blocked, Variant = 0 };
        floor.Set(0, 0, w);
        floor.Set(1, 0, w);
        floor.Set(0, 1, w);
        floor.Set(1, 1, w);

        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(floor);

        Assert.Equal(TerrainTileKinds.Water, floor.Get(0, 0).TileKind);
        Assert.Equal(TerrainTileKinds.Water, floor.Get(1, 0).TileKind);
        Assert.Equal(TerrainTileKinds.Water, floor.Get(0, 1).TileKind);
        Assert.Equal(TerrainTileKinds.Water, floor.Get(1, 1).TileKind);
    }

    [Fact]
    public void Procedural_generation_has_no_water_outside_two_by_two_blocks()
    {
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(48, 36, 16, 16, seed: 12345);
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(map);
        foreach (var z in new[] { 0, 1 })
        {
            var f = map.GetOrCreateFloor(z);
            for (var gy = f.MinY; gy < f.MinY + f.Height; gy++)
            {
                for (var gx = f.MinX; gx < f.MinX + f.Width; gx++)
                {
                    if (f.Get(gx, gy).TileKind != TerrainTileKinds.Water)
                        continue;
                    Assert.True(
                        WaterTerrainRules.IsWaterPartOfAtLeastOneTwoByTwoBlock(f, gx, gy),
                        $"Water at ({gx},{gy}) on Z={z} is not in any 2×2 water block.");
                }
            }
        }
    }
}
