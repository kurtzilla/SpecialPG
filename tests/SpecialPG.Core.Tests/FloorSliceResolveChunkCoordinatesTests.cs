using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class FloorSliceResolveChunkCoordinatesTests
{
    [Fact]
    public void Bounded_floor_maps_origin_cell_to_chunk_0_0()
    {
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 32, chunkHeight: 32);
        floor.Set(0, 0, TileCell.SyntheticLand());

        floor.ResolveChunkCoordinates(0, 0, out var cx, out var cy);
        Assert.Equal(0, cx);
        Assert.Equal(0, cy);
    }

    [Fact]
    public void Bounded_floor_maps_cell_in_second_chunk_column()
    {
        var floor = new FloorSlice(0, 0, z: 0, chunkWidth: 32, chunkHeight: 32);
        floor.Set(32, 0, TileCell.SyntheticLand());

        floor.ResolveChunkCoordinates(32, 0, out var cx, out var cy);
        Assert.Equal(1, cx);
        Assert.Equal(0, cy);
    }

    [Fact]
    public void Unbounded_floor_uses_floor_div_for_negative_global_cells()
    {
        var floor = new FloorSlice(-64, -64, z: 0, chunkWidth: 32, chunkHeight: 32);
        floor.Set(-65, -65, TileCell.SyntheticLand());

        floor.ResolveChunkCoordinates(-65, -65, out var cx, out var cy);
        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void GetChunkWorldCellRange_matches_resolve_for_bounded_edge()
    {
        var floor = new FloorSlice(0, 0, 40, 40, z: 0, chunkWidth: 32, chunkHeight: 32);
        floor.Set(35, 35, TileCell.SyntheticLand());

        floor.ResolveChunkCoordinates(35, 35, out var cx, out var cy);
        floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out var lw, out var lh);

        Assert.Equal(1, cx);
        Assert.Equal(1, cy);
        Assert.Equal(32, gx0);
        Assert.Equal(32, gy0);
        Assert.Equal(8, lw);
        Assert.Equal(8, lh);
    }
}
