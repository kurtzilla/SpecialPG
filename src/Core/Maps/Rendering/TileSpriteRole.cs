namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Sprite shape within a terrain atlas; only <see cref="Main1x1"/> is used until multi-size patches land.</summary>
public enum TileSpriteRole : byte
{
    Main1x1 = 0,
    Main2x2 = 1,
    Main4x4 = 2,
    Side = 10,
    OuterCorner = 11,
    InnerCorner = 12,
    DoubleSide = 13,
    UTransition = 14,
    OTransition = 15,
    Overlay = 20,
}
