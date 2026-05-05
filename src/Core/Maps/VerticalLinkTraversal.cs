using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Gates use of a **single** <see cref="VerticalLink"/> hop. Outgoing uses <c>From→To</c>; when not <see cref="VerticalLink.OneWay"/>, reverse uses <c>To→From</c> over the same link (same geometry, opposite direction).
/// </summary>
public static class VerticalLinkTraversal
{
    public static bool CanTraverseOutgoing(VerticalLink link, TileCell fromTile, TileCell toTile,
        in TerrainNoiseConfig terrain)
    {
        _ = link.Kind;
        return TileTraversal.IsWalkable(fromTile, terrain) && TileTraversal.IsWalkable(toTile, terrain);
    }

    public static bool CanTraverseReverse(VerticalLink link, TileCell currentTile, TileCell destinationTile,
        in TerrainNoiseConfig terrain)
    {
        if (link.OneWay)
        {
            return false;
        }

        return TileTraversal.IsWalkable(currentTile, terrain) &&
               TileTraversal.IsWalkable(destinationTile, terrain);
    }
}
