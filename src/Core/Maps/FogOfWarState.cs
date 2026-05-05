namespace SpecialPG.Core.Maps;

/// <summary>Per-player revealed cells per floor Z; reveal writes are grid-sampled (rect/circle).</summary>
public sealed class FogOfWarState
{
    private readonly Dictionary<(int PlayerId, int Z), HashSet<int>> _revealedLinear = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<(int X, int Y)>> _revealedGlobal = new();

    /// <summary>Linear index in local row-major order (width = map span in cells).</summary>
    public static int CellKeyFromLocal(int localX, int localY, int width) => localY * width + localX;

    private static bool IsUnboundedMap(int width, int height) => width <= 0 || height <= 0;

    public static bool TryGlobalToLocal(int x, int y, int minX, int minY, int width, int height, out int localX,
        out int localY)
    {
        localX = x - minX;
        localY = y - minY;
        if (IsUnboundedMap(width, height))
            return true;

        return (uint)localX < (uint)width && (uint)localY < (uint)height;
    }

    public bool IsRevealed(int playerId, int z, int x, int y, int minX, int minY, int width, int height)
    {
        if (!TryGlobalToLocal(x, y, minX, minY, width, height, out var lx, out var ly))
            return false;

        if (IsUnboundedMap(width, height))
            return _revealedGlobal.TryGetValue((playerId, z), out var gset) && gset.Contains((x, y));

        var key = CellKeyFromLocal(lx, ly, width);
        return _revealedLinear.TryGetValue((playerId, z), out var set) && set.Contains(key);
    }

    /// <summary>Reveal every cell in the inclusive rectangle around <paramref name="centerX"/>, <paramref name="centerY"/> clamped to the map.</summary>
    public void ApplyAxisAlignedRect(int playerId, int z, int centerX, int centerY, int halfWidthCells,
        int halfHeightCells, int minX, int minY, int width, int height)
    {
        if (IsUnboundedMap(width, height))
        {
            var gset = GetOrCreateGlobalSet(playerId, z);
            var x0 = centerX - halfWidthCells;
            var x1 = centerX + halfWidthCells;
            var y0 = centerY - halfHeightCells;
            var y1 = centerY + halfHeightCells;
            for (var yy = y0; yy <= y1; yy++)
            {
                for (var xx = x0; xx <= x1; xx++)
                    gset.Add((xx, yy));
            }

            return;
        }

        var set = GetOrCreateLinearSet(playerId, z);

        var minGx = minX;
        var maxGx = minX + width - 1;
        var minGy = minY;
        var maxGy = minY + height - 1;
        var x0b = Math.Max(minGx, centerX - halfWidthCells);
        var x1b = Math.Min(maxGx, centerX + halfWidthCells);
        var y0b = Math.Max(minGy, centerY - halfHeightCells);
        var y1b = Math.Min(maxGy, centerY + halfHeightCells);
        for (var yy = y0b; yy <= y1b; yy++)
        {
            for (var xx = x0b; xx <= x1b; xx++)
            {
                if (TryGlobalToLocal(xx, yy, minX, minY, width, height, out var lx, out var ly))
                    set.Add(CellKeyFromLocal(lx, ly, width));
            }
        }
    }

    /// <summary>
    /// Reveal every cell whose center lies inside a circle around <paramref name="centerX"/>, <paramref name="centerY"/>,
    /// clamped to map bounds.
    /// </summary>
    public void ApplyCircle(int playerId, int z, int centerX, int centerY, int radiusCells, int minX, int minY, int width,
        int height)
    {
        radiusCells = Math.Max(0, radiusCells);
        if (IsUnboundedMap(width, height))
        {
            var gset = GetOrCreateGlobalSet(playerId, z);
            var x0 = centerX - radiusCells;
            var x1 = centerX + radiusCells;
            var y0 = centerY - radiusCells;
            var y1 = centerY + radiusCells;
            var radiusSq = radiusCells * radiusCells;

            for (var yy = y0; yy <= y1; yy++)
            {
                var dy = yy - centerY;
                for (var xx = x0; xx <= x1; xx++)
                {
                    var dx = xx - centerX;
                    if (dx * dx + dy * dy > radiusSq)
                        continue;
                    gset.Add((xx, yy));
                }
            }

            return;
        }

        var set = GetOrCreateLinearSet(playerId, z);
        var minGx = minX;
        var maxGx = minX + width - 1;
        var minGy = minY;
        var maxGy = minY + height - 1;
        var x0c = Math.Max(minGx, centerX - radiusCells);
        var x1c = Math.Min(maxGx, centerX + radiusCells);
        var y0c = Math.Max(minGy, centerY - radiusCells);
        var y1c = Math.Min(maxGy, centerY + radiusCells);
        var radiusSqb = radiusCells * radiusCells;

        for (var yy = y0c; yy <= y1c; yy++)
        {
            var dy = yy - centerY;
            for (var xx = x0c; xx <= x1c; xx++)
            {
                var dx = xx - centerX;
                if (dx * dx + dy * dy > radiusSqb)
                {
                    continue;
                }

                if (TryGlobalToLocal(xx, yy, minX, minY, width, height, out var lx, out var ly))
                {
                    set.Add(CellKeyFromLocal(lx, ly, width));
                }
            }
        }
    }

    private HashSet<int> GetOrCreateLinearSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedLinear.TryGetValue(key, out var set))
            return set;

        set = new HashSet<int>();
        _revealedLinear[key] = set;
        return set;
    }

    private HashSet<(int X, int Y)> GetOrCreateGlobalSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedGlobal.TryGetValue(key, out var set))
            return set;

        set = new HashSet<(int X, int Y)>();
        _revealedGlobal[key] = set;
        return set;
    }
}
