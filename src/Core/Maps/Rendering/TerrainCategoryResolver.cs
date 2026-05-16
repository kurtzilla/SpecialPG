using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Thin wrapper over <see cref="TerrainAppearance"/> for sprite pipeline call sites.</summary>
public static class TerrainCategoryResolver
{
    public static TerrainRenderCategory Resolve(
        float worldX,
        float worldY,
        in TileCell tile,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain) =>
        TerrainAppearance.Resolve(worldX, worldY, tile, evaluator, terrain);
}
