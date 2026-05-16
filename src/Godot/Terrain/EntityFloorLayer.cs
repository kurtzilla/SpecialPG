#nullable enable
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Renders <see cref="EntityStore"/> props on the active floor above decor.</summary>
public partial class EntityFloorLayer : Node2D
{
    private readonly Dictionary<EntityId, EntityView> _active = new();
    private readonly EntityViewPool _pool = new();
    private readonly List<EntityId> _scratchIds = new();
    private readonly List<EntityRecord> _scratchRecords = new();

    public void SyncVisible(
        FloorSlice floor,
        int minGx,
        int maxGx,
        int minGy,
        int maxGy,
        in SurfaceChunkRebuildContext ctx)
    {
        floor.ResolveChunkCoordinates(minGx, minGy, out var cx0, out var cy0);
        floor.ResolveChunkCoordinates(maxGx, maxGy, out var cx1, out var cy1);
        if (cx0 > cx1)
            (cx0, cx1) = (cx1, cx0);
        if (cy0 > cy1)
            (cy0, cy1) = (cy1, cy0);

        var needed = new HashSet<EntityId>();
        _scratchRecords.Clear();
        for (var cy = cy0; cy <= cy1; cy++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                _scratchIds.Clear();
                ctx.Entities.AppendEntitiesInChunk(cx, cy, ctx.ActorZ, _scratchIds);
                for (var i = 0; i < _scratchIds.Count; i++)
                {
                    var id = _scratchIds[i];
                    if (id.IsNone || !ctx.Entities.TryGet(id, out var record))
                        continue;
                    if (record.Kind == EntityKinds.Actor)
                        continue;
                    if (!needed.Add(id))
                        continue;
                    _scratchRecords.Add(record);
                }
            }
        }

        _scratchRecords.Sort(static (a, b) =>
        {
            var c = a.Y.CompareTo(b.Y);
            return c != 0 ? c : a.X.CompareTo(b.X);
        });

        var toRelease = new List<EntityId>();
        foreach (var (id, view) in _active)
        {
            if (!needed.Contains(id))
                toRelease.Add(id);
        }

        foreach (var id in toRelease)
        {
            var view = _active[id];
            _active.Remove(id);
            RemoveChild(view);
            _pool.Release(view);
        }

        foreach (var record in _scratchRecords)
        {
            if (!_active.TryGetValue(record.Id, out var view))
            {
                view = _pool.Acquire();
                view.Name = $"Ent_{record.Id.Value}";
                _active[record.Id] = view;
                AddChild(view);
            }

            view.Configure(record, ctx);
        }
    }

    public void MarkChunkDirty(int cx, int cy)
    {
        // Full resync on next draw is cheap enough for v1; chunk dirty marks all entities in chunk.
        _ = cx;
        _ = cy;
    }

    public void ClearAll()
    {
        var ids = new List<EntityId>(_active.Keys);
        foreach (var id in ids)
        {
            var view = _active[id];
            _active.Remove(id);
            RemoveChild(view);
            _pool.Release(view);
        }
    }
}
