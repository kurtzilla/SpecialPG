using SpecialPG.Core.Maps;
using Xunit;

namespace SpecialPG.Core.Tests;

public class EntityStoreJsonTests
{
    [Fact]
    public void Empty_json_clears_store()
    {
        var map = new WorldMap(8, 8, 4, 4);
        var store = new EntityStore(map);
        store.Spawn(1, 0, 0, 0);
        Assert.True(EntityStoreJson.TryDeserializeInto(store, "   ", out var err), err);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Round_trip_preserves_records()
    {
        var map = new WorldMap(48, 48, 12, 12);
        var store = new EntityStore(map);
        var id = store.Spawn(EntityKinds.Actor, 3, 4, 0, flags: 7);
        Assert.False(id.IsNone);

        var json = EntityStoreJson.Serialize(store);
        var store2 = new EntityStore(map);
        Assert.True(EntityStoreJson.TryDeserializeInto(store2, json, out var err), err);
        Assert.Equal(store.Count, store2.Count);
        Assert.True(store2.TryGet(id, out var r));
        Assert.Equal(EntityKinds.Actor, r.Kind);
        Assert.Equal(3, r.X);
        Assert.Equal(4, r.Y);
        Assert.Equal(7, r.Flags);
    }
}
