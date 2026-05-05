namespace SpecialPG.Core.Maps.Noise;

/// <summary>
/// Deterministic 2D coherent noise in the approximate range [-1, 1].
/// </summary>
public interface INoiseSampler
{
    /// <summary>Sample noise at continuous world coordinates.</summary>
    float Sample2D(float x, float y);
}
