using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class OriginWalkabilityPatchTests
{
    [Fact]
    public void Apply_makes_Chebyshev_ball_walkable()
    {
        var map = new WorldMap(8, 8, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(1);
        var floor0 = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 8; gy++)
        {
            for (var gx = 0; gx < 8; gx++)
                floor0.Set(gx, gy, TileCell.SyntheticWater());
        }

        const int r = 2;
        OriginWalkabilityPatch.ApplyToBoundedWorld(map, r);

        var cfg = map.TerrainConfig;
        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                var gx = dx;
                var gy = dy;
                if (gx < 0 || gx >= 8 || gy < 0 || gy >= 8)
                {
                    continue;
                }

                var t = floor0.Get(gx, gy);
                Assert.True(TileTraversal.IsWalkable(t, cfg), $"({gx},{gy})");
            }
        }
    }

    [Fact]
    public void Apply_patch_is_one_4_connected_component()
    {
        var map = new WorldMap(16, 16, 8, 8, 0, 0);
        map.TerrainConfig = TerrainNoiseConfig.Default(0);
        var floor0 = map.GetOrCreateFloor(0);
        for (var gy = 0; gy < 16; gy++)
        {
            for (var gx = 0; gx < 16; gx++)
                floor0.Set(gx, gy, TileCell.SyntheticWater());
        }

        const int r = 2;
        OriginWalkabilityPatch.ApplyToBoundedWorld(map, r);

        var anchorGx = 0;
        var anchorGy = 0;
        var gx0 = Math.Max(0, anchorGx - r);
        var gx1 = Math.Min(15, anchorGx + r);
        var gy0 = Math.Max(0, anchorGy - r);
        var gy1 = Math.Min(15, anchorGy + r);
        var expected = (gx1 - gx0 + 1) * (gy1 - gy0 + 1);
        var visited = new bool[16, 16];
        var q = new Queue<(int x, int y)>();
        q.Enqueue((0, 0));
        visited[0, 0] = true;
        var count = 0;
        var cfg = map.TerrainConfig;
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if (!TileTraversal.IsWalkable(floor0.Get(x, y), cfg))
            {
                continue;
            }

            count++;
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (nx < 0 || nx >= 16 || ny < 0 || ny >= 16 || visited[ny, nx])
                {
                    continue;
                }

                if (!TileTraversal.IsWalkable(floor0.Get(nx, ny), cfg))
                {
                    continue;
                }

                visited[ny, nx] = true;
                q.Enqueue((nx, ny));
            }
        }

        Assert.Equal(expected, count);
    }

    [Fact]
    public void ProceduralWorldMapGenerator_includes_origin_walkable_patch()
    {
        var p = MapGenerationParameters.Create(42, 50);
        const int w = 32;
        const int h = 32;
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(w, h, 8, 8, p, minX: -(w / 2), minY: -(h / 2));
        var floor = map.GetOrCreateFloor(0);
        var cfg = map.TerrainConfig;
        var r = OriginWalkabilityPatch.DefaultChebyshevRadius;
        const int originGx = 0;
        const int originGy = 0;
        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > r)
                    continue;

                var gx = originGx + dx;
                var gy = originGy + dy;
                var t = floor.Get(gx, gy);
                Assert.True(TileTraversal.IsWalkable(t, cfg), $"origin neighborhood ({gx},{gy})");
            }
        }
    }
}
