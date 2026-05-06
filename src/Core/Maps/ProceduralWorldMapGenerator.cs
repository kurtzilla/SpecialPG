using System.Collections.Generic;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Bounded procedural fill: deterministic terrain from <see cref="TerrainEvaluator"/> (seed in <see cref="TerrainNoiseConfig"/>).
/// Produces two floors with a two-way vertical link; intended to satisfy <see cref="MapIntegrity.Validate"/> for the filled region.
/// </summary>
public static class ProceduralWorldMapGenerator
{
    private const int MaxElevationSamplesForThreshold = 500_000;

    /// <summary>
    /// Legacy entry: default land/water split from <see cref="MapGenerationParameters.FromSeedOnly"/>.
    /// </summary>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight, int seed,
        int minX = 0, int minY = 0) =>
        BuildBoundedWorld(width, height, chunkWidth, chunkHeight, MapGenerationParameters.FromSeedOnly(seed), minX, minY,
            OriginWalkabilityPatch.DefaultChebyshevRadius);

    /// <summary>
    /// Builds a rectangular world with floors Z=0 and Z=1, filled tile-by-chunk using noise.
    /// Chooses <see cref="TerrainNoiseConfig.WaterElevationThreshold"/> so roughly <see cref="MapGenerationParameters.WaterPercent"/> of
    /// cells classify as water at tile centers.
    /// </summary>
    /// <param name="originPatchChebyshevRadius">
    /// Half-size (Chebyshev) of the guaranteed <see cref="TerrainOverride.ForceLand"/> square applied at map center
    /// (stairs anchor) and again at global (0,0); see <see cref="OriginWalkabilityPatch"/> and
    /// <see cref="LandmassBridgeToLargestComponent"/>.
    /// </param>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight,
        MapGenerationParameters parameters, int minX = 0, int minY = 0,
        int originPatchChebyshevRadius = OriginWalkabilityPatch.DefaultChebyshevRadius)
    {
        if (!parameters.IsValid)
            throw new ArgumentException("MapGenerationParameters must have LandPercent + WaterPercent = 100.", nameof(parameters));

        var map = new WorldMap(width, height, chunkWidth, chunkHeight, minX, minY);

        var baseCfg = TerrainNoiseConfig.Default(parameters.Seed);
        map.TerrainConfig = baseCfg;
        var probeEval = new TerrainEvaluator(baseCfg);

        var elevations = CollectTileCenterElevations(probeEval, minX, minY, width, height, MaxElevationSamplesForThreshold);
        var waterThreshold = ComputeWaterElevationThreshold(elevations, parameters.WaterPercent);

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

                                // Sample at tile center so stored land/water matches SubTileTraversal + TerrainVisualColor
                                // (both use fractional world coords ~ gx+0.5), avoiding “all edges are water” traps.
                                var sampleX = gx + 0.5f;
                                var sampleY = gy + 0.5f;
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

        OriginWalkabilityPatch.ApplyToBoundedWorld(map, originPatchChebyshevRadius, stairX, stairY);
        OriginWalkabilityPatch.ApplyToBoundedWorld(map, originPatchChebyshevRadius, 0, 0);
        LandmassBridgeToLargestComponent.ApplyToBoundedWorld(map);
        return map;
    }

    private static List<float> CollectTileCenterElevations(
        TerrainEvaluator eval,
        int minX,
        int minY,
        int width,
        int height,
        int maxSamples)
    {
        long total = (long)width * height;
        var list = new List<float>((int)Math.Min(total, maxSamples));

        if (total <= maxSamples)
        {
            for (var gy = minY; gy < minY + height; gy++)
            {
                for (var gx = minX; gx < minX + width; gx++)
                    list.Add(eval.EvaluateAt(gx + 0.5f, gy + 0.5f).Elevation);
            }

            return list;
        }

        var stepY = Math.Max(1, (int)Math.Ceiling(height / Math.Sqrt(maxSamples)));
        var stepX = Math.Max(1, (int)Math.Ceiling(width / Math.Sqrt(maxSamples)));
        for (var gy = minY; gy < minY + height; gy += stepY)
        {
            for (var gx = minX; gx < minX + width; gx += stepX)
                list.Add(eval.EvaluateAt(gx + 0.5f, gy + 0.5f).Elevation);
        }

        return list;
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
