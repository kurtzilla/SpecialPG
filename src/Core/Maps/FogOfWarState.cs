namespace SpecialPG.Core.Maps;

/// <summary>Per-player revealed cells per floor Z; reveal writes are grid-sampled (rect/circle).</summary>
public sealed class FogOfWarState
{
    private readonly Dictionary<(int PlayerId, int Z), HashSet<int>> _revealed = new();

    /// <summary>Linear index in local row-major order (width = map span in cells).</summary>
    public static int CellKeyFromLocal(int localX, int localY, int width) => localY * width + localX;

    public static bool TryGlobalToLocal(int x, int y, int minX, int minY, int width, int height, out int localX,
        out int localY)
    {
        localX = x - minX;
        localY = y - minY;
        return (uint)localX < (uint)width && (uint)localY < (uint)height;
    }

    public bool IsRevealed(int playerId, int z, int x, int y, int minX, int minY, int width, int height)
    {
        if (!TryGlobalToLocal(x, y, minX, minY, width, height, out var lx, out var ly))
            return false;

        var key = CellKeyFromLocal(lx, ly, width);
        return _revealed.TryGetValue((playerId, z), out var set) && set.Contains(key);
    }

    /// <summary>Reveal every cell in the inclusive rectangle around <paramref name="centerX"/>, <paramref name="centerY"/> clamped to the map.</summary>
    public void ApplyAxisAlignedRect(int playerId, int z, int centerX, int centerY, int halfWidthCells,
        int halfHeightCells, int minX, int minY, int width, int height)
    {
        var set = GetOrCreateRevealSet(playerId, z);

        var minGx = minX;
        var maxGx = minX + width - 1;
        var minGy = minY;
        var maxGy = minY + height - 1;
        var x0 = Math.Max(minGx, centerX - halfWidthCells);
        var x1 = Math.Min(maxGx, centerX + halfWidthCells);
        var y0 = Math.Max(minGy, centerY - halfHeightCells);
        var y1 = Math.Min(maxGy, centerY + halfHeightCells);
        for (var yy = y0; yy <= y1; yy++)
        {
            for (var xx = x0; xx <= x1; xx++)
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
        var set = GetOrCreateRevealSet(playerId, z);
        var minGx = minX;
        var maxGx = minX + width - 1;
        var minGy = minY;
        var maxGy = minY + height - 1;
        var x0 = Math.Max(minGx, centerX - radiusCells);
        var x1 = Math.Min(maxGx, centerX + radiusCells);
        var y0 = Math.Max(minGy, centerY - radiusCells);
        var y1 = Math.Min(maxGy, centerY + radiusCells);
        var radiusSq = radiusCells * radiusCells;

        for (var yy = y0; yy <= y1; yy++)
        {
            var dy = yy - centerY;
            for (var xx = x0; xx <= x1; xx++)
            {
                var dx = xx - centerX;
                if (dx * dx + dy * dy > radiusSq)
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

    private HashSet<int> GetOrCreateRevealSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealed.TryGetValue(key, out var set))
        {
            return set;
        }

        set = new HashSet<int>();
        _revealed[key] = set;
        return set;
    }
}
