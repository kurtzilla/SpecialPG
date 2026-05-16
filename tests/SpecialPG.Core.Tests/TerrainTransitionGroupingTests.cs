using SpecialPG.Core.Maps.Rendering;
using Xunit;

namespace SpecialPG.Core.Tests;

public class TerrainTransitionGroupingTests
{
    [Theory]
    [InlineData(TerrainRenderCategory.DeepWater, TerrainTransitionGroup.Water)]
    [InlineData(TerrainRenderCategory.ShallowWater, TerrainTransitionGroup.Water)]
    [InlineData(TerrainRenderCategory.ForcedWater, TerrainTransitionGroup.Water)]
    [InlineData(TerrainRenderCategory.Coast, TerrainTransitionGroup.Ground)]
    [InlineData(TerrainRenderCategory.Land, TerrainTransitionGroup.Ground)]
    [InlineData(TerrainRenderCategory.Hill, TerrainTransitionGroup.Ground)]
    [InlineData(TerrainRenderCategory.ForcedLandCoastBlend, TerrainTransitionGroup.Ground)]
    [InlineData(TerrainRenderCategory.ForcedLandOverride, TerrainTransitionGroup.Ground)]
    [InlineData(TerrainRenderCategory.Blocked, TerrainTransitionGroup.Blocked)]
    [InlineData(TerrainRenderCategory.Empty, TerrainTransitionGroup.Empty)]
    public void FromCategory_maps_expected_group(TerrainRenderCategory category, TerrainTransitionGroup expected) =>
        Assert.Equal(expected, TerrainTransitionGrouping.FromCategory(category));

    [Theory]
    [InlineData(TerrainTransitionGroup.Water, TerrainTransitionGroup.Ground, true)]
    [InlineData(TerrainTransitionGroup.Ground, TerrainTransitionGroup.Water, true)]
    [InlineData(TerrainTransitionGroup.Ground, TerrainTransitionGroup.Blocked, true)]
    [InlineData(TerrainTransitionGroup.Blocked, TerrainTransitionGroup.Ground, true)]
    [InlineData(TerrainTransitionGroup.Water, TerrainTransitionGroup.Water, false)]
    [InlineData(TerrainTransitionGroup.Ground, TerrainTransitionGroup.Ground, false)]
    public void NeedsTransition_group_pairs(
        TerrainTransitionGroup a,
        TerrainTransitionGroup b,
        bool expected) =>
        Assert.Equal(expected, TerrainTransitionGrouping.NeedsTransition(a, b));

    [Fact]
    public void NeedsTransition_same_ground_categories_false()
    {
        Assert.False(TerrainTransitionGrouping.NeedsTransition(
            TerrainRenderCategory.Land,
            TerrainRenderCategory.Hill));
        Assert.False(TerrainTransitionGrouping.NeedsTransition(
            TerrainRenderCategory.Coast,
            TerrainRenderCategory.ForcedLandOverride));
    }

    [Fact]
    public void NeedsTransition_same_water_categories_false() =>
        Assert.False(TerrainTransitionGrouping.NeedsTransition(
            TerrainRenderCategory.DeepWater,
            TerrainRenderCategory.ShallowWater));

    [Fact]
    public void NeedsTransition_water_and_ground_categories_true() =>
        Assert.True(TerrainTransitionGrouping.NeedsTransition(
            TerrainRenderCategory.Land,
            TerrainRenderCategory.ShallowWater));

    [Fact]
    public void NeedsTransition_empty_neighbor_false() =>
        Assert.False(TerrainTransitionGrouping.NeedsTransition(
            TerrainTransitionGroup.Ground,
            TerrainTransitionGroup.Empty));

    [Fact]
    public void NeedsTransition_water_and_blocked_false() =>
        Assert.False(TerrainTransitionGrouping.NeedsTransition(
            TerrainTransitionGroup.Water,
            TerrainTransitionGroup.Blocked));
}
