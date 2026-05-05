namespace SpecialPG.Core.Maps;

/// <summary>Movement rules on <see cref="TileData"/>; used by <see cref="WorldState"/>.</summary>
public static class TileTraversal
{
    /// <summary>Water is never walkable even when <see cref="TileFlags"/> omit <see cref="TileFlags.Blocked"/> (e.g. parsed maps).</summary>
    public static bool IsWalkable(TileData tile)
    {
        if (tile.TileKind == TerrainTileKinds.Water)
            return false;
        return (tile.Flags & TileFlags.Blocked) == 0;
    }
}
