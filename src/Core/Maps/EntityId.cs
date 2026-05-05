namespace SpecialPG.Core.Maps;

/// <summary>Stable opaque handle for an entry in <see cref="EntityStore"/>.</summary>
public readonly record struct EntityId(ulong Value)
{
    /// <summary>Reserved; never returned by <see cref="EntityStore.Spawn"/>.</summary>
    public static EntityId None => default;

    public bool IsNone => Value == 0;
}
