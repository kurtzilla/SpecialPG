using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecialPG.Core.Maps;

/// <summary>JSON load/save for <see cref="WorldMap"/> (Shell supplies file bytes or path via Godot).</summary>
public static class WorldMapJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var dto = WorldMapDto.FromWorldMap(map);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static bool TryDeserialize(string json, [NotNullWhen(true)] out WorldMap? map, out string? error)
    {
        map = null;
        error = null;
        try
        {
            var dto = JsonSerializer.Deserialize<WorldMapDto>(json, Options);
            if (dto is null)
            {
                error = "JSON root was null.";
                return false;
            }

            map = dto.ToWorldMap(out var err);
            if (map is null)
            {
                error = err;
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class WorldMapDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int ChunkWidth { get; set; }
        public int ChunkHeight { get; set; }
        public List<FloorDto> Floors { get; set; } = new();
        public List<VerticalLinkDto> VerticalLinks { get; set; } = new();

        public static WorldMapDto FromWorldMap(WorldMap map)
        {
            var dto = new WorldMapDto
            {
                Width = map.Width,
                Height = map.Height,
                MinX = map.MinX,
                MinY = map.MinY,
                ChunkWidth = map.ChunkWidth,
                ChunkHeight = map.ChunkHeight,
            };
            foreach (var z in map.PresentFloorIndices())
            {
                if (!map.TryGetFloor(z, out var slice) || slice is null)
                    continue;
                dto.Floors.Add(FloorDto.FromSlice(slice));
            }

            foreach (var link in map.VerticalLinks)
                dto.VerticalLinks.Add(VerticalLinkDto.FromLink(link));

            return dto;
        }

        public WorldMap? ToWorldMap(out string? error)
        {
            error = null;
            if (Width <= 0 || Height <= 0)
            {
                error = "width and height must be positive.";
                return null;
            }

            var chunkW = ChunkWidth > 0 ? ChunkWidth : MapChunkDimensions.DefaultWidth;
            var chunkH = ChunkHeight > 0 ? ChunkHeight : MapChunkDimensions.DefaultHeight;

            var world = new WorldMap(Width, Height, chunkW, chunkH, MinX, MinY);
            foreach (var floor in Floors)
            {
                if (floor.Cells.Length != Width * Height)
                {
                    error = $"Floor Z={floor.Z}: expected {Width * Height} cells, got {floor.Cells.Length}.";
                    return null;
                }

                var slice = new FloorSlice(MinX, MinY, Width, Height, floor.Z, chunkW, chunkH);
                for (var i = 0; i < floor.Cells.Length; i++)
                {
                    var lx = i % Width;
                    var ly = i / Width;
                    slice.Set(MinX + lx, MinY + ly, floor.Cells[i].ToTile());
                }

                world.SetFloor(slice);
            }

            foreach (var ld in VerticalLinks)
                world.AddVerticalLink(ld.ToLink());

            return world;
        }
    }

    private sealed class FloorDto
    {
        public int Z { get; set; }
        public TileDataDto[] Cells { get; set; } = Array.Empty<TileDataDto>();

        public static FloorDto FromSlice(FloorSlice slice)
        {
            var cells = new TileDataDto[slice.Width * slice.Height];
            var i = 0;
            for (var ly = 0; ly < slice.Height; ly++)
            {
                for (var lx = 0; lx < slice.Width; lx++)
                {
                    cells[i++] = TileDataDto.FromTile(slice.Get(slice.MinX + lx, slice.MinY + ly));
                }
            }

            return new FloorDto { Z = slice.Z, Cells = cells };
        }
    }

    private sealed class TileDataDto
    {
        public ushort TileKind { get; set; }
        public byte Flags { get; set; }
        public byte Variant { get; set; }

        public TileData ToTile() =>
            new() { TileKind = TileKind, Flags = Flags, Variant = Variant };

        public static TileDataDto FromTile(TileData t) =>
            new() { TileKind = t.TileKind, Flags = t.Flags, Variant = t.Variant };
    }

    private sealed class VerticalLinkDto
    {
        public int FromX { get; set; }
        public int FromY { get; set; }
        public int FromZ { get; set; }
        public int ToX { get; set; }
        public int ToY { get; set; }
        public int ToZ { get; set; }
        public VerticalLinkKind Kind { get; set; }
        public bool OneWay { get; set; }

        public VerticalLink ToLink() =>
            new()
            {
                FromX = FromX,
                FromY = FromY,
                FromZ = FromZ,
                ToX = ToX,
                ToY = ToY,
                ToZ = ToZ,
                Kind = Kind,
                OneWay = OneWay,
            };

        public static VerticalLinkDto FromLink(VerticalLink link) =>
            new()
            {
                FromX = link.FromX,
                FromY = link.FromY,
                FromZ = link.FromZ,
                ToX = link.ToX,
                ToY = link.ToY,
                ToZ = link.ToZ,
                Kind = link.Kind,
                OneWay = link.OneWay,
            };
    }
}
