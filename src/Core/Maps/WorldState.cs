namespace SpecialPG.Core.Maps;

/// <summary>
/// Core-owned actor pose and intents on a <see cref="WorldMap"/>.
/// Shell forwards input here; simulation rules stay in Core.
/// </summary>
public sealed class WorldState
{
    private TerrainEvaluator? _terrainEvaluator;

    public WorldState(WorldMap map, int actorX, int actorY, int actorZ)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Entities = new EntityStore(map);
        ActorX = actorX;
        ActorY = actorY;
        ActorZ = actorZ;
        ClampActorToFloor();
        CenterActorSub();
    }

    public WorldMap Map { get; }

    /// <summary>Registered entities (not stored in <see cref="WorldMap"/> tiles). Spatial index uses map chunk dimensions.</summary>
    public EntityStore Entities { get; }

    public FogOfWarState Fog { get; } = new();

    public int ActorX { get; private set; }

    public int ActorY { get; private set; }

    public int ActorZ { get; private set; }

    /// <summary>Eastward sub-index within the current tile; <c>0 .. SubTileGrid.Resolution-1</c>.</summary>
    public int ActorSubX { get; private set; }

    /// <summary>Northward sub-index within the current tile.</summary>
    public int ActorSubY { get; private set; }

    /// <summary>Sub-tile sampling; recreated when <see cref="WorldMap.TerrainConfig"/> changes (clear via <see cref="InvalidateTerrainEvaluator"/>).</summary>
    public TerrainEvaluator TerrainEvaluator =>
        _terrainEvaluator ??= new TerrainEvaluator(Map.TerrainConfig);

    /// <summary>Call after mutating <see cref="WorldMap.TerrainConfig"/> so sub-tile walkability matches.</summary>
    public void InvalidateTerrainEvaluator() => _terrainEvaluator = null;

    /// <summary>
    /// Fine grid step: each delta should be <c>-1</c>, <c>0</c>, or <c>1</c>. Uses <see cref="SubTileTraversal"/>.
    /// </summary>
    public bool TryStepSubTile(int dSubX, int dSubY)
    {
        if (dSubX is < -1 or > 1 || dSubY is < -1 or > 1)
            return false;

        SubTileGrid.AddSubDelta(ActorX, ActorSubX, dSubX, out var nx, out var nsx);
        SubTileGrid.AddSubDelta(ActorY, ActorSubY, dSubY, out var ny, out var nsy);

        if (!SubTileTraversal.IsWalkable(Map, ActorZ, nx, ny, nsx, nsy, TerrainEvaluator))
            return false;

        ActorX = nx;
        ActorY = ny;
        ActorSubX = nsx;
        ActorSubY = nsy;
        return true;
    }

    public bool TryMove(GridDirection direction)
    {
        var (dx, dy) = direction switch
        {
            GridDirection.North => (0, 1),
            GridDirection.South => (0, -1),
            GridDirection.East => (1, 0),
            GridDirection.West => (-1, 0),
            _ => (0, 0),
        };

        if (dx == 0 && dy == 0)
        {
            return false;
        }

        if (!Map.TryGetFloor(ActorZ, out var floor) || floor is null)
        {
            return false;
        }

        var nx = ActorX + dx;
        var ny = ActorY + dy;
        if (!floor.Contains(nx, ny))
        {
            return false;
        }

        var tile = floor.Get(nx, ny);
        if (!TileTraversal.IsWalkable(tile, Map.TerrainConfig))
        {
            return false;
        }

        ActorX = nx;
        ActorY = ny;
        CenterActorSub();
        return true;
    }

    public bool TryUseVerticalLink()
    {
        if (!Map.TryGetFloor(ActorZ, out var floor) || floor is null)
        {
            return false;
        }

        var here = floor.Get(ActorX, ActorY);

        if (Map.TryGetVerticalLinkFrom(ActorX, ActorY, ActorZ, out var outgoing))
        {
            if (!Map.TryGetFloor(outgoing.ToZ, out var toFloor) || toFloor is null)
            {
                return false;
            }

            var dest = toFloor.Get(outgoing.ToX, outgoing.ToY);
            if (!VerticalLinkTraversal.CanTraverseOutgoing(outgoing, here, dest, Map.TerrainConfig))
            {
                return false;
            }

            ActorX = outgoing.ToX;
            ActorY = outgoing.ToY;
            ActorZ = outgoing.ToZ;
            ClampActorToFloor();
            CenterActorSub();
            return true;
        }

        if (Map.TryGetVerticalLinkReverse(ActorX, ActorY, ActorZ, out var reverse))
        {
            if (!Map.TryGetFloor(reverse.FromZ, out var fromFloor) || fromFloor is null)
            {
                return false;
            }

            var destTile = fromFloor.Get(reverse.FromX, reverse.FromY);
            if (!VerticalLinkTraversal.CanTraverseReverse(reverse, here, destTile, Map.TerrainConfig))
            {
                return false;
            }

            ActorX = reverse.FromX;
            ActorY = reverse.FromY;
            ActorZ = reverse.FromZ;
            ClampActorToFloor();
            CenterActorSub();
            return true;
        }

        return false;
    }

    /// <summary>Debug / shell: jump to another present floor index without horizontal move.</summary>
    public bool TryCyclePresentFloor(int delta)
    {
        var list = Map.PresentFloorIndices();
        if (list.Count == 0)
        {
            return false;
        }

        var idx = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == ActorZ)
            {
                idx = i;
                break;
            }
        }

        idx = (idx + delta + list.Count) % list.Count;
        ActorZ = list[idx];
        ClampActorToFloor();
        CenterActorSub();
        return true;
    }

    /// <summary>Call after Shell mutates the map so the actor stays in bounds.</summary>
    public void ClampAfterShellMapMutation() => ClampActorToFloor();

    /// <summary>Shell: sync discrete actor cell from continuous world sampling (e.g. foot cell under player).</summary>
    public void SetActorCellFromShell(int x, int y, int z, int? subX = null, int? subY = null)
    {
        ActorZ = z;
        if (!Map.TryGetFloor(ActorZ, out var floor) || floor is null)
        {
            return;
        }

        if (floor.IsBounded)
        {
            ActorX = Math.Clamp(x, floor.MinX, floor.MinX + floor.Width - 1);
            ActorY = Math.Clamp(y, floor.MinY, floor.MinY + floor.Height - 1);
        }
        else
        {
            ActorX = x;
            ActorY = y;
        }

        if (subX is int sx && subY is int sy)
        {
            ActorSubX = sx;
            ActorSubY = sy;
            NormalizeActorSub();
        }
        else
            CenterActorSub();
    }

    private void ClampActorToFloor()
    {
        if (!Map.TryGetFloor(ActorZ, out var floor) || floor is null)
        {
            return;
        }

        if (!floor.IsBounded)
        {
            NormalizeActorSub();
            return;
        }

        var ox = ActorX;
        var oy = ActorY;
        ActorX = Math.Clamp(ActorX, floor.MinX, floor.MinX + floor.Width - 1);
        ActorY = Math.Clamp(ActorY, floor.MinY, floor.MinY + floor.Height - 1);
        if (ActorX != ox || ActorY != oy)
            CenterActorSub();
        else
            NormalizeActorSub();
    }

    private void CenterActorSub()
    {
        ActorSubX = SubTileGrid.CenterSub;
        ActorSubY = SubTileGrid.CenterSub;
    }

    private void NormalizeActorSub()
    {
        ActorSubX = Math.Clamp(ActorSubX, 0, SubTileGrid.Resolution - 1);
        ActorSubY = Math.Clamp(ActorSubY, 0, SubTileGrid.Resolution - 1);
    }
}
