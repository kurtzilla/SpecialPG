#nullable enable
using Godot;

/// <summary>
/// Player anchor in <see cref="GameRoot"/> space. Movement is discrete WASD in <see cref="GameRoot"/> via
/// <see cref="WorldState.TryStepSubTile"/>; position is updated from Core after each step.
/// </summary>
public partial class ShellPlayer : Node2D
{
}
