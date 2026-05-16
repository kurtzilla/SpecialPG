#nullable enable
using System;
using Godot;
using SpecialPG.Core.Maps;
using SpecialPG.Core.Maps.Noise;

namespace SpecialPG;

/// <summary>Inputs for rebuilding decor and entity views in visible chunks.</summary>
public readonly struct SurfaceChunkRebuildContext
{
    public SurfaceChunkRebuildContext(
        FloorSlice floor,
        EntityStore entities,
        ITerrainEvaluator evaluator,
        in TerrainNoiseConfig terrain,
        int worldSeed,
        int actorZ,
        float cellSizePx,
        bool decorEnabled,
        bool decorUseMultimesh,
        DecorAtlasCatalog decorCatalog,
        Image? decorAtlasImage,
        EntitySpriteCatalog entityCatalog,
        Image? entityAtlasImage,
        Func<float, float, Vector2> gridCenterToWorld,
        Func<FloorSlice, int, int, Vector2> chunkNorthWestCornerWorld)
    {
        Floor = floor;
        Entities = entities;
        Evaluator = evaluator;
        Terrain = terrain;
        WorldSeed = worldSeed;
        ActorZ = actorZ;
        CellSizePx = cellSizePx;
        DecorEnabled = decorEnabled;
        DecorUseMultimesh = decorUseMultimesh;
        DecorCatalog = decorCatalog;
        DecorAtlasImage = decorAtlasImage;
        EntityCatalog = entityCatalog;
        EntityAtlasImage = entityAtlasImage;
        GridCenterToWorld = gridCenterToWorld;
        ChunkNorthWestCornerWorld = chunkNorthWestCornerWorld;
    }

    public FloorSlice Floor { get; }

    public EntityStore Entities { get; }

    public ITerrainEvaluator Evaluator { get; }

    public TerrainNoiseConfig Terrain { get; }

    public int WorldSeed { get; }

    public int ActorZ { get; }

    public float CellSizePx { get; }

    public bool DecorEnabled { get; }

    public bool DecorUseMultimesh { get; }

    public DecorAtlasCatalog DecorCatalog { get; }

    public Image? DecorAtlasImage { get; }

    public EntitySpriteCatalog EntityCatalog { get; }

    public Image? EntityAtlasImage { get; }

    public Func<float, float, Vector2> GridCenterToWorld { get; }

    public Func<FloorSlice, int, int, Vector2> ChunkNorthWestCornerWorld { get; }
}
