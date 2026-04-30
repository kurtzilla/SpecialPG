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
/// </summary>
public static class MapIntegrity
{
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
        x >= map.MinX && x < map.MinX + map.Width && y >= map.MinY && y < map.MinY + map.Height;

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
