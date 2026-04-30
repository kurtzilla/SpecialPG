namespace SpecialPG.Core.Maps;

/// <summary>Movement rules on <see cref="TileData"/>; used by <see cref="WorldState"/>.</summary>
public static class TileTraversal
{
    public static bool IsWalkable(TileData tile) => (tile.Flags & TileFlags.Blocked) == 0;
}
