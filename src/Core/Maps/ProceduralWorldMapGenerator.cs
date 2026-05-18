using System.Collections.Generic;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Bounded procedural fill: deterministic terrain from <see cref="TerrainEvaluator"/> (seed in <see cref="TerrainNoiseConfig"/>).
/// Produces two floors with a two-way vertical link; intended to satisfy <see cref="MapIntegrity.Validate"/> for the filled region.
/// </summary>
public static class ProceduralWorldMapGenerator
{
    /// <summary>Full scan for maps at or below <see cref="LargeMapCellCountThreshold"/> cells.</summary>
    private const int MaxElevationSamplesForThreshold = 500_000;

    /// <summary>Maps larger than 128×128 subsample elevations for the water threshold (faster cold start).</summary>
    private const int LargeMapCellCountThreshold = 32_768;

    /// <summary>Cap when <see cref="LargeMapCellCountThreshold"/> is exceeded.</summary>
    internal const int MaxElevationSamplesForLargeMap = 32_768;

    /// <summary>
    /// Legacy entry: default land/water split from <see cref="MapGenerationParameters.FromSeedOnly"/>.
    /// </summary>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight, int seed,
        int minX = 0, int minY = 0) =>
        BuildBoundedWorld(width, height, chunkWidth, chunkHeight, MapGenerationParameters.FromSeedOnly(seed), minX, minY,
            OriginWalkabilityPatch.DefaultChebyshevRadius, maxLandBridgeCells: 0);

    /// <summary>
    /// Builds a rectangular world with floors Z=0 and Z=1, filled tile-by-chunk using noise.
    /// Chooses <see cref="TerrainNoiseConfig.WaterElevationThreshold"/> so roughly <see cref="MapGenerationParameters.WaterPercent"/> of
    /// cells classify as water at tile centers, then pans noise so the largest landmass sits near global (0,0).
    /// </summary>
    /// <param name="originPatchChebyshevRadius">
    /// Safety margin: small <see cref="TerrainOverride.ForceLand"/> square at global (0,0) after alignment.
    /// </param>
    /// <param name="maxLandBridgeCells">Legacy fallback when origin is not on LCC after alignment; <c>0</c> = unlimited.</param>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight,
        MapGenerationParameters parameters, int minX = 0, int minY = 0,
        int originPatchChebyshevRadius = OriginWalkabilityPatch.DefaultChebyshevRadius,
        int maxLandBridgeCells = 0)
    {
        if (!parameters.IsValid)
            throw new ArgumentException("MapGenerationParameters must have LandPercent + WaterPercent = 100.", nameof(parameters));

        var map = new WorldMap(width, height, chunkWidth, chunkHeight, minX, minY);

        var baseCfg = TerrainNoiseConfig.Default(parameters.Seed);
        map.TerrainConfig = baseCfg;
        var probeEval = new TerrainEvaluator(baseCfg);

        var cellCount = (long)width * height;
        var maxElevationSamples = cellCount > LargeMapCellCountThreshold
            ? MaxElevationSamplesForLargeMap
            : MaxElevationSamplesForThreshold;
        var subsample = CollectElevationSubsamples(probeEval, minX, minY, width, height, maxElevationSamples);
        var elevations = new List<float>(subsample.Samples.Count);
        for (var i = 0; i < subsample.Samples.Count; i++)
            elevations.Add(subsample.Samples[i].Elevation);

        var waterThreshold = ComputeWaterElevationThreshold(elevations, parameters.WaterPercent);
        var (noiseDx, noiseDy) = LandmassNoiseAlignment.ComputeOffsetToPlaceLccAtOrigin(
            subsample.Samples,
            minX,
            minY,
            subsample.StepX,
            subsample.StepY,
            width,
            height,
            waterThreshold);

        map.ProceduralLandmassAligned = true;
        map.ProceduralNoiseOffsetGx = noiseDx;
        map.ProceduralNoiseOffsetGy = noiseDy;

        var terrainCfg = baseCfg with { WaterElevationThreshold = waterThreshold };
        map.TerrainConfig = terrainCfg;
        var eval = new TerrainEvaluator(terrainCfg);

        var dims = new MapChunkDimensions(chunkWidth, chunkHeight);

        var stairX = minX + width / 2;
        var stairY = minY + height / 2;
        var seed = parameters.Seed;

        for (var z = 0; z < 2; z++)
        {
            var floor = map.GetOrCreateFloor(z);
            var prevSuppress = floor.SuppressChunkModificationTracking;
            floor.SuppressChunkModificationTracking = true;
            try
            {
                var nCx = dims.GetChunkCountX(width);
                var nCy = dims.GetChunkCountY(height);

                for (var cx = 0; cx < nCx; cx++)
                {
                    for (var cy = 0; cy < nCy; cy++)
                    {
                        dims.GetChunkWorldExtent(cx, cy, width, height, out var ox, out var oy, out var lw, out var lh);
                        var rng = new Random(HashCode.Combine(seed, z, cx, cy));

                        for (var ly = 0; ly < lh; ly++)
                        {
                            for (var lx = 0; lx < lw; lx++)
                            {
                                var gx = minX + ox + lx;
                                var gy = minY + oy + ly;

                                var sampleX = gx + noiseDx + 0.5f;
                                var sampleY = gy + noiseDy + 0.5f;
                                TileCell cell;
                                if (gx == stairX && gy == stairY)
                                {
                                    cell = eval.ToTileCell(sampleX, sampleY, 0) with
                                    {
                                        Override = TerrainOverride.ForceLand,
                                        Flags = 0,
                                    };
                                }
                                else
                                {
                                    cell = eval.ToTileCell(sampleX, sampleY, (byte)rng.Next(0, 4));
                                }

                                floor.Set(gx, gy, cell);
                            }
                        }
                    }
                }
            }
            finally
            {
                floor.SuppressChunkModificationTracking = prevSuppress;
            }
        }

        map.AddVerticalLink(new VerticalLink
        {
            FromX = stairX,
            FromY = stairY,
            FromZ = 0,
            ToX = stairX,
            ToY = stairY,
            ToZ = 1,
            Kind = VerticalLinkKind.Stairs,
            OneWay = false,
        });

        OriginWalkabilityPatch.ApplyToBoundedWorld(map, originPatchChebyshevRadius, 0, 0);
        ForceLandWalkMargin.ApplyToBoundedWorld(map);

        var floor0 = map.GetOrCreateFloor(0);
        var originGx = Math.Clamp(0, floor0.MinX, floor0.MinX + floor0.Width - 1);
        var originGy = Math.Clamp(0, floor0.MinY, floor0.MinY + floor0.Height - 1);
        if (!LandmassSpawnSupport.IsWalkableOnLargestLandmass(floor0, map.TerrainConfig, originGx, originGy))
            LandmassBridgeToLargestComponent.ApplyToBoundedWorld(map, maxLandBridgeCells);

        return map;
    }

    private readonly record struct ElevationSubsampleResult(
        List<LandmassNoiseAlignment.ElevationSample> Samples,
        int StepX,
        int StepY);

    private static ElevationSubsampleResult CollectElevationSubsamples(
        TerrainEvaluator eval,
        int minX,
        int minY,
        int width,
        int height,
        int maxSamples)
    {
        long total = (long)width * height;
        var list = new List<LandmassNoiseAlignment.ElevationSample>((int)Math.Min(total, maxSamples));

        if (total <= maxSamples)
        {
            for (var gy = minY; gy < minY + height; gy++)
            {
                for (var gx = minX; gx < minX + width; gx++)
                {
                    list.Add(new LandmassNoiseAlignment.ElevationSample(
                        gx,
                        gy,
                        eval.EvaluateAt(gx + 0.5f, gy + 0.5f).Elevation));
                }
            }

            return new ElevationSubsampleResult(list, 1, 1);
        }

        var stepY = Math.Max(1, (int)Math.Ceiling(height / Math.Sqrt(maxSamples)));
        var stepX = Math.Max(1, (int)Math.Ceiling(width / Math.Sqrt(maxSamples)));
        for (var gy = minY; gy < minY + height; gy += stepY)
        {
            for (var gx = minX; gx < minX + width; gx += stepX)
            {
                list.Add(new LandmassNoiseAlignment.ElevationSample(
                    gx,
                    gy,
                    eval.EvaluateAt(gx + 0.5f, gy + 0.5f).Elevation));
            }
        }

        return new ElevationSubsampleResult(list, stepX, stepY);
    }

    /// <summary>
    /// Elevation strictly below the returned value becomes water in <see cref="TerrainEvaluator.IsWater"/>.
    /// Sorts <paramref name="elevations"/> in place.
    /// </summary>
    private static float ComputeWaterElevationThreshold(List<float> elevations, int waterPercent)
    {
        ArgumentNullException.ThrowIfNull(elevations);
        if (elevations.Count == 0)
            return TerrainNoiseConfig.Default(0).WaterElevationThreshold;

        elevations.Sort();

        if (waterPercent <= 0)
            return elevations[0] - 0.02f;

        if (waterPercent >= 100)
            return elevations[^1] + 0.02f;

        var frac = waterPercent / 100f;
        var idx = (int)Math.Clamp(Math.Floor(frac * (elevations.Count - 1)), 0, elevations.Count - 1);
        return elevations[idx];
    }
}
