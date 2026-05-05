namespace SpecialPG.Core.Maps;

/// <summary>Legacy numeric ids; terrain is now <see cref="TileCell"/> + <see cref="TerrainNoiseConfig"/>. Retained for docs / migration notes.
/// </summary>
public static class TerrainTileKinds
{
    /// <summary>Default walkable land.</summary>
    public const ushort Land = 1;

    /// <summary>Water — typically combined with <see cref="TileFlags.Blocked"/> for movement.</summary>
    public const ushort Water = 2;
}
