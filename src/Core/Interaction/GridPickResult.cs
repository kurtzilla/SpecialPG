namespace SpecialPG.Core.Interaction;

/// <summary>
/// Shell-agnostic result of picking a floor cell (e.g. from a 3D ray vs the logical floor plane).
/// Populated by the Shell; Core gameplay may consume this once rules are wired.
/// </summary>
public readonly struct GridPickResult
{
    public bool HasCell { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Z { get; init; }

    public static GridPickResult Miss => default;
}
