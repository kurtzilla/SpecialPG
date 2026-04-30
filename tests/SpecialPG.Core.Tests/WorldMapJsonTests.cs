using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class WorldMapJsonTests
{
    [Fact]
    public void Serialize_Then_TryDeserialize_round_trips_dimensions_floors_and_vertical_links()
    {
        var map = new WorldMap(2, 2);
        var z0 = map.GetOrCreateFloor(0);
        z0.Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        z0.Set(1, 1, new TileData { TileKind = 2, Flags = 0, Variant = 0 });
        var z1 = map.GetOrCreateFloor(1);
        z1.Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        map.AddVerticalLink(new VerticalLink
        {
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 1,
            ToY = 1,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        var json = WorldMapJson.Serialize(map);
        var ok = WorldMapJson.TryDeserialize(json, out var restored, out var err);

        Assert.True(ok, err);
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Width);
        Assert.Equal(2, restored.Height);
        Assert.Equal(32, restored.ChunkWidth);
        Assert.Equal(32, restored.ChunkHeight);
        Assert.Single(restored.VerticalLinks);
        Assert.Equal(0, restored.VerticalLinks[0].FromZ);
        Assert.Equal(1, restored.VerticalLinks[0].ToZ);
        Assert.True(restored.TryGetFloor(0, out var floor0) && floor0 is not null);
        Assert.Equal(2, floor0!.Get(1, 1).TileKind);
    }

    [Fact]
    public void TryDeserialize_omitted_chunk_dimensions_use_32()
    {
        const string json = """{"width":2,"height":2,"floors":[],"verticalLinks":[]}""";
        var ok = WorldMapJson.TryDeserialize(json, out var map, out var err);
        Assert.True(ok, err);
        Assert.NotNull(map);
        Assert.Equal(32, map!.ChunkWidth);
        Assert.Equal(32, map.ChunkHeight);
    }

    [Fact]
    public void Serialize_round_trips_custom_chunk_dimensions()
    {
        var map = new WorldMap(4, 4, 2, 2);
        map.GetOrCreateFloor(0).Set(0, 0, new TileData { TileKind = 1, Flags = 0, Variant = 0 });
        var json = WorldMapJson.Serialize(map);
        var ok = WorldMapJson.TryDeserialize(json, out var restored, out var err);
        Assert.True(ok, err);
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.ChunkWidth);
        Assert.Equal(2, restored.ChunkHeight);
        Assert.Equal(1, restored.GetOrCreateFloor(0).Get(0, 0).TileKind);
    }
}
