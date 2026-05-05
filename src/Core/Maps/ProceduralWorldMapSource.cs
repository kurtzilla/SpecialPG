namespace SpecialPG.Core.Maps;

/// <summary>
/// Procedural bounded <see cref="WorldMap"/> from parameters (Core; Shell map workbench supplies values).
/// </summary>
public sealed class ProceduralWorldMapSource : IWorldMapSource
{
    public ProceduralWorldMapSource(int width, int height, int chunkWidth, int chunkHeight, int seed)
        : this(width, height, chunkWidth, chunkHeight, MapGenerationParameters.FromSeedOnly(seed))
    {
    }

    public ProceduralWorldMapSource(int width, int height, int chunkWidth, int chunkHeight,
        MapGenerationParameters parameters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkHeight);
        if (!parameters.IsValid)
            throw new ArgumentException("Invalid MapGenerationParameters.", nameof(parameters));
        Width = width;
        Height = height;
        ChunkWidth = chunkWidth;
        ChunkHeight = chunkHeight;
        Parameters = parameters;
    }

    public int Width { get; }

    public int Height { get; }

    public int ChunkWidth { get; }

    public int ChunkHeight { get; }

    public MapGenerationParameters Parameters { get; }

    public WorldMap? TryBuildWorldMap(out string sourceSummary, out string? errorDetail)
    {
        sourceSummary =
            $"Procedural seed {Parameters.Seed} land={Parameters.LandPercent}% ({Width}×{Height} cells)";
        errorDetail = null;

        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(Width, Height, ChunkWidth, ChunkHeight, Parameters);
        if ((long)Width * Height < 4_000_000L)
            WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(map);

        var integrity = MapIntegrity.Validate(map);
        if (integrity.HasErrors)
        {
            errorDetail = string.Join("; ", integrity.Issues.FindAll(i => i.Severity == MapIntegritySeverity.Error)
                .ConvertAll(i => i.Message));
            sourceSummary = "Procedural (rejected)";
            return null;
        }

        return map;
    }
}
