namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Logical terrain appearance for sprite selection; aligned with <see cref="TerrainVisualColor"/> legend.</summary>
public enum TerrainRenderCategory : byte
{
    DeepWater,
    ShallowWater,
    Coast,
    Land,
    Hill,
    Blocked,
    ForcedLandCoastBlend,
    ForcedLandOverride,
    ForcedWater,
    Empty,
}
