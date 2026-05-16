#nullable enable
using System.Collections.Generic;
using Godot;

namespace SpecialPG;

/// <summary>Reuses <see cref="Sprite2D"/> nodes for decor chunk rebuilds.</summary>
public sealed class DecorSpritePool
{
    private readonly Stack<Sprite2D> _available = new();

    public Sprite2D Acquire(Node parent)
    {
        if (_available.Count > 0)
        {
            var sprite = _available.Pop();
            sprite.Visible = true;
            if (sprite.GetParent() != parent)
            {
                parent.AddChild(sprite);
            }

            return sprite;
        }

        var created = new Sprite2D
        {
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        parent.AddChild(created);
        return created;
    }

    public void Release(Sprite2D sprite)
    {
        sprite.Visible = false;
        sprite.Texture = null;
        sprite.RegionEnabled = false;
        if (sprite.GetParent() is Node parent)
        {
            parent.RemoveChild(sprite);
        }

        _available.Push(sprite);
    }

    public void ReleaseAll(IReadOnlyList<Sprite2D> sprites)
    {
        for (var i = 0; i < sprites.Count; i++)
        {
            Release(sprites[i]);
        }
    }
}
