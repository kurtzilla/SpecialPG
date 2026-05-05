namespace SpecialPG.Core.Maps;

/// <summary>
/// Per-cell terrain: quantized noise channels, optional override, gameplay flags, and Shell variant.
/// <see cref="ElevationBucket"/> 0 with <see cref="IsEmpty"/> means no stored data (sparse chunk).
/// Materialized procedural cells use elevation buckets in <c>1..255</c>.
/// </summary>
public readonly struct TileCell
{
    public byte ElevationBucket { get; init; }
    public byte MoistureBucket { get; init; }
    public TerrainOverride Override { get; init; }
    public byte Flags { get; init; }
    public byte Variant { get; init; }

    /// <summary>True when no chunk data was written for this cell (sparse storage sentinel).</summary>
    public bool IsEmpty =>
        Override == TerrainOverride.None &&
        Flags == 0 &&
        ElevationBucket == 0 &&
        MoistureBucket == 0 &&
        Variant == 0;

    /// <summary>Approximate normalized elevation in [-1, 1] from stored bucket; undefined if <see cref="IsEmpty"/>.</summary>
    public float DecodeElevation() => ElevationBucket / 127.5f - 1f;

    public static byte QuantizeElevation(float elevation)
    {
        var u = (elevation + 1f) * 127.5f;
        return (byte)Math.Clamp((int)Math.Round(u), 1, 255);
    }

    public static byte QuantizeMoisture(float moisture01) =>
        (byte)Math.Clamp((int)Math.Round(moisture01 * 255f), 0, 255);

    public static TileCell FromTerrainSample(TerrainSample sample, ITerrainEvaluator eval, byte variant = 0)
    {
        var water = eval.IsWater(sample);
        return new TileCell
        {
            ElevationBucket = QuantizeElevation(sample.Elevation),
            MoistureBucket = QuantizeMoisture(sample.Moisture),
            Override = TerrainOverride.None,
            Flags = water ? TileFlags.Blocked : (byte)0,
            Variant = variant,
        };
    }

    /// <summary>Land-like cell for tests and editor (mid elevation).</summary>
    public static TileCell SyntheticLand(byte variant = 0) => new()
    {
        ElevationBucket = 200,
        MoistureBucket = 128,
        Override = TerrainOverride.None,
        Flags = 0,
        Variant = variant,
    };

    /// <summary>Water-like cell for tests (low elevation + blocked).</summary>
    public static TileCell SyntheticWater() => new()
    {
        ElevationBucket = 30,
        MoistureBucket = 220,
        Override = TerrainOverride.None,
        Flags = TileFlags.Blocked,
        Variant = 0,
    };
}
