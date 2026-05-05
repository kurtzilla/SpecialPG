using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>Result of <see cref="MapIntegrity.Validate"/>.</summary>
public sealed class MapIntegrityResult
{
    public bool IsValid => Issues.Count == 0;

    /// <summary>True if any issue has <see cref="MapIntegritySeverity.Error"/> (map should be rejected on load).</summary>
    public bool HasErrors => Issues.Exists(static i => i.Severity == MapIntegritySeverity.Error);

    public List<MapIntegrityIssue> Issues { get; } = new();
}

public enum MapIntegritySeverity
{
    Error,
    Warning,
}

public sealed class MapIntegrityIssue
{
    public MapIntegritySeverity Severity { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Validates rules from <c>docs/architecture.md</c> (map connectivity): every floor with defined tiles
/// must participate in at least one vertical link to a different floor index.
/// Milestone 8: <see cref="ValidateModification"/> and <see cref="ValidateVerticalLink"/> support incremental checks without scanning the whole map.
/// </summary>
public static class MapIntegrity
{
    /// <summary>
    /// Bounded / streaming-safe checks for a single cell write: bounds, floor presence, and any vertical link
    /// touching this cell still traverses under <see cref="VerticalLinkTraversal"/> with the pending tile applied.
    /// </summary>
    public static MapIntegrityResult ValidateModification(
        WorldMap map,
        int z,
        int x,
        int y,
        TileCell newTile,
        in TerrainNoiseConfig terrain)
    {
        ArgumentNullException.ThrowIfNull(map);
        var result = new MapIntegrityResult();
        if (newTile.IsEmpty)
            return result;

        if (!map.TryGetFloor(z, out var floor) || floor is null)
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message = $"ValidateModification: floor Z={z} is not present.",
            });
            return result;
        }

        if (!floor.Contains(x, y))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message = $"ValidateModification: ({x},{y}) is outside floor Z={z} bounds.",
            });
            return result;
        }

        if (map.IsBounded && !InBounds(map, x, y))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message =
                    $"ValidateModification: ({x},{y}) is outside map bounds X∈[{map.MinX},{map.MinX + map.Width}) Y∈[{map.MinY},{map.MinY + map.Height}).",
            });
            return result;
        }

        foreach (var link in map.VerticalLinks)
        {
            if (!LinkTouchesCell(link, x, y, z))
                continue;

            var fromTile = SampleTileWithPending(map, link.FromZ, link.FromX, link.FromY, z, x, y, newTile);
            var toTile = SampleTileWithPending(map, link.ToZ, link.ToX, link.ToY, z, x, y, newTile);

            if (!VerticalLinkTraversal.CanTraverseOutgoing(link, fromTile, toTile, terrain))
            {
                result.Issues.Add(new MapIntegrityIssue
                {
                    Severity = MapIntegritySeverity.Warning,
                    Message =
                        $"ValidateModification: vertical link From ({link.FromX},{link.FromY},{link.FromZ}) → To ({link.ToX},{link.ToY},{link.ToZ}) would not be traversable after updating ({x},{y},{z}).",
                });
            }

            if (!link.OneWay &&
                !VerticalLinkTraversal.CanTraverseReverse(link, toTile, fromTile, terrain))
            {
                result.Issues.Add(new MapIntegrityIssue
                {
                    Severity = MapIntegritySeverity.Warning,
                    Message =
                        $"ValidateModification: vertical link reverse To ({link.ToX},{link.ToY},{link.ToZ}) → From ({link.FromX},{link.FromY},{link.FromZ}) would not be traversable after updating ({x},{y},{z}).",
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Endpoint bounds / floor presence plus <see cref="VerticalLinkTraversal"/> rules for both directions (when not one-way).
    /// </summary>
    public static MapIntegrityResult ValidateVerticalLink(WorldMap map, VerticalLink link, in TerrainNoiseConfig terrain)
    {
        ArgumentNullException.ThrowIfNull(map);
        var result = new MapIntegrityResult();
        AppendLinkEndpointIssues(map, link, result);
        if (result.HasErrors)
            return result;

        if (!TryGetTile(map, link.FromZ, link.FromX, link.FromY, out var fromTile) ||
            !TryGetTile(map, link.ToZ, link.ToX, link.ToY, out var toTile))
        {
            return result;
        }

        if (!VerticalLinkTraversal.CanTraverseOutgoing(link, fromTile, toTile, terrain))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Warning,
                Message =
                    $"Vertical link From ({link.FromX},{link.FromY},{link.FromZ}) → To ({link.ToX},{link.ToY},{link.ToZ}) endpoints are not both walkable.",
            });
        }

        if (!link.OneWay && !VerticalLinkTraversal.CanTraverseReverse(link, toTile, fromTile, terrain))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Warning,
                Message =
                    $"Vertical link reverse To ({link.ToX},{link.ToY},{link.ToZ}) → From ({link.FromX},{link.FromY},{link.FromZ}) endpoints are not both walkable.",
            });
        }

        return result;
    }

    public static MapIntegrityResult Validate(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var result = new MapIntegrityResult();

        foreach (var link in map.VerticalLinks)
        {
            AppendLinkEndpointIssues(map, link, result);
        }

        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var slice) || slice is null || !slice.HasAnyDefinedTile())
                continue;

            if (!FloorHasVerticalExit(map, z))
            {
                result.Issues.Add(new MapIntegrityIssue
                {
                    Severity = MapIntegritySeverity.Error,
                    Message = $"Floor Z={z} has defined tiles but no vertical connection to another floor.",
                });
            }
        }

        foreach (var link in map.VerticalLinks)
        {
            if (link.FromZ == link.ToZ)
            {
                result.Issues.Add(new MapIntegrityIssue
                {
                    Severity = MapIntegritySeverity.Warning,
                    Message =
                        $"Vertical link at ({link.FromX},{link.FromY},{link.FromZ}) has same FromZ and ToZ.",
                });
            }
        }

        return result;
    }

    private static void AppendLinkEndpointIssues(WorldMap map, VerticalLink link, MapIntegrityResult result)
    {
        if (!FloorSliceExists(map, link.FromZ))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message =
                    $"Vertical link From ({link.FromX},{link.FromY},{link.FromZ}) references a floor that is not present on the map.",
            });
        }
        else if (!InBounds(map, link.FromX, link.FromY))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message =
                    $"Vertical link From ({link.FromX},{link.FromY},{link.FromZ}) is outside grid bounds X∈[{map.MinX},{map.MinX + map.Width}) Y∈[{map.MinY},{map.MinY + map.Height}).",
            });
        }

        if (!FloorSliceExists(map, link.ToZ))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message =
                    $"Vertical link To ({link.ToX},{link.ToY},{link.ToZ}) references a floor that is not present on the map.",
            });
        }
        else if (!InBounds(map, link.ToX, link.ToY))
        {
            result.Issues.Add(new MapIntegrityIssue
            {
                Severity = MapIntegritySeverity.Error,
                Message =
                    $"Vertical link To ({link.ToX},{link.ToY},{link.ToZ}) is outside grid bounds X∈[{map.MinX},{map.MinX + map.Width}) Y∈[{map.MinY},{map.MinY + map.Height}).",
            });
        }
    }

    private static bool FloorSliceExists(WorldMap map, int z) =>
        map.TryGetFloor(z, out var slice) && slice is not null;

    private static bool InBounds(WorldMap map, int x, int y) =>
        !map.IsBounded || (x >= map.MinX && x < map.MinX + map.Width && y >= map.MinY && y < map.MinY + map.Height);

    private static bool LinkTouchesCell(VerticalLink link, int x, int y, int z) =>
        (link.FromX == x && link.FromY == y && link.FromZ == z) ||
        (link.ToX == x && link.ToY == y && link.ToZ == z);

    private static TileCell SampleTileWithPending(
        WorldMap map,
        int z,
        int x,
        int y,
        int pendingZ,
        int pendingX,
        int pendingY,
        TileCell pending)
    {
        if (z == pendingZ && x == pendingX && y == pendingY)
            return pending;

        return TryGetTile(map, z, x, y, out var t) ? t : default;
    }

    private static bool TryGetTile(WorldMap map, int z, int x, int y, out TileCell tile)
    {
        tile = default;
        if (!map.TryGetFloor(z, out var floor) || floor is null)
            return false;

        return floor.TryGet(x, y, out tile);
    }

    private static bool FloorHasVerticalExit(WorldMap map, int z)
    {
        foreach (var link in map.VerticalLinks)
        {
            if (link.FromZ == z && link.ToZ != z)
                return true;
            if (link.ToZ == z && link.FromZ != z)
                return true;
        }

        return false;
    }
}
