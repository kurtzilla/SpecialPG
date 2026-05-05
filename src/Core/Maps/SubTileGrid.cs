namespace SpecialPG.Core.Maps;

/// <summary>
/// Integer sub-cells per tile axis for fine movement (Milestone 4). Indices are <c>0 .. Resolution-1</c>;
/// <see cref="SubCellX"/>/<see cref="SubCellY"/> increase east / north within the tile.
/// </summary>
public static class SubTileGrid
{
    /// <summary>Subdivisions per tile edge (power of two recommended).</summary>
    public const int Resolution = 16;

    /// <summary>Default spawn / tile-step alignment (tile center).</summary>
    public const int CenterSub = Resolution / 2;

    public static void AddSubDelta(int tile, int sub, int delta, out int newTile, out int newSub) =>
        AddSubDelta(tile, sub, delta, Resolution, out newTile, out newSub);

    public static void AddSubDelta(int tile, int sub, int delta, int resolution, out int newTile, out int newSub)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolution);
        var sum = sub + delta;
        newTile = tile;
        newSub = sum;
        while (newSub >= resolution)
        {
            newSub -= resolution;
            newTile++;
        }

        while (newSub < 0)
        {
            newSub += resolution;
            newTile--;
        }
    }

    public static bool IsValidSub(int sub) => (uint)sub < (uint)Resolution;
}
