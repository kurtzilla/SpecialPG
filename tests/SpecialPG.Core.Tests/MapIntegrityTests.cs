using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class MapIntegrityTests
{
    [Fact]
    public void Validate_two_floors_with_tiles_and_stair_link_has_no_errors()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.GetOrCreateFloor(1).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
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

        var result = MapIntegrity.Validate(map);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Validate_non_consecutive_floors_with_single_stair_hop_has_no_errors()
    {
        var map = new WorldMap(2, 2);
        var open = new TileData { TileKind = 1, Flags = 0, Variant = 0 };
        map.GetOrCreateFloor(0).Set(0, 0, open);
        map.GetOrCreateFloor(3).Set(0, 0, open);
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 0,
            ToY = 0,
            ToZ = 3,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var result = MapIntegrity.Validate(map);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Validate_floor_with_tiles_but_no_vertical_link_reports_error()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });

        var result = MapIntegrity.Validate(map);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Validate_vertical_link_to_out_of_range_cell_reports_error()
    {
        var map = new WorldMap(2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.GetOrCreateFloor(1).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 9,
            ToY = 0,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var result = MapIntegrity.Validate(map);

        Assert.True(result.HasErrors);
    }
}
