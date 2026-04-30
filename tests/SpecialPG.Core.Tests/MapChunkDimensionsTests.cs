using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class MapChunkDimensionsTests
{
    [Fact]
    public void GetChunkCountX_Y_uses_ceiling_division()
    {
        var d = new MapChunkDimensions(32, 32);
        Assert.Equal(1, d.GetChunkCountX(1));
        Assert.Equal(1, d.GetChunkCountY(1));
        Assert.Equal(3, d.GetChunkCountX(65));
        Assert.Equal(2, d.GetChunkCountY(64));
        Assert.Equal(3, d.GetChunkCountY(65));
    }

    [Fact]
    public void GetChunkWorldExtent_edge_chunk_is_partial()
    {
        var d = new MapChunkDimensions(32, 32);
        d.GetChunkWorldExtent(0, 0, 65, 65, out var ox, out var oy, out var lw, out var lh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(32, lw);
        Assert.Equal(32, lh);

        d.GetChunkWorldExtent(2, 2, 65, 65, out ox, out oy, out lw, out lh);
        Assert.Equal(64, ox);
        Assert.Equal(64, oy);
        Assert.Equal(1, lw);
        Assert.Equal(1, lh);
    }
}
