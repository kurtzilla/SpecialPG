using System.Collections.Generic;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class LandmassBridgeToLargestComponentTests
{
    [Fact]
    public void Apply_connects_isolated_origin_to_largest_landmass()
    {
        var map = new WorldMap(6, 6, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(42);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 6; gy++)
        {
            for (var gx = 0; gx < 6; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        floor.Set(0, 0, TileCell.SyntheticLand(0));
        floor.Set(4, 4, TileCell.SyntheticLand(0));
        floor.Set(4, 5, TileCell.SyntheticLand(0));
        floor.Set(5, 4, TileCell.SyntheticLand(0));
        floor.Set(5, 5, TileCell.SyntheticLand(0));

        LandmassBridgeToLargestComponent.ApplyToFloor(floor, map.TerrainConfig);

        var cfg = map.TerrainConfig;
        Assert.True(TileTraversal.IsWalkable(floor.Get(0, 0), cfg));
        Assert.True(TileTraversal.IsWalkable(floor.Get(4, 4), cfg));

        var seen = new bool[6, 6];
        var q = new Queue<(int X, int Y)>();
        q.Enqueue((0, 0));
        seen[0, 0] = true;
        var reachedBig = false;
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if (x >= 4 && y >= 4)
            {
                reachedBig = true;
                break;
            }

            Try(x - 1, y);
            Try(x + 1, y);
            Try(x, y - 1);
            Try(x, y + 1);

            void Try(int nx, int ny)
            {
                if ((uint)nx >= 6u || (uint)ny >= 6u || seen[nx, ny])
                    return;
                if (!TileTraversal.IsWalkable(floor.Get(nx, ny), cfg))
                    return;
                seen[nx, ny] = true;
                q.Enqueue((nx, ny));
            }
        }

        Assert.True(reachedBig);
    }

    [Fact]
    public void Apply_noop_when_origin_already_on_largest_component()
    {
        var map = new WorldMap(4, 4, 4, 4, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(1);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 4; gy++)
        {
            for (var gx = 0; gx < 4; gx++)
                floor.Set(gx, gy, TileCell.SyntheticLand(0));
        }

        floor.Set(3, 3, TileCell.SyntheticWater());

        var before = floor.Get(1, 1);
        LandmassBridgeToLargestComponent.ApplyToFloor(floor, map.TerrainConfig);
        var after = floor.Get(1, 1);
        Assert.Equal(before.Override, after.Override);
        Assert.Equal(before.Flags, after.Flags);
    }

    [Fact]
    public void Apply_skips_long_bridge_when_max_Manhattan_exceeded()
    {
        var map = new WorldMap(20, 20, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(99);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 20; gy++)
        {
            for (var gx = 0; gx < 20; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        floor.Set(0, 0, TileCell.SyntheticLand(0));
        for (var gy = 17; gy < 20; gy++)
        {
            for (var gx = 17; gx < 20; gx++)
                floor.Set(gx, gy, TileCell.SyntheticLand(0));
        }

        LandmassBridgeToLargestComponent.ApplyToFloor(floor, map.TerrainConfig, maxLandBridgeManhattanCells: 10);

        var cfg = map.TerrainConfig;
        Assert.False(TileTraversal.IsWalkable(floor.Get(1, 0), cfg));
        Assert.False(TileTraversal.IsWalkable(floor.Get(0, 1), cfg));
    }

    [Fact]
    public void Apply_connects_when_max_is_zero_unlimited()
    {
        var map = new WorldMap(20, 20, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(100);
        var floor = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 20; gy++)
        {
            for (var gx = 0; gx < 20; gx++)
                floor.Set(gx, gy, TileCell.SyntheticWater());
        }

        floor.Set(0, 0, TileCell.SyntheticLand(0));
        for (var gy = 17; gy < 20; gy++)
        {
            for (var gx = 17; gx < 20; gx++)
                floor.Set(gx, gy, TileCell.SyntheticLand(0));
        }

        LandmassBridgeToLargestComponent.ApplyToFloor(floor, map.TerrainConfig, maxLandBridgeManhattanCells: 0);

        var cfg = map.TerrainConfig;
        var seen = new bool[20, 20];
        var q = new Queue<(int X, int Y)>();
        q.Enqueue((0, 0));
        seen[0, 0] = true;
        var reachedBig = false;
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if (x >= 17 && y >= 17)
            {
                reachedBig = true;
                break;
            }

            Try(x - 1, y);
            Try(x + 1, y);
            Try(x, y - 1);
            Try(x, y + 1);

            void Try(int nx, int ny)
            {
                if ((uint)nx >= 20u || (uint)ny >= 20u || seen[nx, ny])
                    return;
                if (!TileTraversal.IsWalkable(floor.Get(nx, ny), cfg))
                    return;
                seen[nx, ny] = true;
                q.Enqueue((nx, ny));
            }
        }

        Assert.True(reachedBig);
    }
}
