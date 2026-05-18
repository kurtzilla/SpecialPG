using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public sealed class LandmassNoiseAlignmentTests
{
    [Fact]
    public void ComputeOffset_negates_largest_land_centroid()
    {
        // 3×3 coarse grid: largest component is right cluster; centroid ~(2, 1) in global tile space.
        var samples = new List<LandmassNoiseAlignment.ElevationSample>
        {
            new(0, 0, 0f),
            new(0, 1, 0f),
            new(1, 0, 0f),
            new(1, 1, 0f),
            new(2, 0, 1f),
            new(2, 1, 1f),
            new(3, 0, 1f),
            new(3, 1, 1f),
        };

        var (dx, dy) = LandmassNoiseAlignment.ComputeOffsetToPlaceLccAtOrigin(
            samples,
            minX: 0,
            minY: 0,
            stepX: 1,
            stepY: 1,
            width: 4,
            height: 2,
            waterElevationThreshold: 0.5f);

        Assert.Equal(-3, dx);
        Assert.Equal(-1, dy);
    }

    [Fact]
    public void ComputeOffset_returns_zero_when_no_land()
    {
        var samples = new List<LandmassNoiseAlignment.ElevationSample>
        {
            new(0, 0, 0f),
            new(1, 0, 0f),
        };

        var (dx, dy) = LandmassNoiseAlignment.ComputeOffsetToPlaceLccAtOrigin(
            samples,
            minX: 0,
            minY: 0,
            stepX: 1,
            stepY: 1,
            width: 2,
            height: 1,
            waterElevationThreshold: 0.5f);

        Assert.Equal(0, dx);
        Assert.Equal(0, dy);
    }
}
