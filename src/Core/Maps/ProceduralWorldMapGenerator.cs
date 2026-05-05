using SpecialPG.Core.Maps.Noise;

namespace SpecialPG.Core.Maps;

/// <summary>
/// Bounded procedural fill: deterministic terrain from <see cref="TerrainEvaluator"/> (seed in <see cref="TerrainNoiseConfig"/>).
/// Produces two floors with a two-way vertical link; intended to satisfy <see cref="MapIntegrity.Validate"/> for the filled region.
/// </summary>
public static class ProceduralWorldMapGenerator
{
    /// <summary>
    /// Legacy entry: default land/water split from <see cref="MapGenerationParameters.FromSeedOnly"/>.
    /// </summary>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight, int seed,
        int minX = 0, int minY = 0) =>
        BuildBoundedWorld(width, height, chunkWidth, chunkHeight, MapGenerationParameters.FromSeedOnly(seed), minX, minY);

    /// <summary>
    /// Builds a rectangular world with floors Z=0 and Z=1, filled tile-by-chunk using noise.
    /// <see cref="MapGenerationParameters.WaterPercent"/> biases <see cref="TerrainNoiseConfig.WaterElevationThreshold"/>.
    /// </summary>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight,
        MapGenerationParameters parameters, int minX = 0, int minY = 0)
    {
        if (!parameters.IsValid)
            throw new ArgumentException("MapGenerationParameters must have LandPercent + WaterPercent = 100.", nameof(parameters));

        var map = new WorldMap(width, height, chunkWidth, chunkHeight, minX, minY);
        var w = parameters.WaterPercent / 100f;
        var terrainCfg = TerrainNoiseConfig.Default(parameters.Seed) with
        {
            WaterElevationThreshold = -0.35f + w * 1.15f,
        };
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

                                TileCell cell;
                                if (gx == stairX && gy == stairY)
                                {
                                    cell = eval.ToTileCell(gx, gy, 0) with
                                    {
                                        Override = TerrainOverride.ForceLand,
                                        Flags = 0,
                                    };
                                }
                                else
                                {
                                    cell = eval.ToTileCell(gx, gy, (byte)rng.Next(0, 4));
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

        return map;
    }
}
