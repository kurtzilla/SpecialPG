using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FloorSliceChunkTests
{
    [Fact]
    public void Sparse_chunks_round_trip_non_overlapping_cells()
    {
        var floor = new FloorSlice(0, 0, 256, 256, z: 0, chunkWidth: 32, chunkHeight: 32);
        var a = new TileCell { ElevationBucket = 200, MoistureBucket = 100, Flags = 1, Variant = 2 };
        var b = new TileCell { ElevationBucket = 180, MoistureBucket = 50, Flags = 0, Variant = 1 };
        floor.Set(10, 10, a);
        floor.Set(64, 64, b);

        Assert.Equal(a, floor.Get(10, 10));
        Assert.Equal(b, floor.Get(64, 64));
        Assert.Equal(200, floor.Get(10, 10).ElevationBucket);
        Assert.Equal(180, floor.Get(64, 64).ElevationBucket);
    }
}
