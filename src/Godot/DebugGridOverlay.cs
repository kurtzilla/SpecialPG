#nullable enable
using Godot;
using SpecialPG.Core.Maps;

/// <summary>
/// On-map debug visuals: a <see cref="Node2D"/> that implements <see cref="CanvasItem._Draw"/> to paint walkability, links, ray pick, and paths
/// on top of the grid (same world coordinates as <see cref="GameRoot"/>). Screen-fixed labels (e.g. paths stub) anchor to the viewport origin.
/// </summary>
public partial class DebugGridOverlay : Node2D
{
    private DebugDrawChannel _channels = DebugDrawChannel.None;
    private GameRoot? _grid;

    public DebugDrawChannel GetChannels() => _channels;

    public void SetChannels(DebugDrawChannel channels)
    {
        _channels = channels;
        QueueRedraw();
    }

    public override void _Ready()
    {
        _grid = GetParent() as GameRoot;
        ZIndex = 10;
        Visible = false;
    }

    public override void _Draw()
    {
        if (_channels == DebugDrawChannel.None || _grid is null || !Visible)
        {
            return;
        }

        var floor = _grid.ShellGetActiveFloorSlice();

        if ((_channels & DebugDrawChannel.Walkability) != 0)
        {
            DrawWalkability(floor);
        }

        if ((_channels & DebugDrawChannel.VerticalLinks) != 0)
        {
            DrawVerticalLinks(floor);
        }

        if ((_channels & DebugDrawChannel.RayPick) != 0)
        {
            DrawRayPick(floor);
        }

        if ((_channels & DebugDrawChannel.Paths) != 0)
        {
            DrawPathsStub(floor);
        }
    }

    private void DrawWalkability(FloorSlice floor)
    {
        _grid!.ShellGetVisibleCellBounds(out var minGx, out var maxGx, out var minGy, out var maxGy);
        for (var gy = minGy; gy <= maxGy; gy++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                if (!floor.Contains(gx, gy))
                {
                    continue;
                }

                var tile = floor.Get(gx, gy);
                var rect = _grid!.ShellGetCellRect(gx, gy);
                if (!TileTraversal.IsWalkable(tile))
                {
                    DrawRect(rect, new Color(0.92f, 0.2f, 0.85f, 0.42f), true);
                    DrawRect(rect, new Color(1f, 0.4f, 0.95f, 0.95f), false, 2f);
                }
                else
                {
                    var inset = rect.Grow(-4f);
                    DrawRect(inset, new Color(0.25f, 0.75f, 0.82f, 0.55f), false, 1.5f);
                }
            }
        }
    }

    private void DrawVerticalLinks(FloorSlice floor)
    {
        var z = floor.Z;
        for (var gy = floor.MinY; gy < floor.MinY + floor.Height; gy++)
        {
            for (var gx = floor.MinX; gx < floor.MinX + floor.Width; gx++)
            {
                var hint = _grid!.ShellVerticalLinkHint(gx, gy, z);
                if (hint == GameRoot.VerticalLinkHint.None)
                {
                    continue;
                }

                var rect = _grid.ShellGetCellRect(gx, gy);
                var c = rect.GetCenter();
                var r = Mathf.Min(rect.Size.X, rect.Size.Y) * 0.32f;
                var fill = hint switch
                {
                    GameRoot.VerticalLinkHint.Both => new Color(1f, 0.55f, 0.15f, 0.75f),
                    GameRoot.VerticalLinkHint.Outgoing => new Color(0.35f, 1f, 0.45f, 0.75f),
                    _ => new Color(0.45f, 0.65f, 1f, 0.75f),
                };
                DrawCircle(c, r, fill);
                DrawArc(c, r + 1.5f, 0f, Mathf.Tau, 48, fill.Lightened(0.25f), 2f, true);
            }
        }
    }

    private void DrawRayPick(FloorSlice floor)
    {
        var main = _grid!.GetParent();
        var probe = main?.GetNodeOrNull<InteractionRay3D>("Interaction3D");
        if (probe is null)
        {
            return;
        }

        var pick = probe.LastPick;
        if (!pick.HasCell || pick.Z != floor.Z)
        {
            return;
        }

        var rect = _grid.ShellGetCellRect(pick.X, pick.Y);
        var c = rect.GetCenter();
        var r = Mathf.Min(rect.Size.X, rect.Size.Y) * 0.38f;
        DrawCircle(c, r, new Color(1f, 0.92f, 0.2f, 0.4f));
        DrawArc(c, r, 0f, Mathf.Tau, 64, Colors.Yellow, 3f, true);
    }

    private void DrawPathsStub(FloorSlice floor)
    {
        var path = _grid!.ShellDebugPlaceholderPath;
        if (path.Count >= 2 && floor.Z == 0)
        {
            var lineColor = new Color(0.95f, 0.55f, 0.2f, 0.92f);
            var knotFill = new Color(1f, 0.85f, 0.35f, 0.9f);
            for (var i = 0; i < path.Count - 1; i++)
            {
                var a = _grid.ShellGetCellRect(path[i].X, path[i].Y).GetCenter();
                var b = _grid.ShellGetCellRect(path[i + 1].X, path[i + 1].Y).GetCenter();
                DrawLine(a, b, lineColor, 3f, true);
            }

            foreach (var p in path)
            {
                var c = _grid.ShellGetCellRect(p.X, p.Y).GetCenter();
                DrawCircle(c, 4f, knotFill);
            }

            return;
        }

        var msg = path.Count >= 2
            ? "Paths: sample is on Z=0 (cycle floor to view)."
            : "Paths: (no data)";
        var anchorLocal = ViewportCanvasPointToLocal(new Vector2(8f, 8f));
        DrawString(ThemeDB.FallbackFont, anchorLocal, msg,
            HorizontalAlignment.Left, -1, 14, new Color(0.65f, 0.65f, 0.72f, 0.95f));
    }

    /// <summary>Maps a point in viewport/canvas pixels (e.g. top-left + padding) into this node's local draw space.</summary>
    private Vector2 ViewportCanvasPointToLocal(Vector2 canvasPoint) =>
        GetGlobalTransformWithCanvas().AffineInverse() * canvasPoint;
}
