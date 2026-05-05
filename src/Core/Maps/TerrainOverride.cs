namespace SpecialPG.Core.Maps;

/// <summary>Player or tool override of procedural terrain for a cell.</summary>
public enum TerrainOverride : byte
{
    None = 0,
    ForceLand = 1,
    ForceWater = 2,
}
