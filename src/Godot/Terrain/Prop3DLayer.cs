#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;
using SpecialPG.Core.Maps.Rendering;

namespace SpecialPG;

/// <summary>Hybrid melange: Kenney GLB props on the 3D pick plane above the 2D terrain.</summary>
public partial class Prop3DLayer : Node3D
{
    private const int MaxPropsPerChunk = 48;

    private readonly Dictionary<(int Cx, int Cy), List<Node3D>> _chunkProps = new();
    private readonly List<DecorCell> _decorScratch = new();

    public void ClearAll()
    {
        foreach (var list in _chunkProps.Values)
        {
            foreach (var n in list)
                n.QueueFree();
        }

        _chunkProps.Clear();
    }

    public void SyncVisible(
        FloorSlice floor,
        int minGx,
        int maxGx,
        int minGy,
        int maxGy,
        bool enabled,
        Prop3DCatalog catalog,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        float cellSizePx,
        Func<float, float, Vector3> cellCenterToPickWorld)
    {
        if (!enabled || !catalog.IsLoaded)
        {
            ClearAll();
            return;
        }

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

        var toRemove = new List<(int Cx, int Cy)>();
        foreach (var key in _chunkProps.Keys)
        {
            if (!needed.Contains(key))
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
        {
            foreach (var n in _chunkProps[key])
                n.QueueFree();
            _chunkProps.Remove(key);
        }

        foreach (var (cx, cy) in needed)
        {
            if (_chunkProps.ContainsKey((cx, cy)))
                continue;

            var nodes = BuildChunkProps(
                floor, cx, cy, catalog, evaluator, terrain, worldSeed, cellSizePx, cellCenterToPickWorld);
            _chunkProps[(cx, cy)] = nodes;
            foreach (var n in nodes)
                AddChild(n);
        }
    }

    private List<Node3D> BuildChunkProps(
        FloorSlice floor,
        int cx,
        int cy,
        Prop3DCatalog catalog,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        float cellSizePx,
        Func<float, float, Vector3> cellCenterToPickWorld)
    {
        var result = new List<Node3D>();
        DecorScatterPlanner.PlanChunk(floor, cx, cy, evaluator, terrain, worldSeed, _decorScratch);
        var count = 0;
        foreach (var cell in _decorScratch)
        {
            if (count >= MaxPropsPerChunk)
                break;
            if (!catalog.TryGetForDecorVariant(cell.VariantIndex, out var entry))
                continue;

            var packed = ResourceLoader.Load<PackedScene>(entry.ResourcePath);
            if (packed is null)
            {
                GD.PushWarning($"[Prop3DLayer] Failed to load {entry.ResourcePath}");
                continue;
            }

            var inst = packed.Instantiate<Node3D>();
            if (inst is null)
                continue;

            var pos = cellCenterToPickWorld(cell.Gx + 0.5f, cell.Gy + 0.5f);
            pos.Y = entry.YOffset;
            inst.Position = pos;
            var s = entry.Scale * (cellSizePx / 64f);
            inst.Scale = new Vector3(s, s, s);
            result.Add(inst);
            count++;
        }

        return result;
    }
}
