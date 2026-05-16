#nullable enable
using Godot;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG;

/// <summary>Inputs for rebuilding one <see cref="TerrainChunkView"/> texture.</summary>
public readonly struct TerrainChunkRebuildContext
{
    public TerrainChunkRebuildContext(
        FloorSlice floor,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        float cellSizePx,
        bool useSprites,
        TerrainAtlasCatalog catalog,
        Image? atlasImage,
        bool waterAnimate,
        long animationTimeMs,
        bool transitionsEnabled)
    {
        Floor = floor;
        Evaluator = evaluator;
        Terrain = terrain;
        WorldSeed = worldSeed;
        CellSizePx = cellSizePx;
        UseSprites = useSprites;
        Catalog = catalog;
        AtlasImage = atlasImage;
        WaterAnimate = waterAnimate;
        AnimationTimeMs = animationTimeMs;
        TransitionsEnabled = transitionsEnabled;
    }

    public FloorSlice Floor { get; }

    public ITerrainEvaluator Evaluator { get; }

    public TerrainNoiseConfig Terrain { get; }

    public int WorldSeed { get; }

    public float CellSizePx { get; }

    public bool UseSprites { get; }

    public TerrainAtlasCatalog Catalog { get; }

    public Image? AtlasImage { get; }

    public bool WaterAnimate { get; }

    public long AnimationTimeMs { get; }

    public bool TransitionsEnabled { get; }
}
