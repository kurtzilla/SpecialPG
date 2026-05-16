#nullable enable
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Owns visible <see cref="TerrainChunkView"/> nodes for one active floor pass.</summary>
public partial class TerrainFloorLayer : Node2D
{
    private readonly Dictionary<(int Cx, int Cy), TerrainChunkView> _active = new();
    private readonly TerrainChunkPool _pool = new();
    private readonly HashSet<(int Cx, int Cy)> _dirtyChunks = new();

    public void SyncVisible(
        FloorSlice floor,
        int minGx,
        int maxGx,
        int minGy,
        int maxGy,
        in TerrainChunkRebuildContext ctx,
        System.Func<FloorSlice, int, int, Vector2> chunkTopLeftWorld)
    {
        floor.ResolveChunkCoordinates(minGx, minGy, out var cx0, out var cy0);
        floor.ResolveChunkCoordinates(maxGx, maxGy, out var cx1, out var cy1);

        if (cx0 > cx1)
        {
            (cx0, cx1) = (cx1, cx0);
        }

        if (cy0 > cy1)
        {
            (cy0, cy1) = (cy1, cy0);
        }

        var needed = new HashSet<(int Cx, int Cy)>();
        for (var cy = cy0; cy <= cy1; cy++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                needed.Add((cx, cy));
            }
        }

        var toRelease = new List<(int Cx, int Cy)>();
        foreach (var key in _active.Keys)
        {
            if (!needed.Contains(key))
            {
                toRelease.Add(key);
            }
        }

        foreach (var key in toRelease)
        {
            var view = _active[key];
            _active.Remove(key);
            _dirtyChunks.Remove(key);
            RemoveChild(view);
            _pool.Release(view);
        }

        foreach (var (cx, cy) in needed)
        {
            if (!_active.TryGetValue((cx, cy), out var view))
            {
                view = _pool.Acquire();
                view.Name = $"Chunk_{cx}_{cy}";
                view.ConfigureChunk(cx, cy);
                view.MarkDirty();
                _active[(cx, cy)] = view;
                AddChild(view);
            }

            floor.GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out _, out var lh);
            view.Position = chunkTopLeftWorld(floor, gx0, gy0 + lh - 1);
            view.Visible = true;

            if (_dirtyChunks.Contains((cx, cy)))
            {
                view.MarkDirty();
            }

            view.RebuildIfDirty(ctx);
            _dirtyChunks.Remove((cx, cy));
        }
    }

    public void MarkChunkDirty(int cx, int cy) => _dirtyChunks.Add((cx, cy));

    public void MarkAllDirty()
    {
        foreach (var key in _active.Keys)
            _dirtyChunks.Add(key);
    }

    /// <summary>Marks only active chunks in the visible cell window that contain water (cheap water-animation refresh).</summary>
    /// <returns>True when at least one visible chunk was marked dirty.</returns>
    public bool MarkWaterChunksDirtyInRange(FloorSlice floor, int minGx, int maxGx, int minGy, int maxGy)
    {
        floor.ResolveChunkCoordinates(minGx, minGy, out var cx0, out var cy0);
        floor.ResolveChunkCoordinates(maxGx, maxGy, out var cx1, out var cy1);
        if (cx0 > cx1)
            (cx0, cx1) = (cx1, cx0);
        if (cy0 > cy1)
            (cy0, cy1) = (cy1, cy0);

        var any = false;
        for (var cy = cy0; cy <= cy1; cy++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                if (_active.TryGetValue((cx, cy), out var view) && view.ContainsWater)
                {
                    _dirtyChunks.Add((cx, cy));
                    any = true;
                }
            }
        }

        return any;
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
