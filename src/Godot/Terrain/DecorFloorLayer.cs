#nullable enable
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Chunk-scoped procedural decor above terrain.</summary>
public partial class DecorFloorLayer : Node2D
{
    private readonly Dictionary<(int Cx, int Cy), DecorChunkView> _active = new();
    private readonly DecorChunkPool _pool = new();
    private readonly HashSet<(int Cx, int Cy)> _dirtyChunks = new();

    /// <inheritdoc cref="TerrainFloorLayer.SyncVisible"/>
    public bool SyncVisible(
        FloorSlice floor,
        int minGx,
        int maxGx,
        int minGy,
        int maxGy,
        in SurfaceChunkRebuildContext ctx,
        int maxChunkRebuildsPerCall = int.MaxValue,
        ulong bakeStartUsec = 0,
        ulong bakeTimeBudgetUsec = 0)
    {
        floor.ResolveChunkCoordinates(minGx, minGy, out var cx0, out var cy0);
        floor.ResolveChunkCoordinates(maxGx, maxGy, out var cx1, out var cy1);
        if (cx0 > cx1)
            (cx0, cx1) = (cx1, cx0);
        if (cy0 > cy1)
            (cy0, cy1) = (cy1, cy0);

        var needed = new HashSet<(int Cx, int Cy)>();
        for (var cy = cy0; cy <= cy1; cy++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
                needed.Add((cx, cy));
        }

        var centerCx = (cx0 + cx1) / 2;
        var centerCy = (cy0 + cy1) / 2;
        var neededOrdered = new List<(int Cx, int Cy)>(needed);
        neededOrdered.Sort((a, b) =>
        {
            var da = (a.Cx - centerCx) * (a.Cx - centerCx) + (a.Cy - centerCy) * (a.Cy - centerCy);
            var db = (b.Cx - centerCx) * (b.Cx - centerCx) + (b.Cy - centerCy) * (b.Cy - centerCy);
            return da.CompareTo(db);
        });

        var toRelease = new List<(int Cx, int Cy)>();
        foreach (var key in _active.Keys)
        {
            if (!needed.Contains(key))
                toRelease.Add(key);
        }

        foreach (var key in toRelease)
        {
            var view = _active[key];
            _active.Remove(key);
            _dirtyChunks.Remove(key);
            RemoveChild(view);
            _pool.Release(view);
        }

        var rebuilds = 0;
        var unlimited = maxChunkRebuildsPerCall <= 0 || maxChunkRebuildsPerCall == int.MaxValue;

        foreach (var (cx, cy) in neededOrdered)
        {
            if (!_active.TryGetValue((cx, cy), out var view))
            {
                view = _pool.Acquire();
                view.Name = $"Decor_{cx}_{cy}";
                view.ConfigureChunk(cx, cy);
                view.MarkDirty();
                _active[(cx, cy)] = view;
                AddChild(view);
            }

            floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out _, out var lh);
            view.Position = ctx.ChunkNorthWestCornerWorld(floor, gx0, gy0 + lh - 1);
            view.Visible = true;

            if (_dirtyChunks.Contains((cx, cy)))
                view.MarkDirty();

            if (view.NeedsRebuild(ctx))
            {
                var overCountBudget = !unlimited && rebuilds >= maxChunkRebuildsPerCall;
                var overTimeBudget = bakeTimeBudgetUsec > 0
                                     && bakeStartUsec > 0
                                     && Time.GetTicksUsec() - bakeStartUsec >= bakeTimeBudgetUsec;
                if (!overCountBudget && !overTimeBudget)
                {
                    view.RebuildIfDirty(ctx);
                    rebuilds++;
                    _dirtyChunks.Remove((cx, cy));
                }
                else
                {
                    view.MarkDirty();
                    _dirtyChunks.Add((cx, cy));
                }
            }
            else
            {
                _dirtyChunks.Remove((cx, cy));
            }
        }

        foreach (var key in neededOrdered)
        {
            if (_dirtyChunks.Contains(key))
            {
                return true;
            }

            if (_active.TryGetValue(key, out var pendingView) && pendingView.NeedsRebuild(ctx))
            {
                return true;
            }
        }

        return false;
    }

    public void MarkChunkDirty(int cx, int cy) => _dirtyChunks.Add((cx, cy));

    public void MarkAllDirty()
    {
        foreach (var key in _active.Keys)
            _dirtyChunks.Add(key);
    }

    public void ClearAll()
    {
        _dirtyChunks.Clear();
        var keys = new List<(int Cx, int Cy)>(_active.Keys);
        foreach (var key in keys)
        {
            var view = _active[key];
            _active.Remove(key);
            RemoveChild(view);
            _pool.Release(view);
        }
    }
}
