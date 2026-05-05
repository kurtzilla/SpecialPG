using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Combines fractal simplex layers into <see cref="TerrainSample"/> and exposes threshold-based classification.
/// </summary>
public sealed class TerrainEvaluator : ITerrainEvaluator
{
    private readonly TerrainNoiseConfig _config;
    private readonly INoiseSampler _elevationNoise;
    private readonly INoiseSampler _moistureNoise;
    private readonly INoiseSampler _temperatureNoise;

    public TerrainEvaluator(TerrainNoiseConfig config)
    {
        _config = config;
        _elevationNoise = new SimplexNoiseSampler(NoiseSeedUtility.DeriveChannelSeed(config.Seed, 1));
        _moistureNoise = new SimplexNoiseSampler(NoiseSeedUtility.DeriveChannelSeed(config.Seed, 2));
        _temperatureNoise = new SimplexNoiseSampler(NoiseSeedUtility.DeriveChannelSeed(config.Seed, 3));
    }

    public TerrainNoiseConfig Config => _config;

    public TerrainSample EvaluateAt(float worldX, float worldY)
    {
        var ex = worldX * _config.ElevationNoiseScale;
        var ey = worldY * _config.ElevationNoiseScale;
        var elevation = FractalBrownian2D(ex, ey, _elevationNoise, _config.Octaves, _config.Persistence, _config.Lacunarity);

        var mx = worldX * _config.MoistureNoiseScale;
        var my = worldY * _config.MoistureNoiseScale;
        var mRaw = FractalBrownian2D(mx, my, _moistureNoise, _config.Octaves, _config.Persistence, _config.Lacunarity);
        var moisture = Normalize01(mRaw);

        var tx = worldX * _config.TemperatureNoiseScale;
        var ty = worldY * _config.TemperatureNoiseScale;
        var tRaw = FractalBrownian2D(tx, ty, _temperatureNoise, _config.Octaves, _config.Persistence, _config.Lacunarity);
        var temperature = Normalize01(tRaw);

        return new TerrainSample
        {
            Elevation = elevation,
            Moisture = moisture,
            Temperature = temperature
        };
    }

    public bool IsWater(TerrainSample sample) =>
        sample.Elevation < _config.WaterElevationThreshold;

    public bool IsCoastal(TerrainSample sample) =>
        sample.Elevation >= _config.WaterElevationThreshold &&
        sample.Elevation < _config.CoastElevationThreshold;

    public bool IsHilly(TerrainSample sample) =>
        sample.Elevation > _config.HillElevationThreshold;

    /// <summary>Materialize one cell from procedural noise (for chunk generation).</summary>
    public TileCell ToTileCell(float worldX, float worldY, byte variant = 0) =>
        TileCell.FromTerrainSample(EvaluateAt(worldX, worldY), this, variant);

    private static float FractalBrownian2D(
        float x,
        float y,
        INoiseSampler noise,
        int octaves,
        float persistence,
        float lacunarity)
    {
        if (octaves <= 0)
            return 0f;

        var total = 0f;
        var amplitude = 1f;
        var frequency = 1f;
        var maxValue = 0f;

        for (var i = 0; i < octaves; i++)
        {
            total += noise.Sample2D(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxValue > 0 ? total / maxValue : 0f;
    }

    private static float Normalize01(float value) => Math.Clamp(0.5f + 0.5f * value, 0f, 1f);
}
