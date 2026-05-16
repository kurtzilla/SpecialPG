namespace SpecialPG.Core.Maps.Rendering;

/// <summary>
/// One terrain draw command in grid space. Sort for overlapping ops: Layer, then OriginGy, then OriginGx.
/// </summary>
public readonly record struct TileDrawOp(
    TileSpriteKey Key,
    int OriginGx,
    int OriginGy,
    int SizeCells,
    TerrainDrawLayer Layer,
    TransitionFacing? Facing = null);
