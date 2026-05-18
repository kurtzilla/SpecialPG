using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public sealed class ProceduralWorldMapGeneratorTests
{
    private static (int MinX, int MinY) CenteredMinCorner(int widthCells, int heightCells) =>
        (-(widthCells / 2), -(heightCells / 2));

    [Fact]
    public void Centered_map_origin_is_walkable_on_largest_landmass()
    {
        const int width = 128;
        const int height = 128;
        var (minX, minY) = CenteredMinCorner(width, height);
        var parameters = MapGenerationParameters.Create(42_4242, 38);
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(
            width, height, 32, 32, parameters, minX, minY);

        Assert.True(map.ProceduralLandmassAligned);
        var floor = map.GetOrCreateFloor(0);
        const int originGx = 0;
        const int originGy = 0;
        Assert.True(TileTraversal.IsWalkable(floor.Get(originGx, originGy), map.TerrainConfig));
        Assert.True(LandmassSpawnSupport.IsWalkableOnLargestLandmass(
            floor, map.TerrainConfig, originGx, originGy));
    }

    [Fact]
    public void Noise_offset_is_deterministic_for_same_seed()
    {
        const int width = 128;
        const int height = 128;
        var (minX, minY) = CenteredMinCorner(width, height);
        var parameters = MapGenerationParameters.Create(99_001, 42);

        var mapA = ProceduralWorldMapGenerator.BuildBoundedWorld(
            width, height, 32, 32, parameters, minX, minY);
        var mapB = ProceduralWorldMapGenerator.BuildBoundedWorld(
            width, height, 32, 32, parameters, minX, minY);

        Assert.Equal(mapA.ProceduralNoiseOffsetGx, mapB.ProceduralNoiseOffsetGx);
        Assert.Equal(mapA.ProceduralNoiseOffsetGy, mapB.ProceduralNoiseOffsetGy);
    }

    [Fact]
    public void Large_map_subsamples_elevation_and_approximates_water_percent()
    {
        const int width = 256;
        const int height = 256;
        const int landPercent = 38;
        var parameters = MapGenerationParameters.Create(42_4242, landPercent);
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(
            width, height, 32, 32, parameters);

        var floor = map.GetOrCreateFloor(0);
        var eval = new TerrainEvaluator(map.TerrainConfig);
        var water = 0;
        var total = 0;
        for (var gy = floor.MinY; gy < floor.MinY + floor.Height; gy++)
        {
            for (var gx = floor.MinX; gx < floor.MinX + floor.Width; gx++)
            {
                total++;
                var sample = eval.EvaluateAt(gx + 0.5f, gy + 0.5f);
                if (eval.IsWater(sample))
                    water++;
            }
        }

        var actualWaterPercent = 100.0 * water / total;
        var expectedWater = parameters.WaterPercent;
        Assert.InRange(actualWaterPercent, expectedWater - 8, expectedWater + 8);
    }
}
