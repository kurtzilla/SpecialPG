using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FloorSliceUnboundedMaterializationTests
{
    [Fact]
    public void Get_without_evaluator_returns_empty_tile()
    {
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 8, chunkHeight: 8);
        Assert.True(floor.Get(0, 0).IsEmpty);
        Assert.Equal(0, floor.LoadedChunkCount);
    }

    [Fact]
    public void Get_materializes_chunk_from_evaluator()
    {
        var cfg = TerrainNoiseConfig.Default(seed: 4242);
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 4, chunkHeight: 4)
        {
            TerrainEvaluator = new TerrainEvaluator(cfg),
        };

        var a = floor.Get(0, 0);
        var b = floor.Get(0, 0);
        Assert.False(a.IsEmpty);
        Assert.Equal(a, b);
        Assert.Equal(1, floor.LoadedChunkCount);
    }

    [Fact]
    public void Set_on_new_chunk_prefills_rest_from_noise()
    {
        var cfg = TerrainNoiseConfig.Default(seed: 7);
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 4, chunkHeight: 4)
        {
            TerrainEvaluator = new TerrainEvaluator(cfg),
        };

        var overrideCell = TileCell.SyntheticLand(variant: 3);
        floor.Set(0, 0, overrideCell);

        Assert.Equal(overrideCell, floor.Get(0, 0));
        var neighbor = floor.Get(1, 0);
        Assert.False(neighbor.IsEmpty);
    }

    [Fact]
    public void WorldMap_GetOrCreateFloor_assigns_evaluator_for_unbounded()
    {
        var map = WorldMap.CreateUnbounded(8, 8);
        map.TerrainConfig = TerrainNoiseConfig.Default(seed: 99);
        var floor = map.GetOrCreateFloor(0);
        Assert.NotNull(floor.TerrainEvaluator);
        Assert.False(floor.Get(5, -12).IsEmpty);
    }

    [Fact]
    public void Negative_chunk_coordinates_materialize()
    {
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 8, chunkHeight: 8)
        {
            TerrainEvaluator = new TerrainEvaluator(TerrainNoiseConfig.Default(11)),
        };
        _ = floor.Get(-1, -1);
        Assert.True(floor.LoadedChunkCount >= 1);
    }
}
