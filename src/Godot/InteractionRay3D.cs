#nullable enable
using Godot;
using SpecialPG.Core.Interaction;

/// <summary>
/// Thin 3D slice: orthographic camera + invisible pick volume; raycasts to populate <see cref="GridPickResult"/>.
/// See docs/architecture.md Interaction section.
/// </summary>
public partial class InteractionRay3D : Node3D
{
    private Camera3D? _camera;
    private CollisionShape3D? _shape;
    private GameRoot? _gridMap;

    public GridPickResult LastPick { get; private set; }

    public void RebuildPickGeometry() => CallDeferred(nameof(DeferredSetupPick));

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _shape = GetNode<CollisionShape3D>("PickFloor/CollisionShape3D");
        _gridMap = GetNode<GameRoot>("../GridMap");
        RebuildPickGeometry();
    }

    private void DeferredSetupPick()
    {
        if (_camera is null || _shape is null || _gridMap is null)
        {
            return;
        }

        var w = Mathf.Max(1, _gridMap.ShellMapWidth);
        var h = Mathf.Max(1, _gridMap.ShellMapHeight);
        var cs = Mathf.Max(0.01f, _gridMap.ShellCellSizePixels);
        var wPx = w * cs;
        var hPx = h * cs;
        var box = new BoxShape3D { Size = new Vector3(wPx, 0.08f, hPx) };
        _shape.Shape = box;
        _shape.Position = new Vector3(wPx * 0.5f - cs * 0.5f, 0f, hPx * 0.5f - cs * 0.5f);

        _camera.Position = new Vector3(wPx * 0.5f - cs * 0.5f, 16f, hPx * 0.5f - cs * 0.5f);
        _camera.RotationDegrees = new Vector3(-90f, 0f, 0f);
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = Mathf.Max(wPx, hPx) * 1.15f;
        // Keep 3D pick camera available for ray projection, but do not steal active view from the 2D shell camera.
        _camera.Current = false;
        _camera.Near = 0.05f;
        _camera.Far = 256f;
    }

    /// <summary>Mouse uses <c>_Input</c> so picks are not swallowed by the 2D shell before <c>_UnhandledInput</c>.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            UpdatePick(mm.Position);
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            UpdatePick(mb.Position);
        }
    }

    private void UpdatePick(Vector2 screenPosition)
    {
        if (_camera is null || _gridMap is null)
        {
            return;
        }

        var from = _camera.ProjectRayOrigin(screenPosition);
        var dir = _camera.ProjectRayNormal(screenPosition);
        var reach = Mathf.Max(_gridMap.ShellMapWidth, _gridMap.ShellMapHeight) * _gridMap.ShellCellSizePixels * 2f;
        var to = from + dir * reach;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1;
        var space = GetWorld3D().DirectSpaceState;
        var hit = space.IntersectRay(query);
        GridPickResult next;
        if (hit.Count == 0)
        {
            next = GridPickResult.Miss;
        }
        else
        {
            var pos = (Vector3)hit["position"];
            var cs = Mathf.Max(0.01f, _gridMap.ShellCellSizePixels);
            var lx = Mathf.Clamp(Mathf.FloorToInt(pos.X / cs + 1e-4f), 0, _gridMap.ShellMapWidth - 1);
            var gx = _gridMap.ShellMapMinX + lx;
            var row = Mathf.FloorToInt(pos.Z / cs + 1e-4f);
            var ly = _gridMap.ShellMapHeight - 1 - Mathf.Clamp(row, 0, _gridMap.ShellMapHeight - 1);
            var gy = _gridMap.ShellMapMinY + ly;
            next = new GridPickResult
            {
                HasCell = true,
                X = gx,
                Y = gy,
                Z = _gridMap.ShellActorZ,
            };
        }

        if (!SamePick(next, LastPick))
        {
            LastPick = next;
            _gridMap.OnRayPickUpdated();
        }
    }

    private static bool SamePick(GridPickResult a, GridPickResult b) =>
        a.HasCell == b.HasCell && a.X == b.X && a.Y == b.Y && a.Z == b.Z;
}
