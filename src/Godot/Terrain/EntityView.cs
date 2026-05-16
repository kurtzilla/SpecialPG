#nullable enable
using Godot;
using SpecialPG.Core.Maps;

namespace SpecialPG;

/// <summary>Shell sprite for one <see cref="EntityRecord"/>.</summary>
public partial class EntityView : Node2D
{
    private Sprite2D? _sprite;

    public EntityId EntityId { get; private set; } = EntityId.None;

    public override void _Ready()
    {
        _sprite = new Sprite2D
        {
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        AddChild(_sprite);
    }

    public void Configure(in EntityRecord record, in SurfaceChunkRebuildContext ctx)
    {
        EntityId = record.Id;
        if (_sprite is null)
        {
            _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (_sprite is null)
            {
                _sprite = new Sprite2D { Centered = true, TextureFilter = TextureFilterEnum.Nearest };
                AddChild(_sprite);
            }
        }

        var fx = record.X + (record.SubCellX + 0.5f) / SubTileGrid.Resolution;
        var fy = record.Y + (record.SubCellY + 0.5f) / SubTileGrid.Resolution;
        Position = ctx.GridCenterToWorld(fx, fy);
        ZIndex = record.Y;

        _sprite.Scale = Vector2.One;
        if (ctx.EntityCatalog.AtlasTexture is not null && ctx.EntityCatalog.TryGetPixelRect(record.Kind, out var srcRect))
        {
            _sprite.Texture = ctx.EntityCatalog.AtlasTexture;
            _sprite.RegionEnabled = true;
            _sprite.RegionRect = new Rect2(
                srcRect.Position.X,
                srcRect.Position.Y,
                srcRect.Size.X,
                srcRect.Size.Y);
            _sprite.Modulate = Colors.White;
        }
        else
        {
            _sprite.Texture = null;
            _sprite.RegionEnabled = false;
            _sprite.Modulate = record.Kind switch
            {
                EntityKinds.Actor => new Color(0.86f, 0.31f, 0.31f),
                EntityKinds.Prop => new Color(0.78f, 0.63f, 0.24f),
                _ => new Color(0.7f, 0.7f, 0.75f),
            };
            _sprite.Scale = new Vector2(ctx.CellSizePx * 0.35f, ctx.CellSizePx * 0.35f);
        }
    }

    public void ReleaseToPool()
    {
        if (_sprite is not null)
        {
            _sprite.Texture = null;
        }

        EntityId = EntityId.None;
        Visible = false;
    }
}
