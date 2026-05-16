using System.Collections.Generic;

namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Sort order for overlapping <see cref="TileDrawOp"/> records: Layer, OriginGy, OriginGx.</summary>
public sealed class TileDrawOpComparer : IComparer<TileDrawOp>
{
    public static TileDrawOpComparer Instance { get; } = new();

    public int Compare(TileDrawOp a, TileDrawOp b)
    {
        var layer = a.Layer.CompareTo(b.Layer);
        if (layer != 0)
            return layer;

        var gy = a.OriginGy.CompareTo(b.OriginGy);
        if (gy != 0)
            return gy;

        return a.OriginGx.CompareTo(b.OriginGx);
    }
}
