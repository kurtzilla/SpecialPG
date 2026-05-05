namespace SpecialPG.Core.Maps;

/// <summary>
/// Factorio-style entity registry: dense records by id plus chunk-coordinate buckets <c>(cx, cy, z)</c> for spatial queries.
/// Chunk grid matches <see cref="WorldMap"/> / <see cref="FloorSlice"/> (same <see cref="WorldMap.MinX"/>, <see cref="WorldMap.ChunkWidth"/>, bounded vs unbounded division).
/// </summary>
public sealed class EntityStore
{
    private readonly WorldMap _map;
    private ulong _nextId = 1;
    private readonly Dictionary<EntityId, EntityRecord> _byId = new();
    private readonly Dictionary<(int Cx, int Cy, int Z), List<EntityId>> _byChunk = new();

    public EntityStore(WorldMap map) =>
        _map = map ?? throw new ArgumentNullException(nameof(map));

    public int Count => _byId.Count;

    public EntityId Spawn(ushort kind, int x, int y, int z, ushort flags = 0, byte subCellX = 0, byte subCellY = 0)
    {
        var id = new EntityId(_nextId++);
        var record = new EntityRecord
        {
            Id = id,
            Kind = kind,
            X = x,
            Y = y,
            Z = z,
            Flags = flags,
            SubCellX = subCellX,
            SubCellY = subCellY,
        };
        _byId[id] = record;
        AddToChunkIndex(id, record);
        return id;
    }

    public bool Destroy(EntityId id)
    {
        if (!_byId.Remove(id, out var prev))
            return false;
        RemoveFromChunkIndex(id, prev);
        return true;
    }

    public bool TryGet(EntityId id, out EntityRecord record) => _byId.TryGetValue(id, out record);

    public bool TrySetCell(EntityId id, int x, int y, int z)
    {
        if (!_byId.TryGetValue(id, out var prev))
            return false;
        RemoveFromChunkIndex(id, prev);
        var next = prev with { X = x, Y = y, Z = z };
        _byId[id] = next;
        AddToChunkIndex(id, next);
        return true;
    }

    /// <summary>Appends every entity id in the horizontal chunk <paramref name="cx"/>, <paramref name="cy"/> on floor <paramref name="z"/>.</summary>
    public void AppendEntitiesInChunk(int cx, int cy, int z, List<EntityId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!_byChunk.TryGetValue((cx, cy, z), out var list))
            return;
        for (var i = 0; i < list.Count; i++)
            destination.Add(list[i]);
    }

    /// <summary>
    /// Appends ids for entities whose footprint includes the tile <c>(x, y, z)</c>. v1: exact tile match only.
    /// </summary>
    public void AppendEntitiesOverlappingCell(int x, int y, int z, List<EntityId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ResolveChunkIndices(x, y, out var cx, out var cy);
        if (!_byChunk.TryGetValue((cx, cy, z), out var list))
            return;
        for (var i = 0; i < list.Count; i++)
        {
            var id = list[i];
            if (_byId.TryGetValue(id, out var r) && r.X == x && r.Y == y && r.Z == z)
                destination.Add(id);
        }
    }

    /// <summary>All entities whose tile lies inside the inclusive world rectangle on one floor.</summary>
    public void AppendEntitiesInWorldRect(int minX, int minY, int maxX, int maxY, int z, List<EntityId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (maxX < minX || maxY < minY)
            return;

        ResolveChunkIndices(minX, minY, out var cx0, out var cy0);
        ResolveChunkIndices(maxX, maxY, out var cx1, out var cy1);
        if (cx0 > cx1)
            (cx0, cx1) = (cx1, cx0);
        if (cy0 > cy1)
            (cy0, cy1) = (cy1, cy0);

        for (var cy = cy0; cy <= cy1; cy++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                if (!_byChunk.TryGetValue((cx, cy, z), out var list))
                    continue;
                for (var i = 0; i < list.Count; i++)
                {
                    var id = list[i];
                    if (!_byId.TryGetValue(id, out var r) || r.Z != z)
                        continue;
                    if (r.X >= minX && r.X <= maxX && r.Y >= minY && r.Y <= maxY)
                        destination.Add(id);
                }
            }
        }
    }

    public void Clear()
    {
        _byId.Clear();
        _byChunk.Clear();
        _nextId = 1;
    }

    /// <summary>Replaces the store contents (e.g. after load). Preserves <see cref="EntityRecord.Id"/> values and rebuilds the spatial index.</summary>
    public void ReplaceAllRecords(IReadOnlyList<EntityRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        Clear();
        var max = 0UL;
        foreach (var r in records)
        {
            if (r.Id.IsNone)
                continue;
            _byId[r.Id] = r;
            AddToChunkIndex(r.Id, r);
            if (r.Id.Value > max)
                max = r.Id.Value;
        }

        _nextId = max + 1;
    }

    /// <summary>Copy of all records sorted by <see cref="EntityId"/> for deterministic serialization.</summary>
    public List<EntityRecord> CloneAllRecordsSortedById()
    {
        var list = new List<EntityRecord>(_byId.Count);
        foreach (var r in _byId.Values)
            list.Add(r);
        list.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return list;
    }

    private void ResolveChunkIndices(int x, int y, out int cx, out int cy)
    {
        var slx = x - _map.MinX;
        var sly = y - _map.MinY;
        var cw = _map.ChunkWidth;
        var ch = _map.ChunkHeight;
        if (_map.IsBounded)
        {
            cx = slx / cw;
            cy = sly / ch;
        }
        else
        {
            cx = FloorDiv(slx, cw);
            cy = FloorDiv(sly, ch);
        }
    }

    private static int FloorDiv(int n, int d)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(d);
        if (n >= 0)
            return n / d;
        return (n + 1) / d - 1;
    }

    private void AddToChunkIndex(EntityId id, EntityRecord r)
    {
        ResolveChunkIndices(r.X, r.Y, out var cx, out var cy);
        var key = (cx, cy, r.Z);
        if (!_byChunk.TryGetValue(key, out var list))
        {
            list = new List<EntityId>(4);
            _byChunk[key] = list;
        }

        list.Add(id);
    }

    private void RemoveFromChunkIndex(EntityId id, EntityRecord r)
    {
        ResolveChunkIndices(r.X, r.Y, out var cx, out var cy);
        var key = (cx, cy, r.Z);
        if (!_byChunk.TryGetValue(key, out var list))
            return;
        RemoveIdFromList(list, id);
        if (list.Count == 0)
            _byChunk.Remove(key);
    }

    private static void RemoveIdFromList(List<EntityId> list, EntityId id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] != id)
                continue;
            list[i] = list[^1];
            list.RemoveAt(list.Count - 1);
            return;
        }
    }
}
