using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class MapIntegrityTests
{
    [Fact]
    public void Validate_passes_when_each_defined_floor_has_vertical_exit()
    {
        var map = new WorldMap(4, 4);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var r = MapIntegrity.Validate(map);
        Assert.True(r.IsValid, string.Join("; ", r.Issues.ConvertAll(i => i.Message)));
    }

    [Fact]
    public void Validate_fails_when_floor_has_tiles_but_no_vertical_link()
    {
        var map = new WorldMap(2, 2);
        var open = TileCell.SyntheticLand();
        map.GetOrCreateFloor(0).Set(0, 0, open);
        map.GetOrCreateFloor(1).Set(0, 0, open);

        var r = MapIntegrity.Validate(map);
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Severity == MapIntegritySeverity.Error);
    }

    [Fact]
    public void Validate_errors_when_link_From_out_of_bounds()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 99,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var r = MapIntegrity.Validate(map);
        Assert.True(r.HasErrors);
    }

    [Fact]
    public void Validate_passes_for_symmetric_two_floor_link()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var r = MapIntegrity.Validate(map);
        Assert.False(r.HasErrors);
    }

    [Fact]
    public void ValidateModification_warns_when_stair_endpoint_becomes_blocked()
    {
        var map = new WorldMap(2, 2);
        var terrain = map.TerrainConfig;
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var blocked = TileCell.SyntheticLand() with { Flags = TileFlags.Blocked };
        var r = MapIntegrity.ValidateModification(map, 0, 0, 0, blocked, terrain);
        Assert.False(r.HasErrors);
        Assert.Contains(r.Issues, i => i.Severity == MapIntegritySeverity.Warning);
    }

    [Fact]
    public void ValidateModification_no_warning_when_stair_stays_walkable()
    {
        var map = new WorldMap(2, 2);
        var terrain = map.TerrainConfig;
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand());
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var r = MapIntegrity.ValidateModification(map, 0, 0, 0, TileCell.SyntheticLand(), terrain);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void ValidateVerticalLink_warns_when_from_endpoint_not_walkable()
    {
        var map = new WorldMap(2, 2);
        var terrain = map.TerrainConfig;
        map.GetOrCreateFloor(0).Set(0, 0, TileCell.SyntheticLand() with { Flags = TileFlags.Blocked });
        map.GetOrCreateFloor(1).Set(0, 0, TileCell.SyntheticLand());
        var link = new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        };

        var r = MapIntegrity.ValidateVerticalLink(map, link, terrain);
        Assert.False(r.HasErrors);
        Assert.Contains(r.Issues, i => i.Severity == MapIntegritySeverity.Warning);
    }

    [Fact]
    public void ValidateModification_errors_for_missing_floor()
    {
        var map = new WorldMap(2, 2);
        var terrain = map.TerrainConfig;
        var r = MapIntegrity.ValidateModification(map, 9, 0, 0, TileCell.SyntheticLand(), terrain);
        Assert.True(r.HasErrors);
    }
}
