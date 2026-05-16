namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Identifies one sprite region in the terrain atlas.</summary>
public readonly record struct TileSpriteKey(
    TerrainRenderCategory Category,
    TileSpriteRole Role,
    int VariantIndex);
