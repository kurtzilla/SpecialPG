namespace SpecialPG.Core.Maps;

/// <summary>Well-known <see cref="TileData.TileKind"/> values for terrain classification (Shell graphics map by kind id).
/// </summary>
public static class TerrainTileKinds
{
    /// <summary>Default walkable land.</summary>
    public const ushort Land = 1;

    /// <summary>Water — typically combined with <see cref="TileFlags.Blocked"/> for movement.</summary>
    public const ushort Water = 2;
}
