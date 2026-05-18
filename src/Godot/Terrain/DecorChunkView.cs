#nullable enable
using System.Collections.Generic;
using Godot;
using SpecialPG.Core.Maps.Rendering;

namespace SpecialPG;

/// <summary>Sparse decor sprites for one map chunk.</summary>
public partial class DecorChunkView : Node2D
{
    public const int DecorPlannerVersion = 1;

    private static Shader? s_multimeshShader;

    private readonly List<Sprite2D> _sprites = new();
    private readonly List<DecorCell> _scratch = new();
    private readonly DecorSpritePool _spritePool = new();
    private MultiMeshInstance2D? _multiMesh;
    private bool _dirty = true;
    private int _cachedCx = int.MinValue;
    private int _cachedCy = int.MinValue;
    private int _cachedFloorZ = int.MinValue;
    private int _cachedWorldSeed = int.MinValue;
    private bool _cachedDecorEnabled;
    private bool _cachedUseMultimesh;

    public int ChunkX { get; private set; } = int.MinValue;
    public int ChunkY { get; private set; } = int.MinValue;

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

    public bool NeedsRebuild(in SurfaceChunkRebuildContext ctx) => _dirty || !MatchesCache(ctx);

    public void RebuildIfDirty(in SurfaceChunkRebuildContext ctx)
    {
        if (!_dirty && MatchesCache(ctx))
            return;

        ClearVisuals();
        if (ctx.DecorEnabled && ctx.DecorAtlasImage is not null)
        {
            DecorScatterPlanner.PlanChunk(
                ctx.Floor,
                ChunkX,
                ChunkY,
                ctx.Evaluator,
                ctx.Terrain,
                ctx.WorldSeed,
                _scratch);

            if (ctx.DecorUseMultimesh)
                RebuildMultiMesh(ctx);
            else
                RebuildSprites(ctx);
        }

        _cachedCx = ChunkX;
        _cachedCy = ChunkY;
        _cachedFloorZ = ctx.Floor.Z;
        _cachedWorldSeed = ctx.WorldSeed;
        _cachedDecorEnabled = ctx.DecorEnabled;
        _cachedUseMultimesh = ctx.DecorUseMultimesh;
        _dirty = false;
    }

    public void ReleaseToPool()
    {
        ClearVisuals();
        Visible = false;
        ChunkX = int.MinValue;
        ChunkY = int.MinValue;
        _dirty = true;
        _cachedFloorZ = int.MinValue;
    }

    private void RebuildSprites(in SurfaceChunkRebuildContext ctx)
    {
        foreach (var cell in _scratch)
        {
            if (!ctx.DecorCatalog.TryGetPixelRect(cell.VariantIndex, out var srcRect))
                continue;

            var center = ctx.GridCenterToWorld(cell.Gx + 0.5f, cell.Gy + 0.5f);
            var sprite = _spritePool.Acquire(this);
            sprite.Position = center - Position;
            sprite.Texture = ctx.DecorCatalog.AtlasTexture;
            sprite.RegionEnabled = true;
            sprite.RegionRect = new Rect2(
                srcRect.Position.X,
                srcRect.Position.Y,
                srcRect.Size.X,
                srcRect.Size.Y);
            _sprites.Add(sprite);
        }
    }

    private void RebuildMultiMesh(in SurfaceChunkRebuildContext ctx)
    {
        if (_scratch.Count == 0)
            return;

        var atlasTex = ctx.DecorCatalog.AtlasTexture;
        if (atlasTex is null)
            return;

        var atlasW = (float)ctx.DecorAtlasImage!.GetWidth();
        var atlasH = (float)ctx.DecorAtlasImage.GetHeight();

        _multiMesh = new MultiMeshInstance2D { Name = "DecorMultiMesh" };
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseCustomData = true,
            InstanceCount = _scratch.Count,
        };
        var quad = new QuadMesh { Size = Vector2.One };
        mm.Mesh = quad;
        _multiMesh.Multimesh = mm;

        s_multimeshShader ??= GD.Load<Shader>("res://shaders/decor_multimesh.gdshader");
        if (s_multimeshShader is not null)
        {
            var mat = new ShaderMaterial { Shader = s_multimeshShader };
            mat.SetShaderParameter("atlas_tex", atlasTex);
            _multiMesh.Material = mat;
        }

        AddChild(_multiMesh);

        for (var i = 0; i < _scratch.Count; i++)
        {
            var cell = _scratch[i];
            if (!ctx.DecorCatalog.TryGetPixelRect(cell.VariantIndex, out var srcRect))
                continue;

            var center = ctx.GridCenterToWorld(cell.Gx + 0.5f, cell.Gy + 0.5f);
            var local = center - Position;
            var scale = ctx.CellSizePx;
            var xf = Transform2D.Identity;
            xf.X = new Vector2(scale, 0f);
            xf.Y = new Vector2(0f, scale);
            xf.Origin = local;
            mm.SetInstanceTransform2D(i, xf);

            var uv = new Color(
                srcRect.Position.X / atlasW,
                srcRect.Position.Y / atlasH,
                srcRect.Size.X / atlasW,
                srcRect.Size.Y / atlasH);
            mm.SetInstanceCustomData(i, uv);
        }
    }

    private void ClearVisuals()
    {
        _spritePool.ReleaseAll(_sprites);
        _sprites.Clear();

        if (_multiMesh is not null)
        {
            _multiMesh.QueueFree();
            _multiMesh = null;
        }
    }

    private bool MatchesCache(in SurfaceChunkRebuildContext ctx) =>
        _cachedCx == ChunkX
        && _cachedCy == ChunkY
        && _cachedFloorZ == ctx.Floor.Z
        && _cachedWorldSeed == ctx.WorldSeed
        && _cachedDecorEnabled == ctx.DecorEnabled
        && _cachedUseMultimesh == ctx.DecorUseMultimesh;
}
