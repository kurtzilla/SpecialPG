using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SpecialPG.Core.Maps;

/// <summary>JSON serialization for <see cref="MapSaveEnvelope"/> (Core-only; Shell writes bytes via Godot).</summary>
public static class MapSaveEnvelopeJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Serialize(MapSaveEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static bool TryDeserialize(string json, [NotNullWhen(true)] out MapSaveEnvelope? envelope, out string? error)
    {
        envelope = null;
        error = null;
        try
        {
            envelope = JsonSerializer.Deserialize<MapSaveEnvelope>(json, Options);
            if (envelope is null)
            {
                error = "Envelope root was null.";
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
}
