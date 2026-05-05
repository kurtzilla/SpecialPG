namespace SpecialPG.Core.Maps;

/// <summary>
/// Core-owned actor pose and intents on a <see cref="WorldMap"/>.
/// Shell forwards input here; simulation rules stay in Core.
/// </summary>
public sealed class WorldState
{
    public WorldState(WorldMap map, int actorX, int actorY, int actorZ)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        ActorX = actorX;
        ActorY = actorY;
        ActorZ = actorZ;
        ClampActorToFloor();
    }

    public WorldMap Map { get; }

    public FogOfWarState Fog { get; } = new();

    public int ActorX { get; private set; }

    public int ActorY { get; private set; }

    public int ActorZ { get; private set; }

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
        if (!TileTraversal.IsWalkable(tile))
        {
            return false;
        }

        ActorX = nx;
        ActorY = ny;
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
            if (!VerticalLinkTraversal.CanTraverseOutgoing(outgoing, here, dest))
            {
                return false;
            }

            ActorX = outgoing.ToX;
            ActorY = outgoing.ToY;
            ActorZ = outgoing.ToZ;
            ClampActorToFloor();
            return true;
        }

        if (Map.TryGetVerticalLinkReverse(ActorX, ActorY, ActorZ, out var reverse))
        {
            if (!Map.TryGetFloor(reverse.FromZ, out var fromFloor) || fromFloor is null)
            {
                return false;
            }

            var destTile = fromFloor.Get(reverse.FromX, reverse.FromY);
            if (!VerticalLinkTraversal.CanTraverseReverse(reverse, here, destTile))
            {
                return false;
            }

            ActorX = reverse.FromX;
            ActorY = reverse.FromY;
            ActorZ = reverse.FromZ;
            ClampActorToFloor();
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
        return true;
    }

    /// <summary>Call after Shell mutates the map so the actor stays in bounds.</summary>
    public void ClampAfterShellMapMutation() => ClampActorToFloor();

    /// <summary>Shell: sync discrete actor cell from continuous world sampling (e.g. foot cell under player).</summary>
    public void SetActorCellFromShell(int x, int y, int z)
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
    }

    private void ClampActorToFloor()
    {
        if (!Map.TryGetFloor(ActorZ, out var floor) || floor is null)
        {
            return;
        }

        if (!floor.IsBounded)
            return;

        ActorX = Math.Clamp(ActorX, floor.MinX, floor.MinX + floor.Width - 1);
        ActorY = Math.Clamp(ActorY, floor.MinY, floor.MinY + floor.Height - 1);
    }
}
