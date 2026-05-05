namespace SpecialPG.Core.Maps;

/// <summary>Bitmask values for <see cref="TileCell.Flags"/>; extend as gameplay grows.</summary>
public static class TileFlags
{
    /// <summary>If set, actors cannot enter this cell.</summary>
    public const byte Blocked = 1;
}
