namespace SpecialPG.Core.Maps;

/// <summary>
/// Simulation-facing entity instance. <see cref="SubCellX"/> / <see cref="SubCellY"/> use <see cref="SubTileGrid"/> indices when populated.
/// </summary>
public readonly struct EntityRecord
{
    public EntityId Id { get; init; }

    /// <summary>Game-defined type id (not interpreted by Core).</summary>
    public ushort Kind { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Z { get; init; }

    public ushort Flags { get; init; }

    public byte SubCellX { get; init; }

    public byte SubCellY { get; init; }
}
