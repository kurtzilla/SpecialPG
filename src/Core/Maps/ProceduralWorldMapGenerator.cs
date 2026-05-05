namespace SpecialPG.Core.Maps;

/// <summary>
/// Bounded procedural fill: deterministic per chunk from <paramref name="seed"/> (Factorio-style chunk addressing).
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
    /// Builds a rectangular world with floors Z=0 and Z=1, filled tile-by-chunk using one RNG per chunk.
    /// Land vs water density follows <paramref name="parameters"/> (non-stair cells).
    /// </summary>
    public static WorldMap BuildBoundedWorld(int width, int height, int chunkWidth, int chunkHeight,
        MapGenerationParameters parameters, int minX = 0, int minY = 0)
    {
        if (!parameters.IsValid)
            throw new ArgumentException("MapGenerationParameters must have LandPercent + WaterPercent = 100.", nameof(parameters));

        var map = new WorldMap(width, height, chunkWidth, chunkHeight, minX, minY);
        var dims = new MapChunkDimensions(chunkWidth, chunkHeight);

        var stairX = minX + width / 2;
        var stairY = minY + height / 2;
        var seed = parameters.Seed;
        var waterPercent = parameters.WaterPercent;

        for (var z = 0; z < 2; z++)
        {
            var floor = map.GetOrCreateFloor(z);
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

                            if (gx == stairX && gy == stairY)
                            {
                                floor.Set(gx, gy, new TileData
                                {
                                    TileKind = TerrainTileKinds.Land,
                                    Flags = 0,
                                    Variant = 0,
                                });
                                continue;
                            }

                            if (rng.Next(100) < waterPercent)
                            {
                                floor.Set(gx, gy, new TileData
                                {
                                    TileKind = TerrainTileKinds.Water,
                                    Flags = TileFlags.Blocked,
                                    Variant = 0,
                                });
                                continue;
                            }

                            var kind = (ushort)(TerrainTileKinds.Land + rng.Next(0, 4));
                            floor.Set(gx, gy, new TileData
                            {
                                TileKind = kind,
                                Flags = 0,
                                Variant = (byte)rng.Next(0, 4),
                            });
                        }
                    }
                }
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
