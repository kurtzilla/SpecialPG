using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TileSpriteResolverTests
{
    [Fact]
    public void ResolveCell_emits_one_main_op()
    {
        var cfg = TerrainNoiseConfig.Default(11);
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticLand(variant: 2);
        Span<TileDrawOp> buf = stackalloc TileDrawOp[2];

        TileSpriteResolver.ResolveCell(4, 8, tile, eval, cfg, worldSeed: 99, variantCount: 4, buf, out var count);

        Assert.Equal(1, count);
        Assert.Equal(TileSpriteRole.Main1x1, buf[0].Key.Role);
        Assert.Equal(4, buf[0].OriginGx);
        Assert.Equal(8, buf[0].OriginGy);
        Assert.Equal(1, buf[0].SizeCells);
        Assert.Equal(1, buf[0].Key.VariantIndex);
    }

    [Fact]
    public void Water_tile_uses_water_draw_layer()
    {
        var cfg = TerrainNoiseConfig.Default(12) with { WaterElevationThreshold = 1.01f };
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticWater();
        Span<TileDrawOp> buf = stackalloc TileDrawOp[1];

        TileSpriteResolver.ResolveCell(0, 0, tile, eval, cfg, 0, 4, buf, out _);

        Assert.Equal(TerrainDrawLayer.Water, buf[0].Layer);
    }

    [Fact]
    public void Land_tile_uses_ground_natural_layer()
    {
        var cfg = TerrainNoiseConfig.Default(13);
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticLand();
        Span<TileDrawOp> buf = stackalloc TileDrawOp[1];

        TileSpriteResolver.ResolveCell(1, 1, tile, eval, cfg, 0, 4, buf, out _);

        Assert.Equal(TerrainDrawLayer.GroundNatural, buf[0].Layer);
    }

    [Fact]
    public void Zero_length_destination_writes_nothing()
    {
        var cfg = TerrainNoiseConfig.Default(14);
        var eval = new TerrainEvaluator(cfg);
        var tile = TileCell.SyntheticLand();
        Span<TileDrawOp> buf = stackalloc TileDrawOp[0];

        TileSpriteResolver.ResolveCell(0, 0, tile, eval, cfg, 0, 4, buf, out var count);

        Assert.Equal(0, count);
    }
}
