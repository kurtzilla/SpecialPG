namespace SpecialPG.Core.Maps;

/// <summary>
/// Horizontal tile grid at a fixed floor <see cref="Z"/>. Global cell addresses are <c>(X, Y)</c> with
/// <c>MinX ≤ X &lt; MinX+Width</c>, <c>MinY ≤ Y &lt; MinY+Height</c> when <see cref="IsBounded"/>; otherwise any integer
/// coordinate is addressable. Tiles are stored in sparse chunks keyed by chunk indices (supports negative chunks).
/// </summary>
public sealed class FloorSlice
{
    private readonly Dictionary<(int Cx, int Cy), TileCell[]> _chunks = new();
    private readonly HashSet<(int Cx, int Cy)> _modifiedChunks = new();
    private readonly MapChunkDimensions _chunkDims;

    public int MinX { get; }
    public int MinY { get; }
    public int Width { get; }
    public int Height { get; }
    public int Z { get; }

    public int ChunkWidth { get; }
    public int ChunkHeight { get; }

    /// <summary>False for streaming/unbounded maps: <see cref="Width"/> and <see cref="Height"/> are zero and ignored.</summary>
    public bool IsBounded { get; }

    /// <summary>
    /// When set on an unbounded slice, the first read or write in a chunk allocates it and fills all cells from noise
    /// (authoritative thereafter). Bounded slices ignore this for automatic fill (use <see cref="ProceduralWorldMapGenerator"/>).
    /// </summary>
    public ITerrainEvaluator? TerrainEvaluator { get; set; }

    /// <summary>
    /// When true, <see cref="Set"/> does not record chunk keys for <see cref="ModifiedChunkCount"/> (bulk map build / JSON hydrate).
    /// </summary>
    public bool SuppressChunkModificationTracking { get; set; }

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
        IsBounded = true;
        _chunkDims = new MapChunkDimensions(chunkWidth, chunkHeight);
    }

    /// <summary>Unbounded floor: any <c>(x,y)</c> is valid; only loaded chunks hold non-default tiles until generation runs.</summary>
    public FloorSlice(int minX, int minY, int z, int chunkWidth, int chunkHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkHeight);
        MinX = minX;
        MinY = minY;
        Width = 0;
        Height = 0;
        Z = z;
        ChunkWidth = chunkWidth;
        ChunkHeight = chunkHeight;
        IsBounded = false;
        _chunkDims = new MapChunkDimensions(chunkWidth, chunkHeight);
    }

    public bool Contains(int x, int y) =>
        !IsBounded || (x >= MinX && x < MinX + Width && y >= MinY && y < MinY + Height);

    public TileCell Get(int x, int y)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");
        return TryGetCell(x, y, out var t) ? t : default;
    }

    public void Set(int x, int y, TileCell tile)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates are out of range for this floor.");

        if (tile.IsEmpty)
            return;

        ResolveChunk(x, y, out var cx, out var cy, out var lx, out var ly);
        if (!_chunks.TryGetValue((cx, cy), out var buffer))
        {
            buffer = AllocateChunkBuffer(cx, cy);
            _chunks[(cx, cy)] = buffer;
            if (!IsBounded && TerrainEvaluator is not null)
                FillChunkBufferFromEvaluator(cx, cy, buffer, TerrainEvaluator);
        }

        var idx = LocalIndex(lx, ly, cx, cy);
        buffer[idx] = tile;
        if (!SuppressChunkModificationTracking)
            _modifiedChunks.Add((cx, cy));
    }

    public bool TryGet(int x, int y, out TileCell tile)
    {
        if (!Contains(x, y))
        {
            tile = default;
            return false;
        }

        tile = TryGetCell(x, y, out var t) ? t : default;
        return true;
    }

    private bool TryGetCell(int x, int y, out TileCell tile)
    {
        ResolveChunk(x, y, out var cx, out var cy, out var lx, out var ly);
        if (!_chunks.TryGetValue((cx, cy), out var buffer))
        {
            if (!IsBounded && TerrainEvaluator is not null)
            {
                MaterializeChunkIfNeeded(cx, cy);
                buffer = _chunks[(cx, cy)];
            }
            else
            {
                tile = default;
                return false;
            }
        }

        tile = buffer[LocalIndex(lx, ly, cx, cy)];
        return true;
    }

    /// <summary>For unbounded floors with <see cref="TerrainEvaluator"/> set, allocates and noise-fills the chunk if missing.</summary>
    public void EnsureChunkMaterialized(int cx, int cy)
    {
        if (IsBounded || TerrainEvaluator is null)
            return;
        MaterializeChunkIfNeeded(cx, cy);
    }

    private void MaterializeChunkIfNeeded(int cx, int cy)
    {
        if (_chunks.ContainsKey((cx, cy)))
            return;
        var eval = TerrainEvaluator;
        if (eval is null)
            return;
        var buffer = AllocateChunkBuffer(cx, cy);
        FillChunkBufferFromEvaluator(cx, cy, buffer, eval);
        _chunks[(cx, cy)] = buffer;
    }

    private void FillChunkBufferFromEvaluator(int cx, int cy, TileCell[] buffer, ITerrainEvaluator eval)
    {
        GetChunkWorldCellRange(cx, cy, out var gx0, out var gy0, out var lw, out var lh);
        for (var ly = 0; ly < lh; ly++)
        {
            for (var lx = 0; lx < lw; lx++)
            {
                var gx = gx0 + lx;
                var gy = gy0 + ly;
                var sample = eval.EvaluateAt(gx, gy);
                buffer[ly * lw + lx] = TileCell.FromTerrainSample(sample, eval, 0);
            }
        }
    }

    /// <summary>Chunk indices for global cell <paramref name="gx"/>, <paramref name="gy"/>.</summary>
    public void ResolveChunkCoordinates(int gx, int gy, out int cx, out int cy)
    {
        ResolveChunk(gx, gy, out cx, out cy, out _, out _);
    }

    private void ResolveChunk(int x, int y, out int cx, out int cy, out int lx, out int ly)
    {
        var slx = x - MinX;
        var sly = y - MinY;
        if (IsBounded)
        {
            cx = slx / ChunkWidth;
            cy = sly / ChunkHeight;
        }
        else
        {
            cx = FloorDiv(slx, ChunkWidth);
            cy = FloorDiv(sly, ChunkHeight);
        }

        lx = slx - cx * ChunkWidth;
        ly = sly - cy * ChunkHeight;
    }

    private static int FloorDiv(int n, int d)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(d);
        if (n >= 0)
            return n / d;
        return (n + 1) / d - 1;
    }

    private TileCell[] AllocateChunkBuffer(int cx, int cy)
    {
        int lw, lh;
        if (IsBounded)
            _chunkDims.GetChunkWorldExtent(cx, cy, Width, Height, out _, out _, out lw, out lh);
        else
            _chunkDims.GetChunkWorldExtentUnbounded(cx, cy, out _, out _, out lw, out lh);
        return new TileCell[lw * lh];
    }

    private int LocalIndex(int lx, int ly, int cx, int cy)
    {
        int lw;
        if (IsBounded)
            _chunkDims.GetChunkWorldExtent(cx, cy, Width, Height, out _, out _, out lw, out _);
        else
            _chunkDims.GetChunkWorldExtentUnbounded(cx, cy, out _, out _, out lw, out _);
        return ly * lw + lx;
    }

    /// <summary>Chunk coordinates that currently have allocated tile buffers (for tools like water rules on streaming maps).</summary>
    public IEnumerable<(int Cx, int Cy)> GetLoadedChunkCoordinates() => _chunks.Keys;

    /// <summary>Number of chunks with allocated storage.</summary>
    public int LoadedChunkCount => _chunks.Count;

    /// <summary>Chunks that received at least one <see cref="Set"/> while <see cref="SuppressChunkModificationTracking"/> was false.</summary>
    public int ModifiedChunkCount => _modifiedChunks.Count;

    public bool IsChunkModified(int cx, int cy) => _modifiedChunks.Contains((cx, cy));

    /// <summary>Snapshot copy for save pipelines.</summary>
    public void CopyModifiedChunkCoordinates(List<(int Cx, int Cy)> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var key in _modifiedChunks)
            destination.Add(key);
    }

    /// <summary>Call after persisting modified chunks so the next session only tracks new edits.</summary>
    public void ClearChunkModificationTracking() => _modifiedChunks.Clear();

    /// <summary>
    /// Drops an allocated chunk only if it was never marked modified (e.g. noise-only materialization).
    /// </summary>
    public bool TryEvictUnmodifiedChunk(int cx, int cy)
    {
        if (!_chunks.ContainsKey((cx, cy)) || _modifiedChunks.Contains((cx, cy)))
            return false;
        _chunks.Remove((cx, cy));
        return true;
    }

    /// <summary>World-space origin cell and size for chunk indices <paramref name="cx"/>, <paramref name="cy"/>.</summary>
    public void GetChunkWorldCellRange(int cx, int cy, out int gx0, out int gy0, out int lw, out int lh)
    {
        if (IsBounded)
        {
            _chunkDims.GetChunkWorldExtent(cx, cy, Width, Height, out var ox, out var oy, out lw, out lh);
            gx0 = MinX + ox;
            gy0 = MinY + oy;
        }
        else
        {
            _chunkDims.GetChunkWorldExtentUnbounded(cx, cy, out var ox, out var oy, out lw, out lh);
            gx0 = MinX + ox;
            gy0 = MinY + oy;
        }
    }

    /// <summary>True if any cell is non-default (see <c>docs/architecture.md</c> — “defined tile”).</summary>
    public bool HasAnyDefinedTile()
    {
        foreach (var buffer in _chunks.Values)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].IsEmpty)
                    return true;
            }
        }

        return false;
    }
}
