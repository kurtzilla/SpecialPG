using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecialPG.Core.Maps;

/// <summary>JSON snapshot of <see cref="EntityStore"/> for saves (separate from terrain <see cref="WorldMapJson"/>).</summary>
public static class EntityStoreJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(EntityStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var list = new List<EntityRecordDto>();
        foreach (var r in store.CloneAllRecordsSortedById())
            list.Add(EntityRecordDto.FromRecord(r));

        var root = new EntityStoreRootDto { SchemaVersion = 1, Entities = list };
        return JsonSerializer.Serialize(root, Options);
    }

    /// <summary>Replaces all entities in <paramref name="store"/> from JSON (typically after loading a world).</summary>
    public static bool TryDeserializeInto(EntityStore store, string json, out string? error)
    {
        ArgumentNullException.ThrowIfNull(store);
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            store.Clear();
            return true;
        }

        try
        {
            var root = JsonSerializer.Deserialize<EntityStoreRootDto>(json, Options);
            if (root is null)
            {
                error = "JSON root was null.";
                return false;
            }

            if (root.SchemaVersion != 1)
            {
                error = $"Unsupported entity store schema version {root.SchemaVersion}.";
                return false;
            }

            var records = new List<EntityRecord>();
            foreach (var dto in root.Entities ?? [])
                records.Add(dto.ToRecord());

            store.ReplaceAllRecords(records);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class EntityStoreRootDto
    {
        public int SchemaVersion { get; set; } = 1;

        public List<EntityRecordDto> Entities { get; set; } = new();
    }

    private sealed class EntityRecordDto
    {
        public ulong Id { get; set; }

        public ushort Kind { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        public ushort Flags { get; set; }

        public byte SubCellX { get; set; }

        public byte SubCellY { get; set; }

        public static EntityRecordDto FromRecord(EntityRecord r) =>
            new()
            {
                Id = r.Id.Value,
                Kind = r.Kind,
                X = r.X,
                Y = r.Y,
                Z = r.Z,
                Flags = r.Flags,
                SubCellX = r.SubCellX,
                SubCellY = r.SubCellY,
            };

        public EntityRecord ToRecord() =>
            new()
            {
                Id = new EntityId(Id),
                Kind = Kind,
                X = X,
                Y = Y,
                Z = Z,
                Flags = Flags,
                SubCellX = SubCellX,
                SubCellY = SubCellY,
            };
    }
}
