namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Deterministic terrain sprite variant index for a grid cell.</summary>
public static class TileVariantSelector
{
    /// <summary>
    /// Picks <c>0 .. variantCount-1</c>. <paramref name="tileVariant"/> 0 means auto (hash);
    /// non-zero uses <c>(tileVariant - 1) % variantCount</c>.
    /// </summary>
    public static int SelectVariant(int gx, int gy, int worldSeed, byte tileVariant, int variantCount)
    {
        if (variantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(variantCount));

        if (tileVariant != 0)
            return (tileVariant - 1) % variantCount;

        var hash = HashCode.Combine(gx, gy, worldSeed);
        return (int)((uint)hash % (uint)variantCount);
    }
}
