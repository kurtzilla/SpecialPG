using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FloorSliceChunkLifecycleTests
{
    [Fact]
    public void Set_records_modified_chunk()
    {
        var floor = new FloorSlice(0, 0, 16, 16, z: 0, chunkWidth: 8, chunkHeight: 8);
        Assert.Equal(0, floor.ModifiedChunkCount);
        floor.Set(0, 0, TileCell.SyntheticLand());
        Assert.Equal(1, floor.ModifiedChunkCount);
        Assert.True(floor.IsChunkModified(0, 0));
    }

    [Fact]
    public void Suppress_tracking_skips_modified_set()
    {
        var floor = new FloorSlice(0, 0, 8, 8, 0, 4, 4);
        floor.SuppressChunkModificationTracking = true;
        floor.Set(0, 0, TileCell.SyntheticLand());
        floor.SuppressChunkModificationTracking = false;
        Assert.Equal(0, floor.ModifiedChunkCount);
    }

    [Fact]
    public void TryEvictUnmodifiedChunk_removes_noise_only_materialization()
    {
        var map = WorldMap.CreateUnbounded(8, 8);
        var floor = map.GetOrCreateFloor(0);
        floor.TerrainEvaluator = new TerrainEvaluator(TerrainNoiseConfig.Default(1));
        _ = floor.Get(0, 0);
        Assert.Equal(1, floor.LoadedChunkCount);
        Assert.Equal(0, floor.ModifiedChunkCount);
        Assert.True(floor.TryEvictUnmodifiedChunk(0, 0));
        Assert.Equal(0, floor.LoadedChunkCount);
    }

    [Fact]
    public void TryEvictUnmodifiedChunk_fails_after_Set()
    {
        var floor = new FloorSlice(0, 0, 16, 16, 0, 8, 8);
        floor.Set(0, 0, TileCell.SyntheticLand());
        Assert.False(floor.TryEvictUnmodifiedChunk(0, 0));
        Assert.Equal(1, floor.LoadedChunkCount);
    }

    [Fact]
    public void ClearChunkModificationTracking_clears_flags()
    {
        var floor = new FloorSlice(0, 0, 8, 8, 0, 4, 4);
        floor.Set(0, 0, TileCell.SyntheticLand());
        floor.ClearChunkModificationTracking();
        Assert.Equal(0, floor.ModifiedChunkCount);
    }

    [Fact]
    public void ProceduralWorldMapGenerator_leaves_no_modified_chunks_before_water_rules()
    {
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(32, 32, 16, 16, MapGenerationParameters.Create(3, 55));
        foreach (var z in map.PresentFloorIndices())
        {
            Assert.True(map.TryGetFloor(z, out var floor) && floor is not null);
            Assert.Equal(0, floor!.ModifiedChunkCount);
        }
    }

    [Fact]
    public void WorldMap_clears_tracking_on_all_floors()
    {
        var map = new WorldMap(8, 8, 4, 4);
        var f0 = map.GetOrCreateFloor(0);
        f0.Set(0, 0, TileCell.SyntheticLand());
        map.ClearChunkModificationTrackingOnAllFloors();
        Assert.Equal(0, f0.ModifiedChunkCount);
    }
}
