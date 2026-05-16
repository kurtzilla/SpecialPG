namespace SpecialPG.Core.Maps.Rendering;

/// <summary>Deterministic water animation frame selection for terrain bake (sprite and color modes).</summary>
public static class TerrainWaterAnimation
{
    public const int FrameCount = 4;

    public const int FramePeriodMs = 200;

    /// <summary>Shared timeline frame index in <c>0..frameCount-1</c>.</summary>
    public static int GetGlobalFrameIndex(long timeMs, int frameCount = FrameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var period = FramePeriodMs;
        if (period <= 0)
        {
            period = 1;
        }

        return (int)((timeMs / period) % frameCount);
    }

    /// <summary>Per-cell frame with staggered phase offset.</summary>
    public static int GetFrameIndex(int worldSeed, int gx, int gy, long timeMs, int frameCount = FrameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var global = GetGlobalFrameIndex(timeMs, frameCount);
        var phase = HashCellPhase(worldSeed, gx, gy) % frameCount;
        return (global + phase) % frameCount;
    }

    public static bool IsWaterCategory(TerrainRenderCategory category) =>
        category is TerrainRenderCategory.DeepWater or TerrainRenderCategory.ShallowWater;

    private static int HashCellPhase(int seed, int gx, int gy)
    {
        unchecked
        {
            var h = seed ^ (gx * 374761393) ^ (gy * 668265263);
            h = (int)((h ^ (uint)h >> 13) * 1274126177);
            return h & 0x7FFFFFFF;
        }
    }
}
