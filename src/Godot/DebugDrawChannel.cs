/// <summary>Shell-only debug overlay channels (multi-select via CheckButtons).</summary>
[System.Flags]
public enum DebugDrawChannel
{
    None = 0,
    Walkability = 1,
    VerticalLinks = 2,
    RayPick = 4,
    Paths = 8,
}
