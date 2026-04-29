namespace SpecialPG.Core.Maps;

/// <summary>
/// Horizontal tile grid at a fixed floor <see cref="Z"/>. Cell addresses are <c>(X, Y)</c> indices.
/// See <c>docs/architecture.md</c> for axis meaning; <see cref="TileData"/> omits position.
/// </summary>
public sealed class FloorSlice
{
    private readonly TileData[] _cells;

    public int Width { get; }
    public int Height { get; }
    public int Z { get; }

    public FloorSlice(int width, int height, int z)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Z = z;
        _cells = new TileData[width * height];
    }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public TileData Get(int x, int y)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");
        return _cells[Index(x, y)];
    }

    public void Set(int x, int y, TileData tile)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");
        _cells[Index(x, y)] = tile;
    }

    public bool TryGet(int x, int y, out TileData tile)
    {
        if (!Contains(x, y))
        {
            tile = default;
            return false;
        }

        tile = _cells[Index(x, y)];
        return true;
    }

    private int Index(int x, int y) => y * Width + x;
}
