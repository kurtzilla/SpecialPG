using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public sealed class TileDrawOpComparerTests
{
    [Fact]
    public void Compare_orders_by_layer_then_gy_then_gx()
    {
        var a = new TileDrawOp(
            new TileSpriteKey(TerrainRenderCategory.Land, TileSpriteRole.Main1x1, 0),
            1, 2, 1, TerrainDrawLayer.GroundNatural);
        var b = new TileDrawOp(
            new TileSpriteKey(TerrainRenderCategory.Land, TileSpriteRole.Main1x1, 0),
            0, 2, 1, TerrainDrawLayer.GroundNatural);
        var c = new TileDrawOp(
            new TileSpriteKey(TerrainRenderCategory.Land, TileSpriteRole.Main1x1, 0),
            1, 1, 1, TerrainDrawLayer.GroundNatural);

        Assert.True(TileDrawOpComparer.Instance.Compare(c, a) < 0);
        Assert.True(TileDrawOpComparer.Instance.Compare(b, a) < 0);
        Assert.True(TileDrawOpComparer.Instance.Compare(c, b) < 0);
    }
}
