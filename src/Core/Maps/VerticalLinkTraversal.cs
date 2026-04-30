namespace SpecialPG.Core.Maps;

/// <summary>
/// Gates use of a **single** <see cref="VerticalLink"/> hop. Outgoing uses <c>From→To</c>; when not <see cref="VerticalLink.OneWay"/>, reverse uses <c>To→From</c> over the same link (same geometry, opposite direction).
/// </summary>
public static class VerticalLinkTraversal
{
    public static bool CanTraverseOutgoing(VerticalLink link, TileData fromTile, TileData toTile)
    {
        _ = link.Kind;
        return TileTraversal.IsWalkable(fromTile) && TileTraversal.IsWalkable(toTile);
    }

    public static bool CanTraverseReverse(VerticalLink link, TileData currentTile, TileData destinationTile)
    {
        if (link.OneWay)
        {
            return false;
        }

        return TileTraversal.IsWalkable(currentTile) && TileTraversal.IsWalkable(destinationTile);
    }
}
