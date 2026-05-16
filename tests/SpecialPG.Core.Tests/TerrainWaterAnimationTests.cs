using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public sealed class TerrainWaterAnimationTests
{
    [Fact]
    public void GetFrameIndex_IsDeterministic()
    {
        var a = TerrainWaterAnimation.GetFrameIndex(42, 10, 20, 1000);
        var b = TerrainWaterAnimation.GetFrameIndex(42, 10, 20, 1000);
        Assert.Equal(a, b);
    }

    [Fact]
    public void GetFrameIndex_StaggersByCell()
    {
        var a = TerrainWaterAnimation.GetFrameIndex(1, 0, 0, 500);
        var b = TerrainWaterAnimation.GetFrameIndex(1, 3, 7, 500);
        Assert.InRange(a, 0, TerrainWaterAnimation.FrameCount - 1);
        Assert.InRange(b, 0, TerrainWaterAnimation.FrameCount - 1);
    }

    [Fact]
    public void GetFrameIndex_AdvancesWithTime()
    {
        var t0 = TerrainWaterAnimation.GetGlobalFrameIndex(0);
        var t1 = TerrainWaterAnimation.GetGlobalFrameIndex(TerrainWaterAnimation.FramePeriodMs);
        Assert.NotEqual(t0, t1);
    }

    [Fact]
    public void GetFrameIndex_WrapsInRange()
    {
        for (var ms = 0L; ms < 5000; ms += 37)
        {
            var f = TerrainWaterAnimation.GetFrameIndex(99, 5, 5, ms);
            Assert.InRange(f, 0, TerrainWaterAnimation.FrameCount - 1);
        }
    }
}
