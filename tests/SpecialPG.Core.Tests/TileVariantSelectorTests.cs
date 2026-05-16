using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TileVariantSelectorTests
{
    [Fact]
    public void Same_coordinates_and_seed_yield_same_variant()
    {
        var a = TileVariantSelector.SelectVariant(12, -3, 42, 0, 4);
        var b = TileVariantSelector.SelectVariant(12, -3, 42, 0, 4);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_spreads_variants_across_a_grid()
    {
        var seen = new HashSet<int>();
        for (var gx = 0; gx < 16; gx++)
        {
            for (var gy = 0; gy < 16; gy++)
                seen.Add(TileVariantSelector.SelectVariant(gx, gy, 42, 0, 4));
        }

        Assert.True(seen.Count > 1, "Expected more than one variant across a 16×16 grid.");
    }

    [Fact]
    public void TileVariant_non_zero_overrides_hash()
    {
        var hashed = TileVariantSelector.SelectVariant(7, 11, 99, 0, 4);
        var forced = TileVariantSelector.SelectVariant(7, 11, 99, 2, 4);
        Assert.Equal(1, forced);
        Assert.NotEqual(forced, hashed);
    }

    [Fact]
    public void Variant_is_within_count()
    {
        for (var i = 0; i < 32; i++)
        {
            var v = TileVariantSelector.SelectVariant(i, i * 2, 7, 0, 4);
            Assert.InRange(v, 0, 3);
        }
    }

    [Fact]
    public void Zero_variant_count_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TileVariantSelector.SelectVariant(0, 0, 0, 0, 0));
    }
}
