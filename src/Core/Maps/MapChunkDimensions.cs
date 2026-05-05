namespace SpecialPG.Core.Maps;

/// <summary>
/// Horizontal partition of a floor into rectangular chunks (Factorio-style default 32×32).
/// Map edges may use smaller effective chunk sizes without padding.
/// </summary>
public readonly struct MapChunkDimensions : IEquatable<MapChunkDimensions>
{
    /// <summary>Factorio map chunk width in tiles.</summary>
    public const int DefaultWidth = 32;

    /// <summary>Factorio map chunk height in tiles.</summary>
    public const int DefaultHeight = 32;

    public MapChunkDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public static MapChunkDimensions Default => new(DefaultWidth, DefaultHeight);

    /// <summary>Number of chunk columns covering <paramref name="mapWidth"/>.</summary>
    public int GetChunkCountX(int mapWidth) => (mapWidth + Width - 1) / Width;

    /// <summary>Number of chunk rows covering <paramref name="mapHeight"/>.</summary>
    public int GetChunkCountY(int mapHeight) => (mapHeight + Height - 1) / Height;

    /// <summary>World-space origin and size in cells for chunk <paramref name="chunkX"/>, <paramref name="chunkY"/>.</summary>
    public void GetChunkWorldExtent(int chunkX, int chunkY, int mapWidth, int mapHeight, out int originX,
        out int originY, out int localWidth, out int localHeight)
    {
        originX = chunkX * Width;
        originY = chunkY * Height;
        localWidth = Math.Min(Width, mapWidth - originX);
        localHeight = Math.Min(Height, mapHeight - originY);
    }

    /// <summary>
    /// Full chunk rectangle in local map coordinates (no map edge clipping). Use for unbounded / streaming floors
    /// where every chunk is exactly <see cref="Width"/>×<see cref="Height"/> cells.
    /// </summary>
    public void GetChunkWorldExtentUnbounded(int chunkX, int chunkY, out int originX, out int originY,
        out int localWidth, out int localHeight)
    {
        originX = chunkX * Width;
        originY = chunkY * Height;
        localWidth = Width;
        localHeight = Height;
    }

    public bool Equals(MapChunkDimensions other) => Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is MapChunkDimensions other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height);
}
