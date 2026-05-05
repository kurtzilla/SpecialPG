namespace SpecialPG.Core.Maps;

/// <summary>
/// Evaluates procedural terrain at arbitrary world coordinates (including sub-tile).
/// </summary>
public interface ITerrainEvaluator
{
    TerrainSample EvaluateAt(float worldX, float worldY);

    /// <summary>Whether <paramref name="sample"/> counts as water for walkability / <see cref="TileCell"/> materialization.</summary>
    bool IsWater(TerrainSample sample);
}
