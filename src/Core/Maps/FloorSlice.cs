namespace SpecialPG.Core.Maps;

/// <summary>
/// Horizontal tile grid at a fixed floor <see cref="Z"/>. Global cell addresses are <c>(X, Y)</c> with
/// <c>MinX ≤ X &lt; MinX+Width</c>, <c>MinY ≤ Y &lt; MinY+Height</c>. Tiles are stored in sparse chunks keyed by local indices.
/// </summary>
public sealed class FloorSlice
{
    private readonly Dictionary<(int Cx, int Cy), TileData[]> _chunks = new();
    private readonly MapChunkDimensions _chunkDims;

    public int MinX { get; }
    public int MinY { get; }
    public int Width { get; }
    public int Height { get; }
    public int Z { get; }

    public int ChunkWidth { get; }
    public int ChunkHeight { get; }

    public FloorSlice(int minX, int minY, int width, int height, int z, int chunkWidth, int chunkHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkHeight);
        MinX = minX;
        MinY = minY;
        Width = width;
        Height = height;
        Z = z;
        ChunkWidth = chunkWidth;
        ChunkHeight = chunkHeight;
        _chunkDims = new MapChunkDimensions(chunkWidth, chunkHeight);
    }

    public bool Contains(int x, int y) =>
        x >= MinX && x < MinX + Width && y >= MinY && y < MinY + Height;

    public TileData Get(int x, int y)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");
        return TryGetCell(x, y, out var t) ? t : default;
    }

    public void Set(int x, int y, TileData tile)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");

        ResolveChunk(x, y, out var cx, out var cy, out var lx, out var ly);
        if (!_chunks.TryGetValue((cx, cy), out var buffer))
        {
            if (tile.TileKind == 0 && tile.Flags == 0 && tile.Variant == 0)
                return;

            buffer = AllocateChunkBuffer(cx, cy);
            _chunks[(cx, cy)] = buffer;
        }

        var idx = LocalIndex(lx, ly, cx, cy);
        buffer[idx] = tile;
    }

    public bool TryGet(int x, int y, out TileData tile)
    {
        if (!Contains(x, y))
        {
            tile = default;
            return false;
        }

        tile = TryGetCell(x, y, out var t) ? t : default;
        return true;
    }

    private bool TryGetCell(int x, int y, out TileData tile)
    {
        ResolveChunk(x, y, out var cx, out var cy, out var lx, out var ly);
        if (!_chunks.TryGetValue((cx, cy), out var buffer))
        {
            tile = default;
            return false;
        }

        tile = buffer[LocalIndex(lx, ly, cx, cy)];
        return true;
    }

    private void ResolveChunk(int x, int y, out int cx, out int cy, out int lx, out int ly)
    {
        var slx = x - MinX;
        var sly = y - MinY;
        cx = slx / ChunkWidth;
        cy = sly / ChunkHeight;
        lx = slx - cx * ChunkWidth;
        ly = sly - cy * ChunkHeight;
    }

    private TileData[] AllocateChunkBuffer(int cx, int cy)
    {
        _chunkDims.GetChunkWorldExtent(cx, cy, Width, Height, out _, out _, out var lw, out var lh);
        return new TileData[lw * lh];
    }

    private int LocalIndex(int lx, int ly, int cx, int cy)
    {
        _chunkDims.GetChunkWorldExtent(cx, cy, Width, Height, out _, out _, out var lw, out _);
        return ly * lw + lx;
    }

    /// <summary>True if any cell is non-default (see <c>docs/architecture.md</c> — “defined tile”).</summary>
    public bool HasAnyDefinedTile()
    {
        foreach (var buffer in _chunks.Values)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var t = buffer[i];
                if (t.TileKind != 0 || t.Flags != 0 || t.Variant != 0)
                    return true;
            }
        }

        return false;
    }
}
