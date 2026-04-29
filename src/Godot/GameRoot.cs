using Godot;
using SpecialPG.Core.Maps;
using CoreTileData = SpecialPG.Core.Maps.TileData;

/// <summary>
/// Shell entry: owns the scene tree branch that will drive rendering and input; reads Core types only via normal C# references.
/// </summary>
public partial class GameRoot : Node2D
{
    private const float TileHalfWidth = 32.0f;
    private const float TileHalfHeight = 16.0f;

    private readonly FloorSlice _floor = new(10, 8, z: 0);
    private Vector2I _cursor = new(0, 0);
    private Label _bootLabel = null!;

    public override void _Ready()
    {
        _bootLabel = GetNode<Label>("CanvasLayer/BootLabel");
        BuildSampleFloor();
        UpdateBootLabel();
        QueueRedraw();

        var sample = _floor.Get(1, 1);
        GD.Print($"[GameRoot] Ready: floor {_floor.Width}x{_floor.Height} Z={_floor.Z}, sample TileKind={sample.TileKind}.");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        var delta = Vector2I.Zero;
        if (key.PhysicalKeycode == Key.A || Input.IsActionPressed("ui_left"))
        {
            delta.X = -1;
        }
        else if (key.PhysicalKeycode == Key.D || Input.IsActionPressed("ui_right"))
        {
            delta.X = 1;
        }
        else if (key.PhysicalKeycode == Key.W || Input.IsActionPressed("ui_up"))
        {
            delta.Y = -1;
        }
        else if (key.PhysicalKeycode == Key.S || Input.IsActionPressed("ui_down"))
        {
            delta.Y = 1;
        }

        if (delta == Vector2I.Zero)
        {
            return;
        }

        MoveCursor(delta);
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        var origin = GetGridOrigin();

        for (var y = 0; y < _floor.Height; y++)
        {
            for (var x = 0; x < _floor.Width; x++)
            {
                var tile = _floor.Get(x, y);
                var center = GridToScreen(x, y, origin);
                var points = CreateDiamond(center);
                DrawPolygon(points, [TileColor(tile.TileKind)]);
                DrawPolyline(points, Colors.Black, 1.0f, true);
            }
        }

        var cursorCenter = GridToScreen(_cursor.X, _cursor.Y, origin);
        DrawCircle(cursorCenter, 8.0f, Colors.Gold);
        DrawArc(cursorCenter, 14.0f, 0.0f, Mathf.Tau, 24, Colors.Gold, 2.0f);
    }

    private void BuildSampleFloor()
    {
        for (var y = 0; y < _floor.Height; y++)
        {
            for (var x = 0; x < _floor.Width; x++)
            {
                var kind = (ushort)(((x + y) % 2) + 1);
                _floor.Set(x, y, new CoreTileData { TileKind = kind, Flags = 0, Variant = 0 });
            }
        }
    }

    private void MoveCursor(Vector2I delta)
    {
        var next = _cursor + delta;
        if (!_floor.Contains(next.X, next.Y))
        {
            return;
        }

        _cursor = next;
        UpdateBootLabel();
        QueueRedraw();
    }

    private void UpdateBootLabel()
    {
        _bootLabel.Text = $"SpecialPG prototype - Move: Arrows/WASD | Cursor: ({_cursor.X}, {_cursor.Y}, Z={_floor.Z})";
    }

    private Vector2 GetGridOrigin()
    {
        var viewportCenter = GetViewportRect().Size / 2.0f;
        // Lift the board slightly so the marker and label do not overlap.
        return viewportCenter + new Vector2(0.0f, 30.0f);
    }

    private static Vector2 GridToScreen(int x, int y, Vector2 origin)
    {
        var sx = (x - y) * TileHalfWidth;
        var sy = (x + y) * TileHalfHeight;
        return origin + new Vector2(sx, sy);
    }

    private static Vector2[] CreateDiamond(Vector2 center)
    {
        return
        [
            center + new Vector2(0.0f, -TileHalfHeight),
            center + new Vector2(TileHalfWidth, 0.0f),
            center + new Vector2(0.0f, TileHalfHeight),
            center + new Vector2(-TileHalfWidth, 0.0f)
        ];
    }

    private static Color TileColor(ushort tileKind)
    {
        return tileKind switch
        {
            1 => new Color(0.22f, 0.31f, 0.42f),
            2 => new Color(0.26f, 0.36f, 0.48f),
            _ => new Color(0.30f, 0.40f, 0.50f)
        };
    }
}
