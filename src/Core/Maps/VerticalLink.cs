namespace SpecialPG.Core.Maps;

/// <summary>
/// Directed **single hop** from one cell to another. <see cref="FromZ"/> and <see cref="ToZ"/> may be any distinct floor indices (not necessarily adjacent).
/// When <see cref="OneWay"/> is false, reverse traversal uses the same endpoint pair (see <see cref="WorldMap.TryGetVerticalLinkReverse"/>); for asymmetric up/down later, introduce an explicit rule or new fields.
/// </summary>
public readonly struct VerticalLink
{
    public int FromX { get; init; }
    public int FromY { get; init; }
    public int FromZ { get; init; }
    public int ToX { get; init; }
    public int ToY { get; init; }
    public int ToZ { get; init; }
    public VerticalLinkKind Kind { get; init; }

    /// <summary>If true, return path is not required for this link (design is explicitly one-way).</summary>
    public bool OneWay { get; init; }
}
