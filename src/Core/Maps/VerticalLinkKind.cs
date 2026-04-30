namespace SpecialPG.Core.Maps;

/// <summary>Logical vertical connection between grid cells (Shell renders; Core owns rules).</summary>
public enum VerticalLinkKind : byte
{
    Stairs = 0,
    Ladder = 1,
    Elevator = 2,
    Portal = 3,
    Drop = 4,
}
