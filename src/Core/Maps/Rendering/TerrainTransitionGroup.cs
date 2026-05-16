namespace SpecialPG.Core.Maps.Rendering;

/// <summary>
/// Collapsed terrain classes for edge-transition rules (Phase 4).
/// Full <see cref="TerrainRenderCategory"/> values map here before neighbor comparison.
/// </summary>
public enum TerrainTransitionGroup : byte
{
    Water,
    Ground,
    Blocked,
    Empty,
}

/// <summary>
/// Maps render categories to transition groups and decides which group pairs get edge sprites.
/// </summary>
public static class TerrainTransitionGrouping
{
    /// <summary>
    /// Category → group: Deep/Shallow/ForcedWater → <see cref="TerrainTransitionGroup.Water"/>;
    /// Land/Coast/Hill/ForcedLand* → <see cref="TerrainTransitionGroup.Ground"/>;
    /// Blocked → Blocked; Empty → Empty.
    /// </summary>
    public static TerrainTransitionGroup FromCategory(TerrainRenderCategory category) =>
        category switch
        {
            TerrainRenderCategory.DeepWater => TerrainTransitionGroup.Water,
            TerrainRenderCategory.ShallowWater => TerrainTransitionGroup.Water,
            TerrainRenderCategory.ForcedWater => TerrainTransitionGroup.Water,
            TerrainRenderCategory.Coast => TerrainTransitionGroup.Ground,
            TerrainRenderCategory.Land => TerrainTransitionGroup.Ground,
            TerrainRenderCategory.Hill => TerrainTransitionGroup.Ground,
            TerrainRenderCategory.ForcedLandCoastBlend => TerrainTransitionGroup.Ground,
            TerrainRenderCategory.ForcedLandOverride => TerrainTransitionGroup.Ground,
            TerrainRenderCategory.Blocked => TerrainTransitionGroup.Blocked,
            TerrainRenderCategory.Empty => TerrainTransitionGroup.Empty,
            _ => TerrainTransitionGroup.Empty,
        };

    /// <summary>
    /// Whether a <see cref="TileSpriteRole.Side"/> transition should be considered between two neighbors.
    /// v1: <see cref="TerrainTransitionGroup.Water"/> ↔ <see cref="TerrainTransitionGroup.Ground"/>,
    /// and Ground ↔ Blocked. Same group and Empty pairs return false.
    /// </summary>
    public static bool NeedsTransition(TerrainTransitionGroup a, TerrainTransitionGroup b)
    {
        if (a == b)
            return false;

        if (a == TerrainTransitionGroup.Empty || b == TerrainTransitionGroup.Empty)
            return false;

        return (a == TerrainTransitionGroup.Water && b == TerrainTransitionGroup.Ground)
               || (a == TerrainTransitionGroup.Ground && b == TerrainTransitionGroup.Water)
               || (a == TerrainTransitionGroup.Ground && b == TerrainTransitionGroup.Blocked)
               || (a == TerrainTransitionGroup.Blocked && b == TerrainTransitionGroup.Ground);
    }

    /// <summary>Convenience: compare groups derived from two render categories.</summary>
    public static bool NeedsTransition(TerrainRenderCategory inner, TerrainRenderCategory neighbor) =>
        NeedsTransition(FromCategory(inner), FromCategory(neighbor));
}
