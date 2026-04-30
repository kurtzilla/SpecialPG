using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WorldMapSetFloorTests
{
    [Fact]
    public void SetFloor_rejects_slice_with_mismatched_chunk_dimensions()
    {
        var map = new WorldMap(10, 10, 32, 32);
        var wrong = new FloorSlice(0, 0, 10, 10, 0, 16, 16);
        Assert.Throws<ArgumentException>(() => map.SetFloor(wrong));
    }
}
