#nullable enable
using Godot;

/// <summary>Continuous WASD movement in parent (<see cref="GameRoot"/>) space; parent clamps to walkable cells.</summary>
public partial class ShellPlayer : Node2D
{
    private GameRoot? _root;

    public override void _Ready()
    {
        _root = GetParent() as GameRoot;
        // Run before parent GameRoot physics so the camera snaps to the post-move position (default is parent-then-child).
        ProcessPhysicsPriority = -1;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_root is null)
        {
            return;
        }

        var dir = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.A))
        {
            dir.X -= 1f;
        }

        if (Input.IsPhysicalKeyPressed(Key.D))
        {
            dir.X += 1f;
        }

        if (Input.IsPhysicalKeyPressed(Key.W))
        {
            dir.Y -= 1f;
        }

        if (Input.IsPhysicalKeyPressed(Key.S))
        {
            dir.Y += 1f;
        }

        if (dir.LengthSquared() < 1e-6f)
        {
            return;
        }

        dir = dir.Normalized();
        var step = dir * (_root.MoveSpeedPxS * (float)delta);
        _root.TryApplyPlayerStep(step);
    }
}
