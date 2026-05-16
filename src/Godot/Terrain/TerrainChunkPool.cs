#nullable enable
using System.Collections.Generic;

namespace SpecialPG;

/// <summary>Reuses <see cref="TerrainChunkView"/> nodes when chunks leave the visible cull.</summary>
public sealed class TerrainChunkPool
{
    private readonly Stack<TerrainChunkView> _available = new();

    public TerrainChunkView Acquire()
    {
        if (_available.Count > 0)
        {
            var view = _available.Pop();
            view.Visible = true;
            return view;
        }

        return new TerrainChunkView();
    }

    public void Release(TerrainChunkView view)
    {
        view.ReleaseToPool();
        _available.Push(view);
    }
}
