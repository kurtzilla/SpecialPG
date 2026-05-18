#nullable enable
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps.Rendering;

namespace SpecialPG;

/// <summary>One floor chunk's baked terrain texture in grid space.</summary>
public partial class TerrainChunkView : Node2D
{
    /// <summary>Bump when main patch planner changes to invalidate cached textures.</summary>
    public const int TerrainPlannerVersion = 4;

    private Sprite2D? _sprite;
    private ImageTexture? _texture;
    private bool _dirty = true;
    private int _cachedFloorZ = int.MinValue;
    private int _cachedCx = int.MinValue;
    private int _cachedCy = int.MinValue;
    private int _cachedLw = -1;
    private int _cachedLh = -1;
    private float _cachedCellSizePx = float.NaN;
    private bool _cachedUseSprites;
    private int _cachedWorldSeed = int.MinValue;
    private int _cachedTerrainPlannerVersion = int.MinValue;
    private int _cachedWaterAnimFrame = int.MinValue;
    private bool _cachedWaterAnimate;
    private readonly List<TileDrawOp> _mainOpsScratch = new();

    public int ChunkX { get; private set; } = int.MinValue;
    public int ChunkY { get; private set; } = int.MinValue;

    /// <summary>True when the last bake included water categories (for animation dirty scope).</summary>
    public bool ContainsWater { get; private set; }

    public override void _Ready()
    {
        _sprite = new Sprite2D
        {
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(_sprite);
    }

    public void ConfigureChunk(int cx, int cy)
    {
        if (ChunkX != cx || ChunkY != cy)
        {
            ChunkX = cx;
            ChunkY = cy;
            _dirty = true;
        }
    }

    public void MarkDirty() => _dirty = true;

    public void RebuildIfDirty(in TerrainChunkRebuildContext ctx)
    {
        if (!_dirty && MatchesCache(ctx))
        {
            return;
        }

        if (_sprite is null)
        {
            _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (_sprite is null)
            {
                _sprite = new Sprite2D { Centered = false, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
                AddChild(_sprite);
            }
        }

        ctx.Floor.GetChunkWorldCellRange(ChunkX, ChunkY, out _, out _, out var lw, out var lh);
        var img = TerrainChunkRasterizer.BuildChunkImage(
            ctx.Floor,
            ChunkX,
            ChunkY,
            ctx.Evaluator,
            ctx.Terrain,
            ctx.WorldSeed,
            ctx.CellSizePx,
            ctx.UseSprites,
            ctx.Catalog,
            ctx.AtlasImage,
            _mainOpsScratch,
            ctx.WaterAnimate,
            ctx.AnimationTimeMs);

        UpdateContainsWater();

        _texture?.Dispose();
        _texture = ImageTexture.CreateFromImage(img);
        img.Dispose();
        _sprite.Texture = _texture;
        _sprite.Scale = ctx.UseSprites ? Vector2.One : new Vector2(ctx.CellSizePx, ctx.CellSizePx);

        _cachedFloorZ = ctx.Floor.Z;
        _cachedCx = ChunkX;
        _cachedCy = ChunkY;
        _cachedLw = lw;
        _cachedLh = lh;
        _cachedCellSizePx = ctx.CellSizePx;
        _cachedUseSprites = ctx.UseSprites;
        _cachedWorldSeed = ctx.WorldSeed;
        _cachedTerrainPlannerVersion = TerrainPlannerVersion;
        _cachedWaterAnimate = ctx.WaterAnimate;
        _cachedWaterAnimFrame = ctx.WaterAnimate
            ? TerrainWaterAnimation.GetGlobalFrameIndex(ctx.AnimationTimeMs)
            : int.MinValue;
        _dirty = false;
    }

    private void UpdateContainsWater()
    {
        ContainsWater = false;
        for (var i = 0; i < _mainOpsScratch.Count; i++)
        {
            if (TerrainWaterAnimation.IsWaterCategory(_mainOpsScratch[i].Key.Category))
            {
                ContainsWater = true;
                return;
            }
        }
    }

    public void ReleaseToPool()
    {
        ContainsWater = false;
        _texture?.Dispose();
        _texture = null;
        if (_sprite is not null)
        {
            _sprite.Texture = null;
        }

        Visible = false;
        ChunkX = int.MinValue;
        ChunkY = int.MinValue;
        _dirty = true;
        _cachedFloorZ = int.MinValue;
    }

    private bool MatchesCache(in TerrainChunkRebuildContext ctx)
    {
        if (_texture is null)
        {
            return false;
        }

        ctx.Floor.GetChunkWorldCellRange(ChunkX, ChunkY, out _, out _, out var lw, out var lh);
        return _cachedFloorZ == ctx.Floor.Z
               && _cachedCx == ChunkX
               && _cachedCy == ChunkY
               && _cachedLw == lw
               && _cachedLh == lh
               && Mathf.IsEqualApprox(_cachedCellSizePx, ctx.CellSizePx)
               && _cachedUseSprites == ctx.UseSprites
               && _cachedWorldSeed == ctx.WorldSeed
               && _cachedTerrainPlannerVersion == TerrainPlannerVersion
               && _cachedWaterAnimate == ctx.WaterAnimate
               && (!_cachedWaterAnimate || _cachedWaterAnimFrame == TerrainWaterAnimation.GetGlobalFrameIndex(ctx.AnimationTimeMs));
    }
}
