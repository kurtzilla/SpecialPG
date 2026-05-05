namespace SpecialPG.Core.Maps;

/// <summary>
/// All horizontal <see cref="FloorSlice"/> layers plus vertical connections between cells.
/// Bounded maps: global floor cells satisfy <c>MinX ≤ X &lt; MinX+Width</c>, <c>MinY ≤ Y &lt; MinY+Height</c> (defaults 0,0).
/// Unbounded maps: <see cref="Width"/> and <see cref="Height"/> are zero; use <see cref="CreateUnbounded"/>.
/// </summary>
public sealed class WorldMap
{
    private readonly Dictionary<int, FloorSlice> _floors = new();
    private readonly List<VerticalLink> _verticalLinks = new();

    public WorldMap(int width, int height, int chunkWidth = MapChunkDimensions.DefaultWidth,
        int chunkHeight = MapChunkDimensions.DefaultHeight, int minX = 0, int minY = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkHeight);
        IsBounded = true;
        Width = width;
        Height = height;
        ChunkWidth = chunkWidth;
        ChunkHeight = chunkHeight;
        MinX = minX;
        MinY = minY;
    }

    /// <summary>
    /// Unbounded world (streaming): <see cref="Width"/> and <see cref="Height"/> are 0; floors use unbounded <see cref="FloorSlice"/> ctor.
    /// </summary>
    public static WorldMap CreateUnbounded(int chunkWidth = MapChunkDimensions.DefaultWidth,
        int chunkHeight = MapChunkDimensions.DefaultHeight, int minX = 0, int minY = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkHeight);
        return new WorldMap
        {
            IsBounded = false,
            Width = 0,
            Height = 0,
            ChunkWidth = chunkWidth,
            ChunkHeight = chunkHeight,
            MinX = minX,
            MinY = minY,
        };
    }

    private WorldMap()
    {
    }

    public bool IsBounded { get; private init; }

    public int MinX { get; private init; }
    public int MinY { get; private init; }
    public int Width { get; private init; }
    public int Height { get; private init; }

    /// <summary>Horizontal chunk size in cells (Factorio default 32).</summary>
    public int ChunkWidth { get; private init; }

    /// <summary>Vertical chunk size in cells (Factorio default 32).</summary>
    public int ChunkHeight { get; private init; }

    public IReadOnlyList<VerticalLink> VerticalLinks => _verticalLinks;

    /// <summary>Floor indices that currently exist (including empty slices).</summary>
    public IReadOnlyList<int> PresentFloorIndices()
    {
        var list = _floors.Keys.ToList();
        list.Sort();
        return list;
    }

    public bool TryGetFloor(int z, out FloorSlice? floor) => _floors.TryGetValue(z, out floor);

    /// <summary>Returns existing slice or creates a new empty <see cref="FloorSlice"/> for <paramref name="z"/>.</summary>
    public FloorSlice GetOrCreateFloor(int z)
    {
        if (_floors.TryGetValue(z, out var existing))
            return existing;

        var slice = IsBounded
            ? new FloorSlice(MinX, MinY, Width, Height, z, ChunkWidth, ChunkHeight)
            : new FloorSlice(MinX, MinY, z, ChunkWidth, ChunkHeight);
        _floors[z] = slice;
        return slice;
    }

    /// <summary>Registers an existing slice (e.g. after load). Replaces any slice at <see cref="FloorSlice.Z"/>.</summary>
    public void SetFloor(FloorSlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        if (IsBounded)
        {
            if (slice.Width != Width || slice.Height != Height)
                throw new ArgumentException("Floor slice width/height must match map dimensions.", nameof(slice));
        }
        else if (slice.IsBounded)
        {
            throw new ArgumentException("Unbounded world requires unbounded floor slices (Width/Height are not used).",
                nameof(slice));
        }

        if (slice.ChunkWidth != ChunkWidth || slice.ChunkHeight != ChunkHeight)
            throw new ArgumentException("Floor slice chunk dimensions must match map chunk dimensions.", nameof(slice));
        if (slice.MinX != MinX || slice.MinY != MinY)
            throw new ArgumentException("Floor slice MinX/MinY must match map origin.", nameof(slice));

        _floors[slice.Z] = slice;
    }

    public void AddVerticalLink(VerticalLink link) => _verticalLinks.Add(link);

    public void ClearVerticalLinks() => _verticalLinks.Clear();

    /// <summary>First vertical link whose <see cref="VerticalLink.FromX"/>, <see cref="VerticalLink.FromY"/>, <see cref="VerticalLink.FromZ"/> match the cell.</summary>
    public bool TryGetVerticalLinkFrom(int x, int y, int z, out VerticalLink link)
    {
        foreach (var l in _verticalLinks)
        {
            if (l.FromX == x && l.FromY == y && l.FromZ == z)
            {
                link = l;
                return true;
            }
        }

        link = default;
        return false;
    }

    /// <summary>
    /// If the cell is the <c>To</c> endpoint of a non–one-way link, returns that link so the caller can traverse to <see cref="VerticalLink.FromX"/>, <see cref="VerticalLink.FromY"/>, <see cref="VerticalLink.FromZ"/>.
    /// This is the **inverse hop** of the same <see cref="VerticalLink"/> (symmetric two-way stair for that edge).
    /// </summary>
    public bool TryGetVerticalLinkReverse(int x, int y, int z, out VerticalLink link)
    {
        foreach (var l in _verticalLinks)
        {
            if (!l.OneWay && l.ToX == x && l.ToY == y && l.ToZ == z)
            {
                link = l;
                return true;
            }
        }

        link = default;
        return false;
    }
}
