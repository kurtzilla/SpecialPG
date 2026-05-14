#nullable enable
using Godot;

/// <summary>
/// Player anchor in <see cref="GameRoot"/> space. Discrete WASD updates an authoritative foot target from Core;
/// <see cref="Position"/> lerps toward that target each frame so the camera reads smoother motion without changing grid truth.
/// </summary>
public partial class ShellPlayer : Node2D
{
    /// <summary>1/e smoothing time constant in seconds toward <see cref="AuthoritativeFootWorld"/>.</summary>
    private const float FootSmoothingTauS = 0.055f;

    private Vector2 _footTargetWorld;

    /// <summary>Last foot position committed from Core (sub-cell center); use for sync to <see cref="WorldState"/>, not smoothed <see cref="Node2D.Position"/>.</summary>
    public Vector2 AuthoritativeFootWorld => _footTargetWorld;

    /// <summary>
    /// Sets the discrete foot target from Core. When <paramref name="snapImmediate"/> is true, the visual snaps (bootstrap, glide, teleport).
    /// </summary>
    public void SetFootTargetWorld(Vector2 world, bool snapImmediate)
    {
        _footTargetWorld = world;
        if (snapImmediate)
        {
            Position = world;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (dt <= 0f)
        {
            return;
        }

        // Exponential smoothing toward authoritative target (avoids overshoot vs fixed lerp factor).
        var alpha = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, FootSmoothingTauS));
        Position = Position.Lerp(_footTargetWorld, Mathf.Clamp(alpha, 0f, 1f));
    }
}
