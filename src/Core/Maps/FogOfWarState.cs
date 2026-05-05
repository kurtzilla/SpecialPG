namespace SpecialPG.Core.Maps;

/// <summary>
/// Per-player revealed cells per floor Z; reveal writes are grid-sampled (rect/circle).
/// <see cref="ApplyCircleSubTerrainAware"/> stores land-only sub-samples so fog matches noise shorelines.
/// </summary>
public sealed class FogOfWarState
{
    private const int SubPackShift = 16;

    private readonly Dictionary<(int PlayerId, int Z), HashSet<int>> _revealedLinear = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<(int X, int Y)>> _revealedGlobal = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<long>> _revealedSubPacked = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<int>> _revealedLinearAnySubLand = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<(int X, int Y)>> _revealedGlobalAnySubLand = new();
    private readonly Dictionary<(int PlayerId, int Z), HashSet<(int X, int Y, int SubX, int SubY)>> _revealedSubGlobal =
        new();

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
        {
            if (_revealedGlobal.TryGetValue((playerId, z), out var gset) && gset.Contains((x, y)))
                return true;

            return _revealedGlobalAnySubLand.TryGetValue((playerId, z), out var gLand) && gLand.Contains((x, y));
        }

        var key = CellKeyFromLocal(lx, ly, width);
        if (_revealedLinear.TryGetValue((playerId, z), out var set) && set.Contains(key))
            return true;

        return _revealedLinearAnySubLand.TryGetValue((playerId, z), out var landCells) && landCells.Contains(key);
    }

    /// <summary>
    /// Whether a world-space point (global cell + fractional north/east) is revealed.
    /// Legacy full-cell reveals count for the whole tile; sub-terrain reveals are land-only samples.
    /// </summary>
    public bool IsWorldPointRevealed(int playerId, int z, float worldX, float worldY, int minX, int minY, int width,
        int height)
    {
        WorldFloatToSubCell(worldX, worldY, out var gx, out var gy, out var sx, out var sy);
        if (!TryGlobalToLocal(gx, gy, minX, minY, width, height, out var lx, out var ly))
            return false;

        if (IsUnboundedMap(width, height))
        {
            if (_revealedGlobal.TryGetValue((playerId, z), out var gset) && gset.Contains((gx, gy)))
                return true;

            return _revealedSubGlobal.TryGetValue((playerId, z), out var gsub) &&
                   gsub.Contains((gx, gy, sx, sy));
        }

        var cellKey = CellKeyFromLocal(lx, ly, width);
        if (_revealedLinear.TryGetValue((playerId, z), out var set) && set.Contains(cellKey))
            return true;

        var packed = PackSubKey(cellKey, sx, sy);
        return _revealedSubPacked.TryGetValue((playerId, z), out var pset) && pset.Contains(packed);
    }

    /// <summary>
    /// Reveal land (non-water) sub-samples inside a world-space circle; water stays fogged at sub resolution.
    /// Integer <see cref="ApplyCircle"/> remains for callers that want axis-aligned tile discs without terrain filtering.
    /// </summary>
    public void ApplyCircleSubTerrainAware(
        int playerId,
        int z,
        float centerWorldX,
        float centerWorldY,
        float radiusCells,
        WorldMap map,
        ITerrainEvaluator evaluator,
        int minX,
        int minY,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(evaluator);
        radiusCells = Math.Max(0f, radiusCells);
        var r2 = radiusCells * radiusCells;

        if (!map.TryGetFloor(z, out var floor) || floor is null)
            return;

        var res = SubTileGrid.Resolution;
        var minGx = (int)Math.Floor(centerWorldX - radiusCells - 1.0);
        var maxGx = (int)Math.Ceiling(centerWorldX + radiusCells + 1.0);
        var minGy = (int)Math.Floor(centerWorldY - radiusCells - 1.0);
        var maxGy = (int)Math.Ceiling(centerWorldY + radiusCells + 1.0);

        if (IsUnboundedMap(width, height))
        {
            var gsub = GetOrCreateSubGlobalSet(playerId, z);
            var gLand = GetOrCreateGlobalAnySubLandSet(playerId, z);
            for (var gy = minGy; gy <= maxGy; gy++)
            {
                for (var gx = minGx; gx <= maxGx; gx++)
                {
                    if (!floor.Contains(gx, gy))
                        continue;

                    var tile = floor.Get(gx, gy);
                    if ((tile.Flags & TileFlags.Blocked) != 0)
                        continue;
                    if (tile.Override == TerrainOverride.ForceWater)
                        continue;

                    for (var sy = 0; sy < res; sy++)
                    {
                        for (var sx = 0; sx < res; sx++)
                        {
                            var wx = SubTileTraversal.SubCellWorldX(gx, sx);
                            var wy = SubTileTraversal.SubCellWorldY(gy, sy);
                            var dx = wx - centerWorldX;
                            var dy = wy - centerWorldY;
                            if (dx * dx + dy * dy > r2)
                                continue;

                            if (!IsSubLandForFog(tile, wx, wy, evaluator))
                                continue;

                            gsub.Add((gx, gy, sx, sy));
                            gLand.Add((gx, gy));
                        }
                    }
                }
            }

            return;
        }

        var pset = GetOrCreateSubPackedSet(playerId, z);
        var landCells = GetOrCreateLinearAnySubLandSet(playerId, z);
        var minGxB = minX;
        var maxGxB = minX + width - 1;
        var minGyB = minY;
        var maxGyB = minY + height - 1;
        for (var gy = Math.Max(minGyB, minGy); gy <= Math.Min(maxGyB, maxGy); gy++)
        {
            for (var gx = Math.Max(minGxB, minGx); gx <= Math.Min(maxGxB, maxGx); gx++)
            {
                if (!TryGlobalToLocal(gx, gy, minX, minY, width, height, out var lx, out var ly))
                    continue;

                var tile = floor.Get(gx, gy);
                if ((tile.Flags & TileFlags.Blocked) != 0)
                    continue;
                if (tile.Override == TerrainOverride.ForceWater)
                    continue;

                var cellKey = CellKeyFromLocal(lx, ly, width);
                for (var sy = 0; sy < res; sy++)
                {
                    for (var sx = 0; sx < res; sx++)
                    {
                        var wx = SubTileTraversal.SubCellWorldX(gx, sx);
                        var wy = SubTileTraversal.SubCellWorldY(gy, sy);
                        var dx = wx - centerWorldX;
                        var dy = wy - centerWorldY;
                        if (dx * dx + dy * dy > r2)
                            continue;

                        if (!IsSubLandForFog(tile, wx, wy, evaluator))
                            continue;

                        pset.Add(PackSubKey(cellKey, sx, sy));
                        landCells.Add(cellKey);
                    }
                }
            }
        }
    }

    private static void WorldFloatToSubCell(float worldX, float worldY, out int gx, out int gy, out int sx, out int sy)
    {
        gx = (int)Math.Floor(worldX);
        gy = (int)Math.Floor(worldY);
        var fx = worldX - gx;
        var fy = worldY - gy;
        var res = SubTileGrid.Resolution;
        sx = Math.Clamp((int)Math.Floor(fx * res - 1e-5f), 0, res - 1);
        sy = Math.Clamp((int)Math.Floor(fy * res - 1e-5f), 0, res - 1);
    }

    private static long PackSubKey(int localCellKey, int subX, int subY) =>
        ((long)localCellKey << SubPackShift) | (uint)(subY * SubTileGrid.Resolution + subX);

    private static bool IsSubLandForFog(TileCell tile, float worldX, float worldY, ITerrainEvaluator evaluator)
    {
        if (tile.Override == TerrainOverride.ForceLand)
            return true;

        var sample = evaluator.EvaluateAt(worldX, worldY);
        return !evaluator.IsWater(sample);
    }

    private HashSet<long> GetOrCreateSubPackedSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedSubPacked.TryGetValue(key, out var set))
            return set;

        set = new HashSet<long>();
        _revealedSubPacked[key] = set;
        return set;
    }

    private HashSet<int> GetOrCreateLinearAnySubLandSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedLinearAnySubLand.TryGetValue(key, out var set))
            return set;

        set = new HashSet<int>();
        _revealedLinearAnySubLand[key] = set;
        return set;
    }

    private HashSet<(int X, int Y)> GetOrCreateGlobalAnySubLandSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedGlobalAnySubLand.TryGetValue(key, out var set))
            return set;

        set = new HashSet<(int X, int Y)>();
        _revealedGlobalAnySubLand[key] = set;
        return set;
    }

    private HashSet<(int X, int Y, int SubX, int SubY)> GetOrCreateSubGlobalSet(int playerId, int z)
    {
        var key = (playerId, z);
        if (_revealedSubGlobal.TryGetValue(key, out var set))
            return set;

        set = new HashSet<(int X, int Y, int SubX, int SubY)>();
        _revealedSubGlobal[key] = set;
        return set;
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
