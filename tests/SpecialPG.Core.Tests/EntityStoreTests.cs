using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class EntityStoreTests
{
    [Fact]
    public void Spawn_move_destroy_updates_chunk_buckets()
    {
        var map = new WorldMap(128, 128, chunkWidth: 32, chunkHeight: 32);
        var store = new EntityStore(map);

        var id = store.Spawn(EntityKinds.Prop, x: 10, y: 10, z: 0);
        Assert.False(id.IsNone);
        Assert.Equal(1, store.Count);

        var buf = new List<EntityId>();
        store.AppendEntitiesOverlappingCell(10, 10, 0, buf);
        Assert.Single(buf);
        Assert.Equal(id, buf[0]);

        buf.Clear();
        store.AppendEntitiesInChunk(0, 0, 0, buf);
        Assert.Contains(id, buf);

        Assert.True(store.TrySetCell(id, 40, 10, 0));
        buf.Clear();
        store.AppendEntitiesOverlappingCell(10, 10, 0, buf);
        Assert.Empty(buf);
        buf.Clear();
        store.AppendEntitiesOverlappingCell(40, 10, 0, buf);
        Assert.Single(buf);

        Assert.True(store.Destroy(id));
        Assert.Equal(0, store.Count);
        buf.Clear();
        store.AppendEntitiesOverlappingCell(40, 10, 0, buf);
        Assert.Empty(buf);
    }

    [Fact]
    public void Unbounded_map_uses_floor_division_for_negative_chunks()
    {
        var map = WorldMap.CreateUnbounded(8, 8);
        var store = new EntityStore(map);
        var id = store.Spawn(EntityKinds.Prop, -1, -1, 0);

        var buf = new List<EntityId>();
        store.AppendEntitiesInChunk(-1, -1, 0, buf);
        Assert.Contains(id, buf);
    }

    [Fact]
    public void AppendEntitiesInWorld_rect_filters_floor_and_bounds()
    {
        var map = new WorldMap(64, 64, 16, 16);
        var store = new EntityStore(map);
        var a = store.Spawn(1, 5, 5, 0);
        var b = store.Spawn(1, 20, 20, 0);
        _ = store.Spawn(1, 5, 5, 1);

        var buf = new List<EntityId>();
        store.AppendEntitiesInWorldRect(0, 0, 10, 10, 0, buf);
        Assert.Contains(a, buf);
        Assert.DoesNotContain(b, buf);
    }

    [Fact]
    public void ReplaceAllRecords_rebuilds_index_and_spawn_ids()
    {
        var map = new WorldMap(32, 32, 8, 8);
        var store = new EntityStore(map);
        store.ReplaceAllRecords(
        [
            new EntityRecord
            {
                Id = new EntityId(5),
                Kind = 9,
                X = 1,
                Y = 2,
                Z = 0,
            },
        ]);

        Assert.True(store.TryGet(new EntityId(5), out var r));
        Assert.Equal(9, r.Kind);
        var next = store.Spawn(1, 0, 0, 0);
        Assert.Equal(new EntityId(6), next);
    }
}
