namespace SpecialPG.Core.Maps;

/// <summary>
/// Lean per-cell payload for the horizontal grid at a fixed floor <c>Z</c>.
/// Planar address <c>(X, Y)</c> and floor <c>Z</c> live on the map structure, not duplicated here.
/// See <c>docs/architecture.md</c> for axes and Active Floor rules.
/// </summary>
public readonly struct TileData
{
    /// <summary>Stable id into a tile definition table (graphics, name, etc. in Shell).</summary>
    public ushort TileKind { get; init; }

    /// <summary>Packed gameplay flags (walkable, cover, etc.)—keep in Core as numeric flags.</summary>
    public byte Flags { get; init; }

    /// <summary>Art or logic variant within the same <see cref="TileKind"/> (rotation set, damage stage).</summary>
    public byte Variant { get; init; }
}
