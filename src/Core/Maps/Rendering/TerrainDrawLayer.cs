namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Draw order group for terrain ops (Factorio-style layer groups, simplified).</summary>
public enum TerrainDrawLayer : byte
{
    Water = 0,
    GroundNatural = 1,
    GroundArtificial = 2,
    Decor = 3,
}
