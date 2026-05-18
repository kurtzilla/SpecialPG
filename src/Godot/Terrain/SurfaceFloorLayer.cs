#nullable enable
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Decor and entity layers above <see cref="TerrainFloorLayer"/>.</summary>
public partial class SurfaceFloorLayer : Node2D
{
    private DecorFloorLayer? _decorLayer;
    private EntityFloorLayer? _entityLayer;
    private int _syncedFloorZ = int.MinValue;

    public override void _Ready()
    {
        _decorLayer = new DecorFloorLayer { Name = "DecorFloorLayer" };
        _entityLayer = new EntityFloorLayer { Name = "EntityFloorLayer" };
        AddChild(_decorLayer);
        AddChild(_entityLayer);
    }

    public bool SyncVisible(
        FloorSlice floor,
        int minGx,
        int maxGx,
        int minGy,
        int maxGy,
        in SurfaceChunkRebuildContext ctx,
        int maxDecorChunkRebuildsPerCall = int.MaxValue,
        ulong bakeStartUsec = 0,
        ulong bakeTimeBudgetUsec = 0)
    {
        EnsureChildren();

        if (_syncedFloorZ != floor.Z)
        {
            ClearAll();
            _syncedFloorZ = floor.Z;
        }

        var decorPending = _decorLayer!.SyncVisible(
            floor,
            minGx,
            maxGx,
            minGy,
            maxGy,
            ctx,
            maxDecorChunkRebuildsPerCall,
            bakeStartUsec,
            bakeTimeBudgetUsec);
        _entityLayer!.SyncVisible(floor, minGx, maxGx, minGy, maxGy, ctx);
        return decorPending;
    }

    public void MarkChunkDirty(int cx, int cy)
    {
        EnsureChildren();
        _decorLayer!.MarkChunkDirty(cx, cy);
        _entityLayer!.MarkChunkDirty(cx, cy);
    }

    public void MarkAllDirty()
    {
        EnsureChildren();
        _decorLayer!.MarkAllDirty();
    }

    public void ClearAll()
    {
        _decorLayer?.ClearAll();
        _entityLayer?.ClearAll();
        _syncedFloorZ = int.MinValue;
    }

    private void EnsureChildren()
    {
        if (_decorLayer is null)
        {
            _decorLayer = GetNodeOrNull<DecorFloorLayer>("DecorFloorLayer");
            if (_decorLayer is null)
            {
                _decorLayer = new DecorFloorLayer { Name = "DecorFloorLayer" };
                AddChild(_decorLayer);
            }
        }

        if (_entityLayer is null)
        {
            _entityLayer = GetNodeOrNull<EntityFloorLayer>("EntityFloorLayer");
            if (_entityLayer is null)
            {
                _entityLayer = new EntityFloorLayer { Name = "EntityFloorLayer" };
                AddChild(_entityLayer);
            }
        }
    }
}
